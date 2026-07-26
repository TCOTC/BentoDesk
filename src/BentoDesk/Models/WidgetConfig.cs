using System.Text.Json;
using System.Text.Json.Serialization;

namespace BentoDesk.Models;

/// <summary>
/// Represents the persisted configuration for a single desktop widget.
/// </summary>
public class WidgetConfig
{
    public const int CurrentBoundsCoordinateVersion = 1;

    /// <summary>Unique identifier for this widget instance.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>User-facing display name.</summary>
    public string Name { get; set; } = "BentoDesk";

    /// <summary>Whether the title is the default (not user-customized). Default titles follow language changes.</summary>
    public bool IsDefaultTitle { get; set; } = true;

    /// <summary>Last resolved screen X position in physical pixels.</summary>
    public double X { get; set; } = 100;

    /// <summary>Last resolved screen Y position in physical pixels.</summary>
    public double Y { get; set; } = 100;

    /// <summary>
    /// Corner used to keep this widget stable when display resolution or monitor topology changes.
    /// Values are "LeftTop", "RightTop", "LeftBottom", or "RightBottom".
    /// </summary>
    public string? PositionAnchor { get; set; }

    /// <summary>Horizontal distance from the selected anchor edge in logical pixels.</summary>
    public double PositionMarginX { get; set; }

    /// <summary>Vertical distance from the selected anchor edge in logical pixels.</summary>
    public double PositionMarginY { get; set; }

    /// <summary>Work area signature of the monitor where the widget was last positioned.</summary>
    public string? PositionMonitorKey { get; set; }

    /// <summary>Win32 monitor device name where the widget was last positioned, used before the legacy work area signature.</summary>
    public string? PositionMonitorDeviceName { get; set; }

    /// <summary>Whether the monitor was primary when this widget position was captured. Primary widgets follow the current primary monitor.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? PositionMonitorWasPrimary { get; set; }

    /// <summary>Bounds coordinate model version. Current version stores size and anchor margins in logical pixels.</summary>
    public int BoundsCoordinateVersion { get; set; } = CurrentBoundsCoordinateVersion;

    /// <summary>Widget width in logical pixels.</summary>
    public double Width { get; set; } = 300;

    /// <summary>Widget height in logical pixels.</summary>
    public double Height { get; set; } = 400;

    /// <summary>Widget content type.</summary>
    [JsonConverter(typeof(WidgetKindJsonConverter))]
    public WidgetKind WidgetKind { get; set; } = WidgetKind.File;

    /// <summary>Current view layout mode (Icon grid or List).</summary>
    public ViewMode ViewMode { get; set; } = ViewMode.Icon;

    /// <summary>Whether the widget window is currently shown.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Whether the widget is disabled from desktop restore and tray listing.</summary>
    public bool IsDisabled { get; set; }

    /// <summary>Whether moving this widget is locked.</summary>
    public bool IsPositionLocked { get; set; }

    /// <summary>Whether resizing this widget is locked.</summary>
    public bool IsSizeLocked { get; set; }

    /// <summary>Whether this widget was manually left in its compact state.</summary>
    public bool IsCollapsed { get; set; }

    /// <summary>Independent persisted placement for the compact capsule.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WidgetCompactPlacement? CompactPlacement { get; set; }

    /// <summary>Optional user-adjusted compact capsule width in logical pixels.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? CompactWidth { get; set; }

    /// <summary>
    /// Optional widget-specific payload for future widget kinds.
    /// Stored as simple string values so the config remains easy to serialize and migrate.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = [];

    /// <summary>
    /// Optional filesystem folder to mirror. When set, the widget auto-populates
    /// from folder contents. When <c>null</c>, items are managed manually.
    /// </summary>
    public string? MappedFolderPath { get; set; }

    /// <summary>
    /// Whether this widget follows the global default managed storage root path.
    /// </summary>
    public bool FollowsDefaultStoragePath { get; set; }

    /// <summary>
    /// Inbox for desktop files not claimed by any other managed widget (desktop − other Items).
    /// At most one active File widget should have this set.
    /// </summary>
    public bool IsUncategorizedDefault { get; set; }

    /// <summary>Stable subfolder name used when the widget follows the default managed storage path.</summary>
    public string? ManagedFolderName { get; set; }

    /// <summary>How items are sorted in this widget.</summary>
    public WidgetSortMode SortMode { get; set; } = WidgetSortMode.Name;

    /// <summary>Whether the sort order is descending.</summary>
    public bool SortDescending { get; set; }

    /// <summary>Ordered list of items displayed in this widget.</summary>
    public List<WidgetItemConfig> Items { get; set; } = [];

    /// <summary>When each file was first added to or observed by this BentoDesk widget.</summary>
    public Dictionary<string, DateTimeOffset> FileAddedAtByPath { get; set; } = [];
}

public sealed class WidgetCompactPlacement
{
    public double X { get; set; }

    public double Y { get; set; }

    public string? PositionAnchor { get; set; }

    public double PositionMarginX { get; set; }

    public double PositionMarginY { get; set; }

    public string? PositionMonitorKey { get; set; }

    public string? PositionMonitorDeviceName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? PositionMonitorWasPrimary { get; set; }

    public int BoundsCoordinateVersion { get; set; } = WidgetConfig.CurrentBoundsCoordinateVersion;
}

/// <summary>
/// Persisted reference to a single item (file or shortcut) inside a widget.
/// </summary>
public class WidgetItemConfig
{
    /// <summary>Absolute path to the file, folder, or shortcut.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Display order within the parent widget.</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Determines how items are sorted inside a widget.
/// </summary>
public enum WidgetSortMode
{
    /// <summary>Sort by display name.</summary>
    Name,

    /// <summary>Sort by file size.</summary>
    Size,

    /// <summary>Sort by file type / extension.</summary>
    Type,

    /// <summary>Sort by last modification date.</summary>
    DateModified,

    /// <summary>Manual order: items stay where the user placed them via drag.</summary>
    Manual
}

/// <summary>
/// Determines how items are laid out inside a widget.
/// </summary>
public enum ViewMode
{
    /// <summary>Items displayed as an icon grid.</summary>
    Icon,

    /// <summary>Items displayed as a vertical list with details.</summary>
    List
}

/// <summary>
/// Defines the functional mode of a widget.
/// </summary>
[JsonConverter(typeof(WidgetKindJsonConverter))]
public enum WidgetKind
{
    /// <summary>File-oriented widget used for references or folder-backed storage.</summary>
    File,

    /// <summary>Music control widget backed by Windows media sessions.</summary>
    Music
}

public sealed class WidgetKindJsonConverter : JsonConverter<WidgetKind>
{
    public override WidgetKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string? value = reader.GetString();
            return Enum.TryParse(value, ignoreCase: true, out WidgetKind parsed) &&
                   Enum.IsDefined(parsed)
                ? parsed
                : WidgetKind.File;
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int numericValue))
        {
            var parsed = (WidgetKind)numericValue;
            return Enum.IsDefined(parsed) ? parsed : WidgetKind.File;
        }

        return WidgetKind.File;
    }

    public override void Write(Utf8JsonWriter writer, WidgetKind value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(Enum.IsDefined(value) ? value.ToString() : WidgetKind.File.ToString());
    }
}
