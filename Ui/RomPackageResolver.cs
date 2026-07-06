using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Xiaomi_Flash.Ui
{
    internal sealed class RomPackageInfo
    {
        public string SelectedPath { get; set; } = "";
        public string RomRoot { get; set; } = "";
        public List<RomFlashMethodOption> Methods { get; } = new List<RomFlashMethodOption>();
    }

    internal static class RomPackageResolver
    {
        static readonly string[] FirmwareImagePatterns = { "*.img", "*.bin", "*.elf", "*.mbn" };

        static readonly (string file, RomFlashMethod method, string title, string desc)[] KnownScripts =
        {
            ("flash_all.bat", RomFlashMethod.ScriptFlashAll,
                "flash_all.bat",
                "Clean install (wipes user data)"),
            ("flash_all_lock.bat", RomFlashMethod.ScriptFlashAllLock,
                "flash_all_lock.bat",
                "Clean install + lock bootloader"),
            ("flash_all_except_storage.bat", RomFlashMethod.ScriptFlashAllExceptStorage,
                "flash_all_except_storage.bat",
                "Update without wiping internal storage")
        };

        public static RomPackageInfo Resolve(string selectedPath)
        {
            RomPackageInfo info = new RomPackageInfo { SelectedPath = selectedPath };
            string romRoot = FindRomRoot(selectedPath);
            info.RomRoot = romRoot;

            string payload = Path.Combine(romRoot, "payload.bin");
            if (File.Exists(payload))
            {
                info.Methods.Add(new RomFlashMethodOption
                {
                    Method = RomFlashMethod.Payload,
                    ScriptFileName = "payload.bin",
                    DisplayName = "payload.bin",
                    Description = "OTA-style ROM (payload.bin)"
                });
            }

            foreach ((string file, RomFlashMethod method, string title, string desc) in KnownScripts)
            {
                if (FindScriptPath(romRoot, file) != null)
                {
                    info.Methods.Add(new RomFlashMethodOption
                    {
                        Method = method,
                        ScriptFileName = file,
                        DisplayName = title,
                        Description = desc
                    });
                }
            }

            string imagesDir = GetImagesDir(romRoot);
            if (Directory.Exists(imagesDir) && CountFirmwareImagesInDir(imagesDir) > 0)
            {
                info.Methods.Add(new RomFlashMethodOption
                {
                    Method = RomFlashMethod.ImagesOnly,
                    ScriptFileName = "",
                    DisplayName = "Images only (*.img)",
                    Description = "Manual mode: all .img files in images\\ (no script)"
                });
            }

            return info;
        }

        public static string FindRomRoot(string selectedPath)
        {
            string dir = Path.GetFullPath(selectedPath);
            if (!Directory.Exists(dir))
                return dir;

            if (HasRomMarkers(dir))
            {
                string? better = FindBestRomPackage(dir, maxDepth: 2);
                if (better != null && !string.Equals(better, dir, StringComparison.OrdinalIgnoreCase))
                {
                    int directScore = ScoreRomPackage(dir);
                    int nestedScore = ScoreRomPackage(better);
                    if (nestedScore > directScore)
                        return better;
                }

                return dir;
            }

            string parent = Directory.GetParent(dir)?.FullName;
            if (parent != null && HasRomMarkers(parent))
                return parent;

            if (string.Equals(Path.GetFileName(dir), "images", StringComparison.OrdinalIgnoreCase)
                && parent != null)
                return parent;

            string? nested = FindBestRomPackage(dir, maxDepth: 4);
            if (nested != null)
                return nested;

            return dir;
        }

        public static string GetImagesDir(string romRoot, string? batDir = null)
        {
            string? best = null;
            int bestCount = -1;

            foreach (string candidate in GetImageSearchDirs(romRoot, batDir, null))
            {
                if (!Directory.Exists(candidate))
                    continue;

                int count = CountFirmwareImagesInDir(candidate);
                if (count > bestCount)
                {
                    bestCount = count;
                    best = candidate;
                }
            }

            if (best != null && bestCount > 0)
                return best;

            string standard = Path.Combine(romRoot, "images");
            if (Directory.Exists(standard))
                return standard;

            if (!string.IsNullOrWhiteSpace(batDir))
            {
                string batImages = Path.Combine(batDir, "images");
                if (Directory.Exists(batImages))
                    return batImages;
            }

            return Directory.Exists(standard) ? standard : (batDir ?? romRoot);
        }

        public static IEnumerable<string> GetImageSearchDirs(string romRoot, string? batDir, string? primaryImagesDir)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> dirs = new List<string>();

            void Add(string? path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return;

                string full = Path.GetFullPath(path);
                if (seen.Add(full))
                    dirs.Add(full);
            }

            void AddParentImages(string? baseDir)
            {
                if (string.IsNullOrWhiteSpace(baseDir))
                    return;

                string? parent = Directory.GetParent(baseDir)?.FullName;
                if (parent == null)
                    return;

                Add(Path.Combine(parent, "images"));
                Add(parent);
            }

            Add(primaryImagesDir);
            Add(Path.Combine(romRoot, "images"));
            Add(romRoot);
            AddParentImages(romRoot);

            if (!string.IsNullOrWhiteSpace(batDir))
            {
                Add(Path.Combine(batDir, "images"));
                Add(batDir);
                AddParentImages(batDir);
            }

            return dirs;
        }

        public static string? FindScriptPath(string romRoot, string scriptFileName)
        {
            string direct = Path.Combine(romRoot, scriptFileName);
            if (File.Exists(direct))
                return direct;

            try
            {
                foreach (string found in Directory.EnumerateFiles(romRoot, scriptFileName, SearchOption.AllDirectories))
                    return found;
            }
            catch
            {
            }

            return null;
        }

        public static int CountFirmwareImages(string romRoot, string? batDir = null)
        {
            return CountFirmwareImagesInDir(GetImagesDir(romRoot, batDir));
        }

        public static string DescribeRomLayout(string romRoot, string? batPath = null)
        {
            string? batDir = string.IsNullOrWhiteSpace(batPath)
                ? null
                : Path.GetDirectoryName(Path.GetFullPath(batPath));
            string imagesDir = GetImagesDir(romRoot, batDir);
            int imageCount = CountFirmwareImagesInDir(imagesDir);
            bool imagesExists = Directory.Exists(imagesDir);

            return "ROM root: " + romRoot
                + " | images: " + imagesDir
                + (imagesExists ? " (" + imageCount + " firmware file(s))" : " (missing)");
        }

        static string? FindBestRomPackage(string root, int maxDepth)
        {
            string? best = null;
            int bestScore = 0;
            ScanRomCandidates(root, 0, maxDepth, ref best, ref bestScore);
            return bestScore > 0 ? best : null;
        }

        static void ScanRomCandidates(string dir, int depth, int maxDepth, ref string? best, ref int bestScore)
        {
            int score = ScoreRomPackage(dir);
            if (score > bestScore)
            {
                bestScore = score;
                best = dir;
            }

            if (depth >= maxDepth)
                return;

            try
            {
                foreach (string sub in Directory.GetDirectories(dir))
                    ScanRomCandidates(sub, depth + 1, maxDepth, ref best, ref bestScore);
            }
            catch
            {
            }
        }

        static int ScoreRomPackage(string dir)
        {
            bool hasScript = KnownScripts.Any(entry => File.Exists(Path.Combine(dir, entry.file)));
            bool hasPayload = File.Exists(Path.Combine(dir, "payload.bin"));
            if (!hasScript && !hasPayload)
                return 0;

            int imageCount = CountFirmwareImagesInDir(Path.Combine(dir, "images"));
            if (imageCount == 0)
                imageCount = CountFirmwareImagesInDir(dir);

            if (hasScript || hasPayload)
            {
                if (imageCount > 0)
                    return 100 + imageCount;
                return 1;
            }

            return imageCount;
        }

        static int CountFirmwareImagesInDir(string dir)
        {
            if (!Directory.Exists(dir))
                return 0;

            int count = 0;
            foreach (string pattern in FirmwareImagePatterns)
            {
                try
                {
                    count += Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly).Length;
                }
                catch
                {
                }
            }

            return count;
        }

        static bool HasRomMarkers(string dir)
        {
            if (File.Exists(Path.Combine(dir, "payload.bin")))
                return true;

            foreach ((string file, _, _, _) in KnownScripts)
            {
                if (File.Exists(Path.Combine(dir, file)))
                    return true;
            }

            return false;
        }
    }
}
