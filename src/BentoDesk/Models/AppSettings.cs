namespace BentoDesk.Models;

/// <summary>
/// Root settings object that is serialized to/from the JSON config file.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Settings schema version for migration purposes.
    /// Current version is 1. Older settings files without this field are treated as version 0.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Application theme. Valid values: <c>"System"</c>, <c>"Light"</c>, <c>"Dark"</c>.
    /// </summary>
    public string Theme { get; set; } = "System";

    /// <summary>
    /// Tray icon style. Valid values: <c>"System"</c>, <c>"Colorful"</c>, <c>"Black"</c>, <c>"White"</c>.
    /// </summary>
    public string TrayIconStyle { get; set; } = "Colorful";

    /// <summary>
    /// Accent color source. Valid values: <c>"System"</c>, <c>"Custom"</c>.
    /// </summary>
    public string AccentColorMode { get; set; } = "System";

    /// <summary>
    /// Custom accent color in hex format such as <c>#0078D4</c>.
    /// </summary>
    public string CustomAccentColor { get; set; } = "#0078D4";

    /// <summary>Whether BentoDesk should launch automatically at Windows startup.</summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>Whether BentoDesk should check for updates in the background.</summary>
    public bool AutoCheckForUpdates { get; set; } = true;

    /// <summary>Last time BentoDesk successfully attempted an update check.</summary>
    public DateTimeOffset? LastUpdateCheckAt { get; set; }

    /// <summary>
    /// Enabled state for singleton feature widgets, keyed by <see cref="WidgetKind"/> name.
    /// </summary>
    public Dictionary<string, bool> FeatureWidgetEnabledStates { get; set; } = [];

    /// <summary>Whether the Music widget uses album artwork color as a soft backdrop.</summary>
    public bool MusicUseArtworkBackdrop { get; set; } = true;

    /// <summary>Whether the Music widget album cover reacts lightly to pointer hover.</summary>
    public bool MusicEnableCoverHoverMotion { get; set; } = true;

    /// <summary>
    /// Music widget layout mode. Valid values: <c>"Auto"</c>, <c>"Cover"</c>, <c>"Controls"</c>.
    /// </summary>
    public string MusicDisplayMode { get; set; } = "Auto";

    /// <summary>Whether the global hotkey is enabled.</summary>
    public bool GlobalHotkeyEnabled { get; set; } = true;

    /// <summary>Global hotkey modifier bit flags.</summary>
    public int GlobalHotkeyModifiers { get; set; } = (int)HotkeyModifierKeys.None;

    /// <summary>Global hotkey virtual key code.</summary>
    public int GlobalHotkeyKey { get; set; } = (int)Windows.System.VirtualKey.F7;

    /// <summary>Whether the first-run onboarding has been completed or skipped.</summary>
    public bool HasCompletedOnboarding { get; set; }

    /// <summary>Default width applied to newly created widgets.</summary>
    public double DefaultWidgetWidth { get; set; } = 280;

    /// <summary>Default height applied to newly created widgets.</summary>
    public double DefaultWidgetHeight { get; set; } = 400;

    /// <summary>
    /// Background opacity for widget windows (0.0 - 1.0).
    /// </summary>
    public double WidgetOpacity { get; set; } = 0.80;

    /// <summary>
    /// Material type for widget window backdrops.
    /// Valid values: <c>"Mica"</c>, <c>"MicaAlt"</c>, <c>"Acrylic"</c>,
    /// <c>"AcrylicBase"</c>, <c>"Solid"</c>.
    /// </summary>
    public string WidgetMaterialType { get; set; } = "Mica";

    /// <summary>Independent tint strength for native widget backdrop materials.</summary>
    public double WidgetMaterialIntensity { get; set; } = 0.65;

    /// <summary>
    /// Border color mode for widget windows.
    /// Valid values: <c>"Neutral"</c>, <c>"Accent"</c>, <c>"None"</c>.
    /// </summary>
    public string WidgetBorderColorMode { get; set; } = "Neutral";

    /// <summary>
    /// Border style for widget windows.
    /// Valid values: <c>"Thin"</c>, <c>"Medium"</c>, <c>"Thick"</c>.
    /// </summary>
    public string WidgetBorderStyle { get; set; } = "Thin";

    /// <summary>
    /// Native DWM corner style for widget windows.
    /// Valid values: <c>"Default"</c>, <c>"Square"</c>, <c>"Small"</c>, <c>"Round"</c>.
    /// </summary>
    public string WidgetCornerPreference { get; set; } = "Round";

    /// <summary>
    /// Animation effect used when desktop widgets show or hide.
    /// </summary>
    public string WidgetAnimationEffect { get; set; } = "Fade";

    /// <summary>
    /// Animation speed preset used when desktop widgets show or hide.
    /// </summary>
    public string WidgetAnimationSpeed { get; set; } = "Standard";

    /// <summary>
    /// Slide direction for Slide animation effect.
    /// Valid values: <c>"None"</c>, <c>"Left"</c>, <c>"Right"</c>, <c>"Up"</c>, <c>"Down"</c>.
    /// </summary>
    public string WidgetAnimationSlideDirection { get; set; } = "None";

    /// <summary>
    /// Easing intensity for animations.
    /// Valid values: <c>"None"</c>, <c>"Light"</c>, <c>"Standard"</c>, <c>"Strong"</c>.
    /// </summary>
    public string WidgetAnimationEasingIntensity { get; set; } = "Standard";

    /// <summary>
    /// Default chrome/title mode for display widgets such as Music and System Monitor.
    /// Valid values: <c>"Standard"</c>, <c>"Compact"</c>, <c>"Overlay"</c>, <c>"Hidden"</c>.
    /// </summary>
    public string DisplayWidgetChromeMode { get; set; } = "Overlay";

    /// <summary>
    /// Default chrome/title mode for interactive widgets such as file widgets.
    /// Valid values: <c>"Standard"</c>, <c>"Compact"</c>, <c>"Overlay"</c>, <c>"Hidden"</c>.
    /// </summary>
    public string InteractiveWidgetChromeMode { get; set; } = "Standard";

    /// <summary>
    /// How widgets enter and leave their compact state.
    /// Valid values: <c>"Expanded"</c>, <c>"Click"</c>, <c>"Smart"</c>.
    /// </summary>
    public string WidgetCollapseBehavior { get; set; } = "Smart";

    /// <summary>Whether widgets are allowed to enter compact capsule mode.</summary>
    public bool WidgetCapsuleModeEnabled { get; set; } = true;

    /// <summary>
    /// How compact and expanded widget widths relate to each other.
    /// Valid values: <c>"Aligned"</c>, <c>"Independent"</c>.
    /// </summary>
    public string WidgetCompactWidthMode { get; set; } = "Aligned";

    /// <summary>
    /// How compact widgets are arranged on the desktop.
    /// Valid values: <c>"Free"</c>, <c>"Bar"</c>.
    /// </summary>
    public string WidgetCapsuleArrangementMode { get; set; } = "Free";

    /// <summary>Logical pixel spacing between adjacent widgets in a capsule bar.</summary>
    public double WidgetCapsuleBarSpacing { get; set; } = 8;

    /// <summary>
    /// Where the capsule bar is anchored.
    /// Valid values: <c>"Floating"</c>, <c>"Top"</c>, <c>"Bottom"</c>,
    /// <c>"Left"</c>, <c>"Right"</c>.
    /// </summary>
    public string WidgetCapsuleBarPlacement { get; set; } = "Floating";

    /// <summary>
    /// Primary flow direction for the capsule bar.
    /// Valid values: <c>"Auto"</c>, <c>"Horizontal"</c>, <c>"Vertical"</c>.
    /// </summary>
    public string WidgetCapsuleBarDirection { get; set; } = "Auto";

    /// <summary>Stable user order used when compact widgets form a capsule bar.</summary>
    public List<string> WidgetCapsuleBarOrder { get; set; } = [];

    /// <summary>Free-layout placements preserved while a capsule bar is active.</summary>
    public Dictionary<string, WidgetCompactPlacement> WidgetCapsuleFreePlacements { get; set; } = [];

    /// <summary>
    /// Information density used by compact widgets.
    /// Valid values: <c>"Smart"</c>, <c>"Minimal"</c>, <c>"Summary"</c>.
    /// </summary>
    public string WidgetCompactContentMode { get; set; } = "Smart";

    /// <summary>Whether compact widgets hide sensitive content previews.</summary>
    public bool WidgetCompactHideSensitiveContent { get; set; }

    /// <summary>
    /// Motion style used when compact widgets expand or collapse.
    /// Valid values: <c>"Smooth"</c>, <c>"Slow"</c>, <c>"Snappy"</c>,
    /// <c>"Custom"</c>, <c>"None"</c>.
    /// </summary>
    public string WidgetCompactAnimationEffect { get; set; } = "Slow";

    /// <summary>Compact transition duration in milliseconds.</summary>
    public int WidgetCompactAnimationDurationMs { get; set; } = 220;

    /// <summary>Pointer hover delay before a smart compact widget expands.</summary>
    public int WidgetCompactExpandDelayMs { get; set; } = 180;

    /// <summary>Pointer leave delay before a smart compact widget collapses.</summary>
    public int WidgetCompactCollapseDelayMs { get; set; } = 420;

    /// <summary>
    /// Corner treatment for media inside compact widgets.
    /// Valid values: <c>"FollowWidget"</c>, <c>"Square"</c>, <c>"Small"</c>, <c>"Round"</c>.
    /// </summary>
    public string WidgetCompactMediaCornerMode { get; set; } = "FollowWidget";

    /// <summary>
    /// Title icon presentation for widget title bars.
    /// Valid values: <c>"FilledMono"</c>, <c>"LineMono"</c>, <c>"Color"</c>, <c>"Hidden"</c>, <c>"TextLabel"</c>.
    /// </summary>
    public string WidgetTitleIconMode { get; set; } = "Color";

    /// <summary>Whether to double click to open files.</summary>
    public bool DoubleClickToOpen { get; set; } = true;

    /// <summary>
    /// Whether double-clicking empty desktop space toggles hiding all desktop icons and widgets.
    /// </summary>
    public bool DoubleClickDesktopToHideAll { get; set; }

    /// <summary>
    /// Whether shortcut icons should hide the arrow overlay inside BentoDesk.
    /// </summary>
    public bool HideShortcutArrowOverlay { get; set; } = true;

    /// <summary>
    /// Whether image files in file widgets should use the system file icon instead of image thumbnails.
    /// </summary>
    public bool ShowImageFilesAsIcons { get; set; }

    /// <summary>
    /// Whether to show action buttons on widget hover.
    /// </summary>
    public bool ShowHoverButtons { get; set; } = true;

    /// <summary>
    /// Comma-separated widget title hover actions. Valid values: <c>"LockPosition"</c>,
    /// <c>"LockSize"</c>, <c>"Add"</c>, <c>"More"</c>, <c>"Delete"</c>.
    /// </summary>
    public string WidgetHoverButtonActions { get; set; } = "More";

    /// <summary>
    /// Whether resize snap-to-edge alignment guides are enabled during
    /// widget resize operations.
    /// </summary>
    public bool ResizeSnapEnabled { get; set; } = true;

    /// <summary>
    /// Whether list view should show secondary file details under item names.
    /// </summary>
    public bool ShowListItemDetails { get; set; }

    /// <summary>
    /// Whether file widgets show full path tooltips when hovering items.
    /// </summary>
    public bool ShowFileItemPathTooltips { get; set; } = true;

    /// <summary>Whether file widgets automatically group related items into stacks.</summary>
    public bool FileStacksEnabled { get; set; }

    /// <summary>Grouping rule used by automatic file stacks.</summary>
    public string FileStackGroupBy { get; set; } = "Kind";

    /// <summary>Minimum number of related files required to create an automatic stack.</summary>
    public int FileStackThreshold { get; set; } = 3;

    /// <summary>Ordering rule for members inside an automatic stack.</summary>
    public string FileStackOrderBy { get; set; } = "Widget";

    /// <summary>User-defined extension groups, evaluated in list order.</summary>
    public List<FileStackCustomRule> FileStackCustomRules { get; set; } = [];

    /// <summary>How files not matched by a custom rule are displayed.</summary>
    public string FileStackUnmatchedBehavior { get; set; } = "KeepLoose";

    /// <summary>
    /// How files should be handled when dropped into a managed storage widget.
    /// Valid values: <c>"Move"</c>, <c>"Copy"</c>.
    /// </summary>
    public string ManagedDropAction { get; set; } = "Move";

    /// <summary>
    /// Recent organization history used for undo and quick review.
    /// </summary>
    public List<OrganizationHistoryEntry> RecentOrganizationHistory { get; set; } = [];

    /// <summary>
    /// Icon size used by widgets in icon view.
    /// </summary>
    public double IconSize { get; set; } = 30;

    /// <summary>
    /// Label text size used by widgets in both icon and list views.
    /// </summary>
    public double TextSize { get; set; } = 11.5;

    /// <summary>
    /// Layout density preset for widget content.
    /// Valid values: <c>"Compact"</c>, <c>"Standard"</c>, <c>"Relaxed"</c>, <c>"Custom"</c>.
    /// </summary>
    public string LayoutDensity { get; set; } = "Standard";

    /// <summary>
    /// Continuous layout density scale used by widget content.
    /// Smaller values create a tighter layout.
    /// </summary>
    public double LayoutDensityScale { get; set; } = 0.56;

    /// <summary>
    /// Horizontal spacing scale used by widget content.
    /// Smaller values place items closer together horizontally.
    /// </summary>
    public double HorizontalSpacingScale { get; set; } = 0.40;

    /// <summary>
    /// Vertical spacing scale used by widget content.
    /// Smaller values place items closer together vertically.
    /// </summary>
    public double VerticalSpacingScale { get; set; } = 0.60;

    /// <summary>
    /// File name width scale used by icon-view labels.
    /// Smaller values keep labels narrower.
    /// </summary>
    public double FileNameWidthScale { get; set; } = 0.36;

    /// <summary>
    /// Whether widget item labels should include file extensions.
    /// </summary>
    public bool ShowFileExtensions { get; set; }

    /// <summary>
    /// Whether .lnk shortcuts should keep their extension hidden even when file extensions are shown.
    /// </summary>
    public bool HideShortcutExtensionWhenShowingFileExtensions { get; set; } = true;

    /// <summary>All configured widgets.</summary>
    public List<WidgetConfig> Widgets { get; set; } = [];

    /// <summary>
    /// When true, clicking one widget during batch raise keeps only that widget on top;
    /// others move to non-topmost. When false (default), all widgets stay visible together.
    /// </summary>
    public bool FocusClickedWidgetOnRaise { get; set; }

    /// <summary>Widget ids that were deleted and should not be restored.</summary>
    public List<string> DeletedWidgetIds { get; set; } = [];
}
