using Newtonsoft.Json;
using System;
using System.IO;

namespace AMN.ManifestGen
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            //args = ["-i", @"D:\Code\DemoPlugin\bin\Debug\net9.0\Native_DemoPlugin.dll",
            //    "-o", @"D:\Code\DemoPlugin\bin\Debug\net9.0\Native_DemoPlugin.json",
            //    "-t", "net9.0",
            //    "-c"];

            string inputFilePath = null;
            string outputFilePath = null;
            string targetFramework = null;
            bool isTargetNetFramework = true;
            bool cleanOutputRequired = false;
            bool ignoreDependencyVersion = false;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].ToLower() == "-i" && i < args.Length - 1)
                {
                    inputFilePath = args[i + 1];
                }
                else if (args[i].ToLower() == "-o" && i < args.Length - 1)
                {
                    outputFilePath = args[i + 1];
                }
                else if (args[i].ToLower() == "-t" && i < args.Length - 1)
                {
                    targetFramework = args[i + 1];
                    isTargetNetFramework = CheckIsTargetNetFramework(targetFramework);
                }
                else if (args[i].ToLower() == "-c")
                {
                    cleanOutputRequired = true;
                }
                else if (args[i] == "--ignoreDependencyVersion")
                {
                    ignoreDependencyVersion = true;
                }
            }

            if (string.IsNullOrEmpty(inputFilePath) || string.IsNullOrEmpty(outputFilePath))
            {
                Console.Error.WriteLine("未指定输入文件或输出文件路径");
                return -1;
            }
            if (!File.Exists(inputFilePath))
            {
                Console.Error.WriteLine("输入文件路径不存在");
                return -1;
            }

            try
            {
                var info = ManifestReader.ReadManifest(inputFilePath, targetFramework, isTargetNetFramework);

                File.WriteAllText(outputFilePath, JsonConvert.SerializeObject(info, Formatting.Indented));
                Console.WriteLine($"Manifest 已生成成功，Json文件写出到 {outputFilePath}");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("生成 Manifest 时发生错误\n" + e.ToString());
                return -1;
            }

            if (cleanOutputRequired)
            {
                return OutputCleaner.CleanOutput(inputFilePath, targetFramework, ignoreDependencyVersion);
            }

            return 0;
        }

        private static bool CheckIsTargetNetFramework(string targetFramework)
        {
            if (string.IsNullOrWhiteSpace(targetFramework))
            {
                throw new InvalidDataException("无效的生成目标");
            }

            Console.WriteLine($"正在处理生成目标: {targetFramework}");
            int identityPos = targetFramework.IndexOf("net", StringComparison.OrdinalIgnoreCase);
            int dotPos = targetFramework.IndexOf('.', identityPos + 3);
            string possibleNetVersion = targetFramework.Substring(identityPos + 3, dotPos >= 0 ? dotPos - 3 : targetFramework.Length - identityPos - 3);

            if (int.TryParse(possibleNetVersion, out int netVersion))
            {
                return netVersion >= 40;
            }
            else
            {
                throw new InvalidDataException($"无效的.Net版本: {possibleNetVersion}");
            }
        }
    }
}
