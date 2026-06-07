namespace Xiaomi_Flash.Ui
{
    internal enum FlashScriptStepKind
    {
        Flash,
        Erase,
        Reboot,
        SetActive,
        OemLock
    }

    internal sealed class FlashScriptStep
    {
        public FlashScriptStepKind Kind { get; set; }
        public string DisplayName { get; set; } = "";
        public string Partition { get; set; } = "";
        public string ImagePath { get; set; } = "";
        public string ExtraArgs { get; set; } = "";
        public string RebootTarget { get; set; } = "";
        public string ActiveSlot { get; set; } = "";
    }
}
