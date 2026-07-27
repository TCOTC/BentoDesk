using BentoDesk.Models;

namespace BentoDesk.Services.WidgetKinds;

internal enum WidgetWindowHostKind
{
    /// <summary>现有 WidgetWindow + WidgetViewModel 路径。</summary>
    LegacyFileWindow,

    /// <summary>ContentWidgetWindow + IWidgetContent 路径。</summary>
    DetachedContentWindow
}

internal sealed record WidgetCompactKindPolicy(
    bool CollapseToTitleBarWidth,
    double? SmartModeWidth,
    bool UsesSmartDetailHeight);

/// <summary>
/// Per-kind platform policy for host routing, sizing, and session availability.
/// </summary>
internal interface IWidgetKindHandler
{
    WidgetKind WidgetKind { get; }

    WidgetWindowHostKind HostKind { get; }

    bool SupportsMultiInstance { get; }

    (double Width, double Height) GetDefaultSize(AppSettings settings);

    string? DefaultTitleLocalizationKey { get; }

    string? SettingsMenuTextKey { get; }

    string? Glyph { get; }

    WidgetCompactKindPolicy CompactPolicy { get; }

    bool IsAvailableForSession(AppSettings settings);
}
