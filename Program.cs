using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace AMN.ManifestGen
{
    internal class Program
    {
        private const string PluginAttributeFullName = "Another_Mirai_Native.Abstractions.Models.PluginInfo";

        private static string InputFilePath { get; set; }

        private static string OutputFilePath { get; set; }

        private static string TargetFramework { get; set; }

        private static bool IsTargetNetFramework { get; set; } = true;

        private static int Main(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].ToLower() == "-i" && i < args.Length - 1)
                {
                    InputFilePath = args[i + 1];
                }
                else if (args[i].ToLower() == "-o" && i < args.Length - 1)
                {
                    OutputFilePath = args[i + 1];
                }
                else if (args[i].ToLower() == "-t" && i < args.Length - 1)
                {
                    TargetFramework = args[i + 1];
                    IsTargetNetFramework = CheckIsTargetNetFramework();
                }
            }
            if (string.IsNullOrEmpty(InputFilePath) || string.IsNullOrEmpty(OutputFilePath))
            {
                Console.Error.WriteLine("未指定输入文件或输出文件路径");
                return -1;
            }
            if (!File.Exists(InputFilePath))
            {
                Console.Error.WriteLine("输入文件路径不存在");
                return -1;
            }
            var info = ReadManifest(InputFilePath);

            File.WriteAllText(OutputFilePath, JsonConvert.SerializeObject(info, Formatting.Indented));
            Console.WriteLine($"Manifest 已生成成功，Json文件写出到 {OutputFilePath}");
            return 0;
        }

        private static bool CheckIsTargetNetFramework()
        {
            if (string.IsNullOrWhiteSpace(TargetFramework))
            {
                throw new InvalidDataException("无效的生成目标");
            }
            Console.WriteLine($"正在处理生成目标: {TargetFramework}");
            int identityPos = TargetFramework.IndexOf("net", StringComparison.OrdinalIgnoreCase);
            int dotPos = TargetFramework.IndexOf('.', identityPos + 3);
            string possibleNetVersion = TargetFramework.Substring(identityPos + 3, dotPos >= 0 ? dotPos : TargetFramework.Length - identityPos);
            if (int.TryParse(possibleNetVersion, out int netVersion))
            {
                return netVersion >= 40;
            }
            else
            {
                throw new InvalidDataException($"无效的.Net版本: {possibleNetVersion}");
            }
        }

        public static AppInfo ReadManifest(string assemblyPath)
        {
            using var fs = File.OpenRead(assemblyPath);
            using var pe = new PEReader(fs);

            if (!pe.HasMetadata)
            {
                throw new InvalidOperationException("该文件不包含 .NET 元数据，无法读取插件信息。");
            }

            var mr = pe.GetMetadataReader();

            AppInfo? found = null;
            AppInfo.Event[] events = 
                [
                    new AppInfo.Event{
                        id = 1003,
                        type = 1003,
                        function = "OnEnableAsync",
                        name = "插件启用事件",
                        priority = 30000
                    },
                    new AppInfo.Event{
                        id = 1004,
                        type = 1004,
                        function = "OnDisableAsync",
                        name = "插件禁用事件",
                        priority = 30000
                    },
                ];
            AppInfo.Menu[] menus = Array.Empty<AppInfo.Menu>();
            string appId = string.Empty;
            foreach (var typeHandle in mr.TypeDefinitions)
            {
                var td = mr.GetTypeDefinition(typeHandle);

                var typeName = mr.GetString(td.Name);
                if (typeName == "<Module>")
                {
                    continue;
                }

                var e = EventBuilder.BuildEventsFromEntryType(mr, td);
                events = [.. events, .. e];
                var m = MenuBuilder.BuildMenusFromEntryType(mr, td);
                menus = [.. menus, .. m];

                foreach (var caHandle in td.GetCustomAttributes())
                {
                    var ca = mr.GetCustomAttribute(caHandle);

                    if (!MetadataHelpers.IsTargetAttribute(mr, ca, PluginAttributeFullName))
                    {
                        continue;
                    }

                    // 解码 attribute 的参数（假设：.ctor(string id, string name, string version)，可带命名参数 Author/Description）
                    var (id, name, version, author, desc) = MetadataHelpers.DecodePluginAttribute(mr, ca);
                    var fullTypeName = MetadataHelpers.GetFullTypeName(mr, td);
                    appId = id;

                    var manifest = new AppInfo
                    {
                        name = name,
                        version = version,
                        author = author ?? "",
                        description = desc ?? "",
                        status = Array.Empty<object>(),
                        auth = Enum.GetValues(typeof(PluginAPIType))
                                .Cast<PluginAPIType>()
                                .Select(x => (int)x)
                                .Distinct()
                                .OrderBy(x => x)
                                .ToArray(),
                    };
                    if (found != null)
                    {
                        throw new InvalidOperationException(
                            $"发现多个 [{PluginAttributeFullName}]。\n请确保只有一个入口类型标记 PluginAttribute。");
                    }

                    found = manifest;
                }
            }
            found._event = events;
            found.menu = menus;
            Console.WriteLine($"AppId: {appId}; 插件名称: {found.name}; 版本: {found.version}; 作者: {found.author}; 描述: {found.description}");
            Console.WriteLine($"检索到 {found._event.Length} 条事件处理器");
            Console.WriteLine($"检索到 {found.menu.Length} 个窗口处理器");
            Console.WriteLine($"生成目标是: {TargetFramework}");
            found.LoaderType = IsTargetNetFramework ? 0 : 1;

            return found ?? throw new InvalidOperationException($"未找到 [{PluginAttributeFullName}] 标记的入口类型。");
        }
    }
}
