using System;
using System.Collections.Generic;
using System.IO;

namespace Xiaomi_Flash.Ui
{
    internal enum RomFlashKind
    {
        None,
        Script,
        Images,
        Payload
    }

    internal sealed class RomFlashPlan
    {
        public RomFlashKind Kind { get; set; }
        public RomFlashMethod Method { get; set; }
        public string RomRoot { get; set; } = "";
        public string ScriptFileName { get; set; } = "";
        public string MethodDescription { get; set; } = "";
        public string? PayloadPath { get; set; }
        public List<FlashScriptStep> Steps { get; } = new List<FlashScriptStep>();
        public List<string> SkippedSteps { get; } = new List<string>();
    }

    internal static class RomFlashScanner
    {
        public static RomFlashPlan Scan(RomPackageInfo package, RomFlashMethodOption methodOption)
        {
            RomFlashPlan plan = new RomFlashPlan
            {
                RomRoot = package.RomRoot,
                Method = methodOption.Method,
                ScriptFileName = methodOption.ScriptFileName,
                MethodDescription = methodOption.Description
            };

            if (methodOption.Method == RomFlashMethod.Payload)
            {
                string payload = Path.Combine(package.RomRoot, "payload.bin");
                if (!File.Exists(payload))
                    return plan;

                plan.Kind = RomFlashKind.Payload;
                plan.PayloadPath = payload;
                plan.Steps.Add(new FlashScriptStep
                {
                    Kind = FlashScriptStepKind.Flash,
                    DisplayName = "payload.bin",
                    Partition = "payload.bin",
                    ImagePath = payload
                });
                return plan;
            }

            if (IsScriptMethod(methodOption.Method))
            {
                string batPath = Path.Combine(package.RomRoot, methodOption.ScriptFileName);
                if (File.Exists(batPath))
                {
                    List<string> skipped = new List<string>();
                    plan.Steps.AddRange(RomBatScriptParser.Parse(batPath, package.RomRoot, skipped));
                    plan.SkippedSteps.AddRange(skipped);
                    if (plan.Steps.Count > 0)
                        plan.Kind = RomFlashKind.Script;
                }
                return plan;
            }

            if (methodOption.Method == RomFlashMethod.ImagesOnly)
            {
                ScanImagesFolder(plan, RomPackageResolver.GetImagesDir(package.RomRoot));
                if (plan.Steps.Count > 0)
                    plan.Kind = RomFlashKind.Images;
            }

            return plan;
        }

        static bool IsScriptMethod(RomFlashMethod method)
        {
            return method == RomFlashMethod.ScriptFlashAll
                || method == RomFlashMethod.ScriptFlashAllLock
                || method == RomFlashMethod.ScriptFlashAllExceptStorage;
        }

        static void ScanImagesFolder(RomFlashPlan plan, string imagesDir)
        {
            string[] files = Directory.GetFiles(imagesDir, "*.img", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file);
                plan.Steps.Add(new FlashScriptStep
                {
                    Kind = FlashScriptStepKind.Flash,
                    DisplayName = name,
                    Partition = name,
                    ImagePath = file
                });
            }
        }

        public static string? FindVbmetaImage(string? romRoot)
        {
            if (string.IsNullOrWhiteSpace(romRoot))
                return null;

            string imagesDir = RomPackageResolver.GetImagesDir(romRoot);
            string direct = Path.Combine(imagesDir, "vbmeta.img");
            if (File.Exists(direct))
                return Path.GetFullPath(direct);

            string[] files = Directory.GetFiles(imagesDir, "vbmeta*.img", SearchOption.TopDirectoryOnly);
            if (files.Length > 0)
                return Path.GetFullPath(files[0]);

            foreach (string batName in new[] { "flash_all.bat", "flash_all_lock.bat", "flash_all_except_storage.bat" })
            {
                string batPath = Path.Combine(romRoot, batName);
                if (!File.Exists(batPath))
                    continue;

                foreach (FlashScriptStep step in RomBatScriptParser.Parse(batPath, romRoot))
                {
                    if (step.Kind == FlashScriptStepKind.Flash
                        && step.Partition.StartsWith("vbmeta", StringComparison.OrdinalIgnoreCase)
                        && File.Exists(step.ImagePath))
                        return step.ImagePath;
                }
            }

            return null;
        }

        public static string FindAntiImage(string romRoot)
        {
            string imagesDir = RomPackageResolver.GetImagesDir(romRoot);

            string[] files = Directory.GetFiles(imagesDir, "anti*.img", SearchOption.TopDirectoryOnly);
            if (files.Length > 0)
                return Path.GetFullPath(files[0]);

            string batPath = Path.Combine(romRoot, "flash_all.bat");
            if (!File.Exists(batPath))
                batPath = Path.Combine(romRoot, "flash_all_lock.bat");
            if (!File.Exists(batPath))
                return null;

            foreach (FlashScriptStep step in RomBatScriptParser.Parse(batPath, romRoot))
            {
                if (step.Kind == FlashScriptStepKind.Flash
                    && step.Partition.Equals("anti", StringComparison.OrdinalIgnoreCase)
                    && File.Exists(step.ImagePath))
                    return step.ImagePath;
            }

            return null;
        }
    }
}
