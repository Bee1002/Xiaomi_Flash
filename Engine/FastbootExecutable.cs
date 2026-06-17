using System;
using System.Diagnostics;
using System.IO;

namespace Xiaomi_Flash
{
    /// <summary>
    /// Resuelve qué fastboot.exe usar. Xiaomi super.img sparse falla con algunas versiones de Google fastboot.
    /// Prioridad: ROM tools → bundled junto al .exe.
    /// </summary>
    internal static class FastbootExecutable
    {
        static string? overrideRomRoot;

        public static void SetRomRoot(string? romRoot)
        {
            overrideRomRoot = string.IsNullOrWhiteSpace(romRoot) ? null : romRoot;
        }

        public static string ResolvePath()
        {
            if (!string.IsNullOrWhiteSpace(overrideRomRoot))
            {
                foreach (string candidate in GetRomCandidates(overrideRomRoot))
                {
                    if (File.Exists(candidate))
                        return candidate;
                }
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fastboot.exe");
        }

        public static string GetVersionText(string? path = null)
        {
            path ??= ResolvePath();
            if (!File.Exists(path))
                return "not found";

            try
            {
                using Process process = new Process();
                process.StartInfo.FileName = path;
                process.StartInfo.Arguments = "--version";
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.Start();
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit(5000);
                string text = (stdout + stderr).Trim();
                if (text.Length == 0)
                    return "unknown";
                int lineBreak = text.IndexOf('\n');
                return lineBreak >= 0 ? text.Substring(0, lineBreak).Trim() : text;
            }
            catch (Exception ex)
            {
                return "error: " + ex.Message;
            }
        }

        static string[] GetRomCandidates(string romRoot)
        {
            return new[]
            {
                Path.Combine(romRoot, "fastboot.exe"),
                Path.Combine(romRoot, "tools", "fastboot.exe"),
                Path.Combine(romRoot, "bin", "fastboot.exe"),
                Path.Combine(romRoot, "platform-tools", "fastboot.exe"),
                Path.Combine(romRoot, "images", "fastboot.exe")
            };
        }
    }
}
