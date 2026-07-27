using BentoDesk.Models;

namespace BentoDesk.Services.WidgetKinds;

internal sealed class FileWidgetKindHandler : IWidgetKindHandler
{
    public static FileWidgetKindHandler Instance { get; } = new();

    public WidgetKind WidgetKind => WidgetKind.File;

    public WidgetWindowHostKind HostKind => WidgetWindowHostKind.LegacyFileWindow;

    public bool SupportsMultiInstance => true;

    public string? DefaultTitleLocalizationKey => null;

    public string? SettingsMenuTextKey => null;

    public string? Glyph => "\uE8A5";

    public WidgetCompactKindPolicy CompactPolicy { get; } = new(
        CollapseToTitleBarWidth: true,
        SmartModeWidth: null,
        UsesSmartDetailHeight: false);

    public (double Width, double Height) GetDefaultSize(AppSettings settings)
    {
        return (
            Math.Max(settings.DefaultWidgetWidth, SettingsService.MinWidgetWidth),
            Math.Max(settings.DefaultWidgetHeight, SettingsService.MinWidgetHeight));
    }

    public bool IsAvailableForSession(AppSettings settings) => true;
}
