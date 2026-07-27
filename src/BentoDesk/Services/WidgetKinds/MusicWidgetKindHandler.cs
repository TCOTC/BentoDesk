using BentoDesk.Models;

namespace BentoDesk.Services.WidgetKinds;

internal sealed class MusicWidgetKindHandler : IWidgetKindHandler
{
    public static MusicWidgetKindHandler Instance { get; } = new();

    public WidgetKind WidgetKind => WidgetKind.Music;

    public WidgetWindowHostKind HostKind => WidgetWindowHostKind.DetachedContentWindow;

    public bool SupportsMultiInstance => false;

    public string? DefaultTitleLocalizationKey => "Music.Title";

    public string? SettingsMenuTextKey => "Music.OpenSettings";

    public string? Glyph => "\uEC4F";

    public WidgetCompactKindPolicy CompactPolicy { get; } = new(
        CollapseToTitleBarWidth: false,
        SmartModeWidth: WidgetCompactBoundsCalculator.SmartMediaWidth,
        UsesSmartDetailHeight: true);

    public (double Width, double Height) GetDefaultSize(AppSettings settings) => (380, 190);

    public bool IsAvailableForSession(AppSettings settings) => settings.MusicWidgetEnabled;
}
