using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace AMN.ManifestGen
{
    internal static class ManifestReader
    {
        private const string PluginAttributeFullName = "Another_Mirai_Native.Abstractions.Models.PluginInfo";

        public static AppInfo ReadManifest(string assemblyPath, string targetFramework, bool isTargetNetFramework)
        {
            using var fs = File.OpenRead(assemblyPath);
            using var pe = new PEReader(fs);

            if (!pe.HasMetadata)
                throw new InvalidOperationException("该文件不包含 .NET 元数据，无法读取插件信息。");

            var mr = pe.GetMetadataReader();

            AppInfo found = null;
            AppInfo.Event[] events =
            [
                new AppInfo.Event { id = 1003, type = 1003, function = "OnEnableAsync", name = "插件启用事件", priority = 30000 },
                new AppInfo.Event { id = 1004, type = 1004, function = "OnDisableAsync", name = "插件禁用事件", priority = 30000 },
            ];
            AppInfo.Menu[] menus = Array.Empty<AppInfo.Menu>();
            string appId = string.Empty;

            foreach (var typeHandle in mr.TypeDefinitions)
            {
                var td = mr.GetTypeDefinition(typeHandle);

                if (mr.GetString(td.Name) == "<Module>")
                    continue;

                var e = EventBuilder.BuildEventsFromEntryType(mr, td);
                events = [.. events, .. e];
                var m = MenuBuilder.BuildMenusFromEntryType(mr, td);
                menus = [.. menus, .. m];

                foreach (var caHandle in td.GetCustomAttributes())
                {
                    var ca = mr.GetCustomAttribute(caHandle);

                    if (!MetadataHelpers.IsTargetAttribute(mr, ca, PluginAttributeFullName))
                        continue;

                    // 解码 attribute 的参数（假设：.ctor(string id, string name, string version)，可带命名参数 Author/Description）
                    var (id, name, version, author, desc) = MetadataHelpers.DecodePluginAttribute(mr, ca);
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
                        throw new InvalidOperationException($"发现多个 [{PluginAttributeFullName}]。\n请确保只有一个入口类型标记 PluginAttribute。");

                    found = manifest;
                }
            }

            found._event = events;
            found.menu = menus;
            Console.WriteLine($"AppId: {appId}; 插件名称: {found.name}; 版本: {found.version}; 作者: {found.author}; 描述: {found.description}");
            Console.WriteLine($"检索到 {found._event.Length} 条事件处理器");
            Console.WriteLine($"检索到 {found.menu.Length} 个窗口处理器");
            Console.WriteLine($"生成目标是: {targetFramework}");
            found.LoaderType = isTargetNetFramework ? 0 : 1;

            return found ?? throw new InvalidOperationException($"未找到 [{PluginAttributeFullName}] 标记的入口类型。");
        }
    }
}
