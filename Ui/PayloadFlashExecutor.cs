using System;
using System.IO;
using ChromeosUpdateEngine;
using Xiaomi_Flash;

namespace Xiaomi_Flash.Ui
{
    /// <summary>
    /// Extrae particiones de un payload y las flashea vía fastboot (motor compartido v2 + legacy).
    /// </summary>
    internal static class PayloadFlashExecutor
    {
        public static bool Run(
            string serial,
            Payload payload,
            string tmpDir,
            Action<string> log,
            bool bypassAntiRb = false,
            string? romRoot = null,
            bool continueOnAntiRbFail = false,
            Action<string>? onError = null,
            Action<string, string>? onPartitionProgress = null)
        {
            if (string.IsNullOrWhiteSpace(serial) || payload == null)
                return false;

            Directory.CreateDirectory(tmpDir);
            bool ok = true;

            FastbootGate.EnterCritical();
            try
            {
                if (bypassAntiRb && !string.IsNullOrEmpty(romRoot))
                {
                    AntiRollbackBypass.ApplyResult antiResult = AntiRollbackBypass.Apply(
                        serial, romRoot, true, continueOnAntiRbFail, log);
                    if (!antiResult.ShouldProceed)
                        ok = false;
                }

                if (!ok)
                    return false;

                foreach (PartitionUpdate partitionUpdate in payload.manifest.Partitions)
                {
                    onPartitionProgress?.Invoke(partitionUpdate.PartitionName, "extract");
                    log("Extracting " + partitionUpdate.PartitionName);
                    Payload.PayloadExtractionException? extractError = payload.extract(
                        partitionUpdate.PartitionName, tmpDir, false, false);

                    if (extractError != null)
                    {
                        log("ERROR: " + extractError.Message);
                        onError?.Invoke(extractError.Message);
                        onPartitionProgress?.Invoke(partitionUpdate.PartitionName, "failed");
                        return false;
                    }

                    log("Extracted " + partitionUpdate.PartitionName);
                }

                foreach (PartitionUpdate partitionUpdate in payload.manifest.Partitions)
                {
                    onPartitionProgress?.Invoke(partitionUpdate.PartitionName, "flash");
                    lock (FastbootGate.Sync)
                    {
                        string flashCmd = "flash \"" + partitionUpdate.PartitionName + "\" \""
                            + tmpDir + "\\" + partitionUpdate.PartitionName + ".img\"";
                        if (!Fastboot.Run(serial, flashCmd, log))
                        {
                            onError?.Invoke("Flash failed: " + partitionUpdate.PartitionName);
                            onPartitionProgress?.Invoke(partitionUpdate.PartitionName, "failed");
                            return false;
                        }
                    }

                    onPartitionProgress?.Invoke(partitionUpdate.PartitionName, "ok");
                }

                return true;
            }
            finally
            {
                FastbootGate.ExitCritical();
            }
        }
    }
}
