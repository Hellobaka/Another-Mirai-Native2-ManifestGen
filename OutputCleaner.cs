using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AMN.ManifestGen
{
    internal static class OutputCleaner
    {
        private const string DependencyManifestFileName = "DependencyManifest-dotnet9.json";
        private const string ILRepackFileName = "ILRepack.exe";

        public static int CleanOutput(string inputFilePath, string targetFramework, bool ignoreDependencyVersion, string ilrepackAdditionParam)
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

            string inputFileName = Path.GetFileName(inputFilePath);
            string renamedFilePath = Path.Combine(Path.GetDirectoryName(inputFilePath), "Native_" + inputFileName);
            if (File.Exists(renamedFilePath))
            {
                File.Delete(renamedFilePath);
            }

            string outputFilePath = inputFilePath;

            DllManifest[] manifests = JsonConvert.DeserializeObject<DllManifest[]>(File.ReadAllText(dependencyManifestFileName));
            DllManifest[] localManifests = Directory.GetFiles(Path.GetDirectoryName(inputFilePath), "*.dll")
                .Where(x => Path.GetFileName(x) != inputFileName)
                .Select(GetDllManifest)
                .ToArray();

            List<DllManifest> duplicatedDlls = [];
            foreach (DllManifest manifest in manifests)
            {
                var local = localManifests.FirstOrDefault(m =>
                {
                    if (m.AssemblyName != manifest.AssemblyName)
                    {
                        return false;
                    }

                    if (ignoreDependencyVersion)
                    {
                        return true; // 只匹配名称，忽略版本和公钥
                    }

                    return m.Version == manifest.Version && m.PublicToken == manifest.PublicToken;
                });
                if (local != null && File.Exists(local.FullPath))
                {
                    // 说明当前目录下已经存在同名同版本的 dll，可以删除
                    duplicatedDlls.Add(local);
                }
            }
            string[] ignores = ["Another-Mirai-Native.Abstractions.dll"];
            // 剩余的dll打包
            var otherDlls = localManifests.Where(x => !duplicatedDlls.Any(o => o.Equals(x)))
                .Where(x => !ignores.Contains(Path.GetFileName(x.FullPath)));
            //.Where(x => !IsFrameworkDll(x));

            // 获取 .NET 运行时目录用于解析框架程序集
            var runtimeLibDirs = GetRuntimeLibDirectories(targetFramework);

            Console.WriteLine($"[ILRepack] 目标框架: {targetFramework}");
            Console.WriteLine($"[ILRepack] 解析到运行时版本: {GetRuntimeVersionFromTargetFramework(targetFramework) ?? "未知"}");
            Console.WriteLine($"[ILRepack] 找到 {runtimeLibDirs.Count} 个运行时目录");

            string command = $"/parallel /ndebug {(string.IsNullOrWhiteSpace(ilrepackAdditionParam) ? "" : ilrepackAdditionParam + " ")}";
            // 添加输出目录作为 lib
            command += $"/lib:\"{Path.GetDirectoryName(inputFilePath)}\" ";
            // 添加 .NET 运行时目录作为 lib（用于解析框架程序集引用）
            foreach (var dir in runtimeLibDirs)
            {
                command += $"/lib:\"{dir}\" ";
            }

            command += $"/out:\"{outputFilePath}\" \"{inputFilePath}\" ";

            Console.WriteLine($"[ILRepack] 待合并的 DLL 数量: {otherDlls.Count()}");
            if(otherDlls.Count() == 0)
            {
                Console.WriteLine($"[ILRepack] 无需 ILRepack");
                return 0;
            }
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
        private static List<string> GetRuntimeLibDirectories(string targetFramework)
        {
            var dirs = new List<string>();

            string runtimeVersion = GetRuntimeVersionFromTargetFramework(targetFramework);
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
                    var sharedDir = Path.Combine(dotnetRoot, "shared", "Microsoft.NETCore.App");
                    if (Directory.Exists(sharedDir))
                    {
                        // 查找匹配版本的目录，如 "9.0.5" 匹配 "net9"
                        var versions = Directory.GetDirectories(sharedDir)
                            .Where(d => Path.GetFileName(d).StartsWith(runtimeVersion + "."))
                            .OrderByDescending(d => d)
                            .FirstOrDefault();

                        if (versions == null)
                        {
                            // 如果没找到精确匹配，尝试获取最新版本
                            versions = Directory.GetDirectories(sharedDir)
                                .Where(d => Path.GetFileName(d).StartsWith(runtimeVersion.Split('.')[0] + "."))
                                .OrderByDescending(d => d)
                                .FirstOrDefault();
                        }

                        if (versions != null)
                        {
                            dirs.Add(versions);
                            Console.WriteLine($"[ILRepack] 使用 .NET 运行时目录: {versions}");
                        }
                    }
                }
            }

            return dirs.Distinct().ToList();
        }

        /// <summary>
        /// 从 TargetFramework 字符串解析运行时版本
        /// </summary>
        private static string GetRuntimeVersionFromTargetFramework(string targetFramework)
        {
            if (string.IsNullOrEmpty(targetFramework))
            {
                return null;
            }

            // net9 -> 9.0, net9.0 -> 9.0, net8 -> 8.0, net8.0 -> 8.0
            var tfm = targetFramework.ToLowerInvariant();
            if (tfm.StartsWith("net"))
            {
                var versionPart = tfm.Substring(3);
                if (versionPart.Length > 0)
                {
                    var dotIndex = versionPart.IndexOf('.');
                    if (dotIndex > 0)
                    {
                        return versionPart; // 已经是 X.Y 格式
                    }

                    if (int.TryParse(versionPart, out var major) && major >= 5)
                    {
                        return $"{major}.0";
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
    }
}
