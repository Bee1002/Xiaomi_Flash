#nullable disable
using System;

namespace Xiaomi_Flash.Ui
{
    /// <summary>
    /// API pública del flasher v2: detección fastboot, comandos y flash payload.
    /// El estado compartido y los handlers legacy viven en Legacy/FastbootUI.cs.
    /// </summary>
    public static class FastbootDeviceService
    {
        public const string PayloadTmp = FastbootUI.PAYLOAD_TMP;

        public static void Initialize() => FastbootUI.init();

        public static bool HasFastbootDevice() => FastbootUI.HasFastbootDevice();

        public static bool IsDeviceFastbootd(string serial) => FastbootUI.IsDeviceFastbootd(serial);

        public static void AppendTerminalLog(string line) => FastbootUI.AppendTerminalLog(line);

        public static void RefreshConnectionPanel() => FastbootUI.RefreshConnectionPanel();

        public static bool EnsureFastbootDevice(out string serial) =>
            FastbootUI.EnsureFastbootDevice(out serial);

        public static void RunStepCommand(
            string serial,
            string cmd,
            int stepCount,
            bool showDialogOnDone,
            bool skipVarRefresh,
            Action onComplete = null) =>
            FastbootUI.RunStepCommand(serial, cmd, stepCount, showDialogOnDone, skipVarRefresh, onComplete);

        public static void RunPayloadFlash(
            string path,
            Action<bool> onComplete,
            bool bypassAntiRb = false,
            string romRoot = null) =>
            FastbootUI.RunPayloadFlash(path, onComplete, bypassAntiRb, romRoot);
    }
}
