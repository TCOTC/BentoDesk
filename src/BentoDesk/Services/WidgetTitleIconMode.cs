namespace BentoDesk.Services;

using BentoDesk.Models;

public enum WidgetTitleIconMode
{
    FilledMono,
    LineMono,
    Color,
    Hidden,
    TextLabel
}

public static class WidgetTitleIconModeNames
{
    public const string FilledMono = nameof(WidgetTitleIconMode.FilledMono);
    public const string LineMono = nameof(WidgetTitleIconMode.LineMono);
    public const string Color = nameof(WidgetTitleIconMode.Color);
    public const string Hidden = nameof(WidgetTitleIconMode.Hidden);
    public const string TextLabel = nameof(WidgetTitleIconMode.TextLabel);

    public static WidgetTitleIconMode NormalizeMode(string? value)
    {
        return Enum.TryParse(value, ignoreCase: true, out WidgetTitleIconMode mode)
            ? mode
            : WidgetTitleIconMode.Color;
    }

    public static string NormalizeSettingValue(string? value)
    {
        return ToSettingValue(NormalizeMode(value));
    }

    public static string ToSettingValue(WidgetTitleIconMode mode)
    {
        return mode switch
        {
            WidgetTitleIconMode.FilledMono => FilledMono,
            WidgetTitleIconMode.LineMono => LineMono,
            WidgetTitleIconMode.Color => Color,
            WidgetTitleIconMode.Hidden => Hidden,
            WidgetTitleIconMode.TextLabel => TextLabel,
            _ => Color
        };
    }
}

public enum WidgetTitleIconKind
{
    Default,
    ManagedStorage,
    MappedFolder,
    Music,
    Tags,
    Search,
    SystemMonitor
}

public static class WidgetTitleIconKindNames
{
    public const string Default = nameof(WidgetTitleIconKind.Default);
    public const string ManagedStorage = nameof(WidgetTitleIconKind.ManagedStorage);
    public const string MappedFolder = nameof(WidgetTitleIconKind.MappedFolder);
    public const string Music = nameof(WidgetTitleIconKind.Music);
    public const string Tags = nameof(WidgetTitleIconKind.Tags);
    public const string Search = nameof(WidgetTitleIconKind.Search);
    public const string SystemMonitor = nameof(WidgetTitleIconKind.SystemMonitor);

    public static WidgetTitleIconKind NormalizeKind(string? value)
    {
        return Enum.TryParse(value, ignoreCase: true, out WidgetTitleIconKind kind)
            ? kind
            : WidgetTitleIconKind.Default;
    }

    public static string ToSettingValue(WidgetTitleIconKind kind)
    {
        return kind switch
        {
            WidgetTitleIconKind.ManagedStorage => ManagedStorage,
            WidgetTitleIconKind.MappedFolder => MappedFolder,
            WidgetTitleIconKind.Music => Music,
            WidgetTitleIconKind.Tags => Tags,
            WidgetTitleIconKind.Search => Search,
            WidgetTitleIconKind.SystemMonitor => SystemMonitor,
            _ => Default
        };
    }

    public static string FromFileWidget(bool followsDefaultStoragePath)
    {
        return followsDefaultStoragePath ? ManagedStorage : MappedFolder;
    }

    public static string FromWidgetKind(WidgetKind kind)
    {
        return kind switch
        {
            WidgetKind.File => ManagedStorage,
            WidgetKind.Music => Music,
            WidgetKind.Tags => Tags,
            WidgetKind.Search => Search,
            WidgetKind.SystemMonitor => SystemMonitor,
            _ => Default
        };
    }

    public static string FromLegacyGlyph(string? glyph)
    {
        return glyph switch
        {
            "\uE8B7" => ManagedStorage,
            "\uE71B" => MappedFolder,
            "\uEC4F" => Music,
            "\uE8EC" => Tags,
            "\uE721" => Search,
            "\uE9D9" => SystemMonitor,
            _ => Default
        };
    }

    public static string GetLocalizationKey(WidgetTitleIconKind kind)
    {
        return kind switch
        {
            WidgetTitleIconKind.ManagedStorage => "WidgetTitleIcon.Label.ManagedStorage",
            WidgetTitleIconKind.MappedFolder => "WidgetTitleIcon.Label.MappedFolder",
            WidgetTitleIconKind.Music => "WidgetTitleIcon.Label.Music",
            WidgetTitleIconKind.Tags => "WidgetTitleIcon.Label.Tags",
            WidgetTitleIconKind.Search => "WidgetTitleIcon.Label.Search",
            WidgetTitleIconKind.SystemMonitor => "WidgetTitleIcon.Label.SystemMonitor",
            _ => "WidgetTitleIcon.Label.Default"
        };
    }

    public static string GetColorAssetName(WidgetTitleIconKind kind)
    {
        return kind switch
        {
            WidgetTitleIconKind.ManagedStorage => "managed-storage",
            WidgetTitleIconKind.MappedFolder => "mapped-folder",
            WidgetTitleIconKind.Music => "music",
            WidgetTitleIconKind.Tags => "tags",
            WidgetTitleIconKind.Search => "search",
            WidgetTitleIconKind.SystemMonitor => "system-monitor",
            _ => "default"
        };
    }
}
