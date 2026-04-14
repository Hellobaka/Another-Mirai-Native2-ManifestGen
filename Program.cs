using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace AMN.ManifestGen
{
    internal class Program
    {
        private const string PluginAttributeFullName = "Another_Mirai_Native.Abstractions.Models.PluginInfo";
        private const string DependencyManifestFileName = "DependencyManifest-dotnet9.json";
        private const string ILRepackFileName = "ILRepack.exe";

        private static string InputFilePath { get; set; }

        private static string OutputFilePath { get; set; }

        private static string TargetFramework { get; set; }

        private static bool IsTargetNetFramework { get; set; } = true;

        private static bool CleanOutputRequired { get; set; }

        private static bool IgnoreDependencyVersion { get; set; }

        private static int Main(string[] args)
        {
            //args = ["-i", @"D:\Code\DemoPlugin\bin\Debug\net9.0\Native_DemoPlugin.dll", 
            //    "-o", @"D:\Code\DemoPlugin\bin\Debug\net9.0\Native_DemoPlugin.json",
            //    "-t", "net9.0",
            //    "-c"];
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
                else if (args[i].ToLower() == "-c")
                {
                    CleanOutputRequired = true;
                }
                else if (args[i] == "--ignoreDependencyVersion")
                {
                    IgnoreDependencyVersion = true;
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
            if (CleanOutputRequired)
            {
                return CleanOutput();
            }
            return 0;
        }

        private static int CleanOutput()
        {
            Console.WriteLine("开始整理输出目录");
            string dependencyManifestFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DependencyManifestFileName);
            string ilRepacker = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ILRepackFileName);
           
            if (!File.Exists(dependencyManifestFileName))
            {
                Console.Error.WriteLine($"未找到依赖清单文件 {dependencyManifestFileName}，无法执行整理操作");
                return -1;
            }
            if (!File.Exists(ilRepacker))
            {
                Console.Error.WriteLine("无法找到 ILRepack 工具");
                return -1;
            }
            string inputFileName = Path.GetFileName(InputFilePath);
            string renamedFilePath = Path.Combine(Path.GetDirectoryName(InputFilePath), "Native_" + inputFileName);
            if (File.Exists(renamedFilePath))
            {
                File.Delete(renamedFilePath);
            }
            string outputFilePath = Path.ChangeExtension(InputFilePath, ".Merged.dll");
            if (File.Exists(outputFilePath))
            {
                File.Delete(outputFilePath);
            }
            DllManifest[] manifests = JsonConvert.DeserializeObject<DllManifest[]>(File.ReadAllText(dependencyManifestFileName));
            DllManifest[] localManifests = Directory.GetFiles(Path.GetDirectoryName(InputFilePath), "*.dll")
                .Where(x => Path.GetFileName(x) != inputFileName)
                .Select(GetDllManifest)
                .ToArray();
            List<DllManifest> duplicatedDlls = [];
            foreach (DllManifest manifest in manifests)
            {
                var local = localManifests.FirstOrDefault(m =>
                {
                    if (m.AssemblyName != manifest.AssemblyName)
                        return false;
                    if (IgnoreDependencyVersion)
                        return true; // 只匹配名称，忽略版本和公钥
                    return m.Version == manifest.Version && m.PublicToken == manifest.PublicToken;
                });
                if (local != null && File.Exists(local.FullPath))
                {
                    // 说明当前目录下已经存在同名同版本的 dll，可以删除
                    duplicatedDlls.Add(local);
                }
            }
            // 剩余的dll打包
            var otherDlls = localManifests.Where(x => !duplicatedDlls.Any(o => o.Equals(x)));
                //.Where(x => !IsFrameworkDll(x));

            // 获取 .NET 运行时目录用于解析框架程序集
            var runtimeLibDirs = GetRuntimeLibDirectories();

            Console.WriteLine($"[ILRepack] 目标框架: {TargetFramework}");
            Console.WriteLine($"[ILRepack] 解析到运行时版本: {GetRuntimeVersionFromTargetFramework() ?? "未知"}");
            Console.WriteLine($"[ILRepack] 找到 {runtimeLibDirs.Count} 个运行时目录");

            string command = $"/parallel /ndebug ";
            // 添加输出目录作为 lib
            command += $"/lib:\"{Path.GetDirectoryName(InputFilePath)}\" ";
            // 添加 .NET 运行时目录作为 lib（用于解析框架程序集引用）
            foreach (var dir in runtimeLibDirs)
            {
                command += $"/lib:\"{dir}\" ";
            }
            command += $"/out:\"{outputFilePath}\" \"{InputFilePath}\" ";

            Console.WriteLine($"[ILRepack] 待合并的 DLL 数量: {otherDlls.Count()}");
            foreach (var dll in otherDlls)
            {
                command += $"\"{dll.FullPath}\" ";
                Console.WriteLine($"  - {dll.AssemblyName} ({dll.Version})");
            }
            var startInfo = new ProcessStartInfo()
            {
                Arguments = command,
                CreateNoWindow = true,
                UseShellExecute = false,
                FileName = ilRepacker,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            var p = Process.Start(startInfo);

            // 先读取输出，再等待退出，避免缓冲区满导致死锁
            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();

            Console.WriteLine(stdout);
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                Console.Error.WriteLine($"ILRepack 发生错误: {stderr}");
                return -1;
            }

            // 删除已合并的依赖 DLL（不包括原始插件 DLL）
            foreach (var dll in otherDlls.Concat(duplicatedDlls))
            {
                File.Delete(dll.FullPath);
                Console.WriteLine($"[ILRepack] 已删除: {Path.GetFileName(dll.FullPath)}");
            }

            // 注意：不删除原始 DLL 和 Merged DLL
            // targets 文件会处理：Merged.dll -> 原始 DLL -> Native_前缀.dll

            Console.WriteLine($"[ILRepack] 合并完成: {Path.GetFileName(outputFilePath)}");
            return 0;
        }

        private static bool IsFrameworkDll(DllManifest manifest)
        {
            string assemblyName = manifest.AssemblyName;
            return assemblyName.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
                || assemblyName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 获取 .NET 运行时目录列表，用于 ILRepack 解析框架程序集引用
        /// </summary>
        private static List<string> GetRuntimeLibDirectories()
        {
            var dirs = new List<string>();

            // 根据 TargetFramework 查找对应的运行时目录
            // TargetFramework 格式: net9, net9.0, net8, net8.0 等
            string? runtimeVersion = GetRuntimeVersionFromTargetFramework();
            if (runtimeVersion != null)
            {
                var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
                if (string.IsNullOrEmpty(dotnetRoot))
                {
                    // 默认路径
                    dotnetRoot = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet")
                        : "/usr/share/dotnet";
                }

                if (Directory.Exists(dotnetRoot))
                {
                    // 查找对应版本的 shared 目录
                    var sharedDir = Path.Combine(dotnetRoot, "shared", "Microsoft.NETCore.App");
                    if (Directory.Exists(sharedDir))
                    {
                        // 查找匹配版本的目录
                        var versions = Directory.GetDirectories(sharedDir)
                            .Where(d =>
                            {
                                var name = Path.GetFileName(d);
                                // 匹配主版本号，如 "9.0.5" 匹配 "net9"
                                return name.StartsWith(runtimeVersion + ".");
                            })
                            .OrderByDescending(d => d)
                            .FirstOrDefault();

                        if (versions != null)
                        {
                            dirs.Add(versions);
                            Console.WriteLine($"[ILRepack] 使用 .NET 运行时目录: {versions}");
                        }
                        else
                        {
                            // 如果没找到精确匹配，尝试获取最新版本
                            versions = Directory.GetDirectories(sharedDir)
                                .Where(d => Path.GetFileName(d).StartsWith(runtimeVersion.Split('.')[0] + "."))
                                .OrderByDescending(d => d)
                                .FirstOrDefault();
                            if (versions != null)
                            {
                                dirs.Add(versions);
                                Console.WriteLine($"[ILRepack] 使用 .NET 运行时目录: {versions}");
                            }
                        }
                    }
                }
            }

            return dirs.Distinct().ToList();
        }

        /// <summary>
        /// 从 TargetFramework 字符串解析运行时版本
        /// </summary>
        private static string? GetRuntimeVersionFromTargetFramework()
        {
            if (string.IsNullOrEmpty(TargetFramework))
                return null;

            // net9 -> 9.0, net9.0 -> 9.0, net8 -> 8.0, net8.0 -> 8.0
            var tfm = TargetFramework.ToLowerInvariant();
            if (tfm.StartsWith("net"))
            {
                var versionPart = tfm.Substring(3);
                // net9 -> "9", net9.0 -> "9.0", net472 -> "472"
                if (versionPart.Length > 0)
                {
                    // 对于 .NET Core/.NET 5+，格式是 netX 或 netX.Y
                    // 尝试解析主版本号
                    var dotIndex = versionPart.IndexOf('.');
                    if (dotIndex > 0)
                    {
                        return versionPart; // 已经是 X.Y 格式
                    }
                    else
                    {
                        // 只有主版本号，如 "9"
                        if (int.TryParse(versionPart, out var major) && major >= 5)
                        {
                            return $"{major}.0";
                        }
                    }
                }
            }

            return null;
        }

        private static DllManifest GetDllManifest(string item)
        {
            AssemblyName assemblyName = AssemblyName.GetAssemblyName(item);
            Version assemblyVersion = assemblyName.Version;

            // PublicKeyToken
            byte[] tokenBytes = assemblyName.GetPublicKeyToken();
            string publicKeyToken = tokenBytes == null || tokenBytes.Length == 0
                ? "null"
                : BitConverter.ToString(tokenBytes).Replace("-", "").ToLowerInvariant();

            return new DllManifest
            {
                AssemblyName = assemblyName.Name,
                Version = assemblyVersion == null ? "null" : assemblyVersion.ToString(),
                PublicToken = publicKeyToken,
                FullPath = Path.GetFullPath(item)
            };
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
            string possibleNetVersion = TargetFramework.Substring(identityPos + 3, dotPos >= 0 ? dotPos - 3 : TargetFramework.Length - identityPos - 3);
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

    internal class DllManifest
    {
        public string AssemblyName { get; set; }

        public string Version { get; set; }

        public string PublicToken { get; set; }

        public string FullPath { get; set; }

        public override string ToString()
        {
            return this.AssemblyName;
        }

        public override bool Equals(object obj)
        {
            if (obj is not DllManifest manifest) { return false; }
            return FullPath.Equals(manifest.FullPath);
        }
    }
}
