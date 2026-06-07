using System;
using System.Collections.Generic;
using System.IO;

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
        static readonly (string file, RomFlashMethod method, string title, string desc)[] KnownScripts =
        {
            ("flash_all.bat", RomFlashMethod.ScriptFlashAll,
                "flash_all.bat",
                "Instalación limpia (borra datos)"),
            ("flash_all_lock.bat", RomFlashMethod.ScriptFlashAllLock,
                "flash_all_lock.bat",
                "Instalación limpia + bloquear bootloader"),
            ("flash_all_except_storage.bat", RomFlashMethod.ScriptFlashAllExceptStorage,
                "flash_all_except_storage.bat",
                "Actualización sin borrar almacenamiento interno")
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
                    Description = "ROM en formato payload (OTA)"
                });
                return info;
            }

            foreach ((string file, RomFlashMethod method, string title, string desc) in KnownScripts)
            {
                if (File.Exists(Path.Combine(romRoot, file)))
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
            if (Directory.Exists(imagesDir) && Directory.GetFiles(imagesDir, "*.img").Length > 0)
            {
                info.Methods.Add(new RomFlashMethodOption
                {
                    Method = RomFlashMethod.ImagesOnly,
                    ScriptFileName = "",
                    DisplayName = "Solo imágenes (*.img)",
                    Description = "Modo manual: todas las .img en images\\ (sin script)"
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
                return dir;

            string parent = Directory.GetParent(dir)?.FullName;
            if (parent != null && HasRomMarkers(parent))
                return parent;

            if (string.Equals(Path.GetFileName(dir), "images", StringComparison.OrdinalIgnoreCase)
                && parent != null)
                return parent;

            return dir;
        }

        public static string GetImagesDir(string romRoot)
        {
            string imagesDir = Path.Combine(romRoot, "images");
            return Directory.Exists(imagesDir) ? imagesDir : romRoot;
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
