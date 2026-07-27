namespace BentoDesk.Services;

public enum WidgetChromeMode
{
    Standard,
    Compact,
    Overlay,
    Hidden
}

public static class WidgetChromeModeNames
{
    public const string MetadataKey = "ChromeMode";
}
