namespace Xiaomi_Flash.Ui
{
    internal sealed class DeviceHardwareSnapshot
    {
        public string? Serial { get; set; }
        public string? Port { get; set; }
        public string? Codename { get; set; }
        public string? Model { get; set; }
        public string? Storage { get; set; }
        public string? CpuId { get; set; }
        public string? HwRevision { get; set; }
        public string? SecureBoot { get; set; }
        public string? Bootloader { get; set; }
        public string? AntiRollback { get; set; }
        public string? FirmwareVersion { get; set; }
        public string? BootSlot { get; set; }

        public bool HasCoreInfo =>
            !string.IsNullOrWhiteSpace(Codename)
            || !string.IsNullOrWhiteSpace(Model)
            || !string.IsNullOrWhiteSpace(Storage);
    }
}
