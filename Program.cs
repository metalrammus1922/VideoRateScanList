using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace VideoRateScanList
{
    class Program
    {
        private static void Main(string[] args)
        {
            string outputDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            Console.WriteLine($"输出目录: {outputDir}");
            Console.WriteLine();

            // 获取所有固定硬盘分区
            DriveInfo[] drives = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                .ToArray();

            Console.WriteLine($"检测到 {drives.Length} 个固定硬盘分区:");
            foreach (var drive in drives)
            {
                Console.WriteLine($"  {drive.Name} - 可用空间: {drive.AvailableFreeSpace / 1024 / 1024 / 1024} GB");
            }
            Console.WriteLine();

            Console.Write("请输入检索深度（直接回车表示全检索）: ");
            string depthInput = Console.ReadLine();
            int? maxDepth = string.IsNullOrWhiteSpace(depthInput) ? null : int.Parse(depthInput);
            Console.WriteLine();

            foreach (var drive in drives)
            {
                string driveLetter = drive.Name.TrimEnd('\\');
                Console.WriteLine($"========================================");
                Console.WriteLine($"开始扫描 {driveLetter} 盘...");
                Console.WriteLine($"========================================");

                List<string> rateFiles = ScanRateVideoFiles(drive.Name, maxDepth);

                if (rateFiles.Count > 0)
                {
                    Console.WriteLine($"找到 {rateFiles.Count} 个以rate开头的视频文件/文件夹：");

                    var sortedFiles = SortRateFiles(rateFiles);

                    string htmlFilePath = GenerateHtmlReport(sortedFiles, driveLetter, outputDir);

                    Console.WriteLine($"HTML报告已生成: {htmlFilePath}");
                    Console.WriteLine($"文件按照rate SSS到rate A的顺序排列");

                    Console.WriteLine("\n文件列表：");
                    for (int i = 0; i < sortedFiles.Count; i++)
                    {
                        string displayName = Path.GetFileName(sortedFiles[i]);
                        if (!File.Exists(sortedFiles[i]))
                        {
                            displayName += "\\ (文件夹)";
                        }
                        Console.WriteLine($"{i + 1}. {displayName}");
                    }
                }
                else
                {
                    Console.WriteLine($"   {driveLetter} 盘未找到以rate开头的视频文件/文件夹");
                }

                Console.WriteLine();
            }

            Console.WriteLine("所有盘扫描完成！");
            Console.WriteLine($"报告已保存到: {outputDir}");
            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
        }

        /// <summary>
        /// 扫描指定目录下所有视频文件，查找以rate开头的文件
        /// </summary>
        private static List<string> ScanRateVideoFiles(string rootPath, int? maxDepth)
        {
            List<string> rateFiles = new List<string>();

            try
            {
                ScanDirectoryRecursively(rootPath, rateFiles, currentDepth: 0, maxDepth: maxDepth);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"扫描过程中出现错误: {ex.Message}");
            }

            return rateFiles;
        }

        /// <summary>
        /// 递归扫描目录，安全处理权限问题
        /// </summary>
        private static void ScanDirectoryRecursively(string directoryPath, List<string> rateFiles, int currentDepth, int? maxDepth)
        {
            try
            {
                string[] videoExtensions = { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".m4v", ".3gp", ".mpeg", ".mpg" };

                bool isRateFolderAtMaxDepth = false;

                // 检查当前文件夹名是否以rate开头
                string directoryName = Path.GetFileName(directoryPath);
                if (directoryName.StartsWith("rate", StringComparison.OrdinalIgnoreCase) && IsValidRateFormat(directoryName))
                {
                    string[] files = Directory.GetFiles(directoryPath);
                    bool hasVideoFiles = files.Any(file =>
                    {
                        try
                        {
                            string extension = Path.GetExtension(file).ToLowerInvariant();
                            return videoExtensions.Contains(extension);
                        }
                        catch
                        {
                            return false;
                        }
                    });

                    if (hasVideoFiles)
                    {
                        rateFiles.Add(directoryPath);
                        Console.WriteLine($"  找到rate文件夹: {directoryName}");
                        // 如果处于最大深度，标记为需要继续扫描子目录
                        if (maxDepth.HasValue && currentDepth >= maxDepth.Value)
                        {
                            isRateFolderAtMaxDepth = true;
                        }
                        else
                        {
                            return;
                        }
                    }
                }

                // 扫描当前目录的文件
                string[] filesInDir = Directory.GetFiles(directoryPath);
                foreach (string file in filesInDir)
                {
                    try
                    {
                        string extension = Path.GetExtension(file).ToLowerInvariant();
                        if (videoExtensions.Contains(extension))
                        {
                            string fileName = Path.GetFileNameWithoutExtension(file);

                            if (fileName.StartsWith("rate", StringComparison.OrdinalIgnoreCase))
                            {
                                if (IsValidRateFormat(fileName))
                                {
                                    rateFiles.Add(file);
                                    Console.WriteLine($"  找到文件: {Path.GetFileName(file)}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  处理文件 {file} 时出错: {ex.Message}");
                    }
                }

                // 如果达到最大深度且不是rate文件夹的特殊情况，不再递归
                if (!isRateFolderAtMaxDepth && maxDepth.HasValue && currentDepth >= maxDepth.Value)
                {
                    return;
                }

                // 递归扫描子目录
                string[] subdirectories = Directory.GetDirectories(directoryPath);
                foreach (string subdirectory in subdirectories)
                {
                    try
                    {
                        if (ShouldSkipDirectory(subdirectory))
                        {
                            continue;
                        }
                        ScanDirectoryRecursively(subdirectory, rateFiles, currentDepth + 1, maxDepth);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // 静默跳过无权限目录
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  扫描目录 {subdirectory} 时出错: {ex.Message}");
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // 静默跳过无权限目录
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  扫描目录 {directoryPath} 时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 判断是否应该跳过某个目录
        /// </summary>
        private static bool ShouldSkipDirectory(string directoryPath)
        {
            string directoryName = Path.GetFileName(directoryPath).ToLowerInvariant();

            string[] skipDirectories = {
                "system volume information",
                "$recycle.bin",
                "recycler",
                "windows",
                "program files",
                "program files (x86)",
                "programdata",
                "appdata",
                "local",
                "locallow",
                "roaming",
                "temp",
                "tmp",
                "perflogs",
                "recovery",
                "intel",
                "msocache",
                "$windows.~ws",
                "$windows.~bt",
                "node_modules",
                ".git",
                "packages"
            };

            return skipDirectories.Contains(directoryName);
        }

        /// <summary>
        /// 按照rate SSS到rate A的顺序排序文件/文件夹
        /// </summary>
        private static List<string> SortRateFiles(List<string> files)
        {
            Dictionary<string, int> rateOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "SSS+", 1 },
                { "SSS", 2 },
                { "SS+", 3 },
                { "SS", 4 },
                { "S+", 5 },
                { "S", 6 },
                { "A+", 7 },
                { "A", 8 }
            };

            return files.OrderBy(item =>
            {
                string name = File.Exists(item) ? Path.GetFileNameWithoutExtension(item) : Path.GetFileName(item);
                string ratePart = name.Substring(4).Trim();
                string rateLevel = ExtractRateLevel(ratePart);

                if (rateOrder.ContainsKey(rateLevel))
                {
                    return rateOrder[rateLevel];
                }
                return 999;
            }).ThenBy(item => Path.GetFileName(item)).ToList();
        }

        /// <summary>
        /// 生成HTML报告文件
        /// </summary>
        private static string GenerateHtmlReport(List<string> files, string driveLetter, string outputDir)
        {
            // 使用盘符作为文件名标识
            string driveLabel = driveLetter.TrimEnd(':');
            string htmlFileName = $"rate_video_report_{driveLabel}.html";
            string htmlFilePath = Path.Combine(outputDir, htmlFileName);

            StringBuilder html = new StringBuilder();

            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang=\"zh-CN\">");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset=\"UTF-8\">");
            html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            html.AppendLine($"    <title>Rate视频文件报告 - {driveLetter}盘</title>");
            html.AppendLine("    <style>");
            html.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; background-color: #f5f5f5; }");
            html.AppendLine("        .container { max-width: 1200px; margin: 0 auto; background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }");
            html.AppendLine("        h1 { color: #333; text-align: center; border-bottom: 2px solid #007acc; padding-bottom: 10px; }");
            html.AppendLine("        .summary { background: #e7f3ff; padding: 15px; border-radius: 5px; margin-bottom: 20px; }");
            html.AppendLine("        table { width: 100%; border-collapse: collapse; margin-top: 10px; }");
            html.AppendLine("        th, td { border: 1px solid #ddd; padding: 12px; text-align: left; }");
            html.AppendLine("        th { background-color: #007acc; color: white; font-weight: bold; }");
            html.AppendLine("        tr:nth-child(even) { background-color: #f9f9f9; }");
            html.AppendLine("        tr:hover { background-color: #f0f8ff; }");
            html.AppendLine("        .rate-sss { background-color: #ffeb3b; font-weight: bold; }");
            html.AppendLine("        .rate-ss { background-color: #4caf50; color: white; font-weight: bold; }");
            html.AppendLine("        .rate-s { background-color: #2196f3; color: white; font-weight: bold; }");
            html.AppendLine("        .rate-a { background-color: #9c27b0; color: white; font-weight: bold; }");
            html.AppendLine("        .timestamp { color: #666; font-size: 12px; text-align: right; margin-top: 20px; }");
            html.AppendLine("        .folder-badge { background-color: #ff9800; color: white; padding: 2px 6px; border-radius: 3px; font-size: 11px; margin-left: 6px; }");
            html.AppendLine("    </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("    <div class=\"container\">");
            html.AppendLine($"        <h1>Rate视频文件扫描报告 - {driveLetter}盘</h1>");
            html.AppendLine("        <div class=\"summary\">");
            html.AppendLine($"            <strong>扫描时间:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}<br>");
            html.AppendLine($"            <strong>扫描目录:</strong> {driveLetter}\\\\<br>");
            html.AppendLine($"            <strong>找到文件:</strong> {files.Count} 个");
            html.AppendLine("        </div>");
            html.AppendLine("        <table>");
            html.AppendLine("            <thead>");
            html.AppendLine("                <tr>");
            html.AppendLine("                    <th width=\"80px\">序号</th>");
            html.AppendLine("                    <th>名称</th>");
            html.AppendLine("                    <th width=\"120px\">Rate等级</th>");
            html.AppendLine("                </tr>");
            html.AppendLine("            </thead>");
            html.AppendLine("            <tbody>");

            for (int i = 0; i < files.Count; i++)
            {
                string displayName;
                string rateLevel;
                bool isFolder;

                if (File.Exists(files[i]))
                {
                    isFolder = false;
                    displayName = Path.GetFileName(files[i]);
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(files[i]);
                    rateLevel = ExtractRateLevel(fileNameWithoutExt.Substring(4).Trim());
                }
                else
                {
                    isFolder = true;
                    displayName = Path.GetFileName(files[i]);
                    string folderName = Path.GetFileName(files[i]);
                    rateLevel = ExtractRateLevel(folderName.Substring(4).Trim());
                }

                string rateClass = GetRateCssClass(rateLevel);
                string nameDisplay;
                if (isFolder)
                {
                    nameDisplay = $"{displayName}<span class=\"folder-badge\">文件夹</span>";
                }
                else
                {
                    nameDisplay = displayName;
                }

                html.AppendLine("                <tr>");
                html.AppendLine($"                    <td>{i + 1}</td>");
                html.AppendLine($"                    <td>{nameDisplay}</td>");
                html.AppendLine($"                    <td class=\"{rateClass}\">{rateLevel}</td>");
                html.AppendLine("                </tr>");
            }

            html.AppendLine("            </tbody>");
            html.AppendLine("        </table>");
            html.AppendLine($"        <div class=\"timestamp\">生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</div>");
            html.AppendLine("    </div>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            File.WriteAllText(htmlFilePath, html.ToString(), Encoding.UTF8);

            return htmlFilePath;
        }

        /// <summary>
        /// 根据rate等级获取CSS类名
        /// </summary>
        private static string GetRateCssClass(string rateLevel)
        {
            switch (rateLevel.ToUpper())
            {
                case "SSS+":
                    return "rate-sss-plus";
                case "SSS":
                    return "rate-sss";
                case "SS+":
                    return "rate-ss-plus";
                case "SS":
                    return "rate-ss";
                case "S+":
                    return "rate-s-plus";
                case "S":
                    return "rate-s";
                case "A+":
                    return "rate-a-plus";
                case "A":
                    return "rate-a";
                default:
                    return "";
            }
        }

        /// <summary>
        /// 验证文件名是否符合rate格式要求
        /// </summary>
        private static bool IsValidRateFormat(string fileName)
        {
            string remaining = fileName.Substring(4).Trim();
            return !string.IsNullOrWhiteSpace(remaining);
        }

        /// <summary>
        /// 从rate部分提取rate等级
        /// </summary>
        private static string ExtractRateLevel(string ratePart)
        {
            string[] validPatterns = { "SSS+", "SSS", "SS+", "SS", "S+", "S", "A+", "A" };

            foreach (string pattern in validPatterns)
            {
                if (ratePart.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return pattern;
                }
            }

            return ratePart.Split(' ')[0];
        }
    }
}
