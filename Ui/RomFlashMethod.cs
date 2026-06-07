namespace Xiaomi_Flash.Ui
{
    internal enum RomFlashMethod
    {
        ScriptFlashAll,
        ScriptFlashAllLock,
        ScriptFlashAllExceptStorage,
        ImagesOnly,
        Payload
    }

    internal sealed class RomFlashMethodOption
    {
        public RomFlashMethod Method { get; set; }
        public string ScriptFileName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
