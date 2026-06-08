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
            Action<string>? onError = null)
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
                    log("Bypass anti_RB...");
                    if (!FastbootFlashSession.TryFlashAntiPartition(serial, romRoot))
                        ok = false;
                }

                if (!ok)
                    return false;

                foreach (PartitionUpdate partitionUpdate in payload.manifest.Partitions)
                {
                    log("Extracting " + partitionUpdate.PartitionName);
                    Payload.PayloadExtractionException? extractError = payload.extract(
                        partitionUpdate.PartitionName, tmpDir, false, false);

                    if (extractError != null)
                    {
                        log("ERROR: " + extractError.Message);
                        onError?.Invoke(extractError.Message);
                        return false;
                    }

                    log("Extracted " + partitionUpdate.PartitionName);
                }

                foreach (PartitionUpdate partitionUpdate in payload.manifest.Partitions)
                {
                    lock (FastbootGate.Sync)
                    {
                        string flashCmd = "flash \"" + partitionUpdate.PartitionName + "\" \""
                            + tmpDir + "\\" + partitionUpdate.PartitionName + ".img\"";
                        if (!Fastboot.Run(serial, flashCmd, log))
                        {
                            onError?.Invoke("Flash failed: " + partitionUpdate.PartitionName);
                            return false;
                        }
                    }
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
