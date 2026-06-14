using System;
using System.Collections.Generic;
using System.IO;
using Xiaomi_Flash;

namespace Xiaomi_Flash.Ui
{
    /// <summary>
    /// Bypass anti-rollback compartido entre flash por script (.bat) y payload.bin.
    /// </summary>
    internal static class AntiRollbackBypass
    {
        internal readonly struct ApplyResult
        {
            public ApplyResult(bool attempted, bool flashSucceeded, bool shouldProceed)
            {
                Attempted = attempted;
                FlashSucceeded = flashSucceeded;
                ShouldProceed = shouldProceed;
            }

            public bool Attempted { get; }
            public bool FlashSucceeded { get; }
            public bool ShouldProceed { get; }
        }

        public static ApplyResult Apply(
            string serial,
            string romRoot,
            bool enabled,
            bool continueOnFail,
            Action<string> logLine = null)
        {
            if (!enabled)
                return new ApplyResult(false, false, true);

            Action<string> log = logLine ?? FastbootDeviceService.AppendTerminalLog;
            log("Bypass anti_RB...");

            bool flashOk = TryFlashAntiPartition(serial, romRoot, log);
            if (flashOk)
                return new ApplyResult(true, true, true);

            if (continueOnFail)
            {
                LogContinueAfterFailure(log);
                return new ApplyResult(true, false, true);
            }

            return new ApplyResult(true, false, false);
        }

        public static bool TryFlashAntiPartition(string serial, string romRoot, Action<string> logLine = null)
        {
            Action<string> log = logLine ?? FastbootDeviceService.AppendTerminalLog;

            string antiImage = RomFlashScanner.FindAntiImage(romRoot);
            if (string.IsNullOrEmpty(antiImage) || !File.Exists(antiImage))
            {
                LogError(log, "Bypass anti_RB: anti image not found in ROM");
                return false;
            }

            string cmd = "flash \"anti\" \"" + antiImage + "\"";
            try
            {
                lock (FastbootGate.Sync)
                {
                    return Fastboot.Run(serial, cmd, log, Fastboot.GetRebootTimeoutMs(cmd));
                }
            }
            catch (Exception ex)
            {
                LogError(log, ex.Message);
                return false;
            }
        }

        public static List<FlashScriptStep> FilterScriptSteps(List<FlashScriptStep> steps)
        {
            List<FlashScriptStep> filtered = new List<FlashScriptStep>();
            foreach (FlashScriptStep step in steps)
            {
                if (step.Kind == FlashScriptStepKind.Flash
                    && step.Partition.Equals("anti", StringComparison.OrdinalIgnoreCase))
                    continue;
                filtered.Add(step);
            }
            return filtered;
        }

        static void LogContinueAfterFailure(Action<string> log)
        {
            const string message = "Bypass anti_RB failed — continuing flash (user override)";
            if (log == FastbootDeviceService.AppendTerminalLog)
                TerminalLog.Error(message);
            else
                log(message);
        }

        static void LogError(Action<string> log, string message)
        {
            if (log == FastbootDeviceService.AppendTerminalLog)
                TerminalLog.Error(message);
            else
                log("ERROR: " + message);
        }
    }
}
