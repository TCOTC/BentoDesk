using BentoDesk.Models;

namespace BentoDesk.Services;

/// <summary>
/// Helpers for managed-widget desktop file membership (files stay on the user desktop).
/// </summary>
public static class ManagedDesktopMembership
{
    public static bool IsManagedDesktopWidget(WidgetConfig? widget)
    {
        return widget is not null &&
               widget.WidgetKind == WidgetKind.File &&
               widget.FollowsDefaultStoragePath &&
               !widget.IsDisabled;
    }

    /// <summary>
    /// Category widgets that own desktop files via <see cref="WidgetConfig.Items"/>.
    /// The uncategorized default inbox is excluded — it displays the complement at runtime.
    /// </summary>
    public static bool IsCategorizingManagedWidget(WidgetConfig? widget)
    {
        return IsManagedDesktopWidget(widget) && widget is { IsUncategorizedDefault: false };
    }

    public static HashSet<string> CollectClaimedPaths(
        IEnumerable<WidgetConfig> widgets,
        IEnumerable<string>? deletedWidgetIds = null)
    {
        var deleted = deletedWidgetIds?.ToHashSet(StringComparer.OrdinalIgnoreCase)
                      ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var widget in widgets)
        {
            if (!IsCategorizingManagedWidget(widget) || deleted.Contains(widget.Id))
            {
                continue;
            }

            foreach (var item in widget.Items)
            {
                if (!string.IsNullOrWhiteSpace(item.Path))
                {
                    claimed.Add(Path.GetFullPath(item.Path));
                }
            }
        }

        return claimed;
    }

    public static bool IsOnUserDesktop(string path, string? userDesktop = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        userDesktop ??= Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string fullPath = Path.GetFullPath(path);
        string desktop = Path.GetFullPath(userDesktop);
        string? parent = Path.GetDirectoryName(fullPath);
        return parent is not null &&
               parent.Equals(desktop, StringComparison.OrdinalIgnoreCase);
    }

    public static void AddMembership(WidgetConfig widget, string path, int? sortOrder = null)
    {
        string fullPath = Path.GetFullPath(path);
        if (widget.Items.Any(item => item.Path.Equals(fullPath, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        int order = sortOrder ?? (widget.Items.Count == 0 ? 0 : widget.Items.Max(item => item.SortOrder) + 1);
        widget.Items.Add(new WidgetItemConfig
        {
            Path = fullPath,
            SortOrder = order
        });

        widget.FileAddedAtByPath[fullPath] = DateTimeOffset.Now;
    }

    public static bool RemoveMembership(WidgetConfig widget, string path)
    {
        string fullPath = Path.GetFullPath(path);
        int removed = widget.Items.RemoveAll(item =>
            item.Path.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
        widget.FileAddedAtByPath.Remove(fullPath);
        return removed > 0;
    }

    public static WidgetConfig? FindOwner(
        IEnumerable<WidgetConfig> widgets,
        string path,
        IEnumerable<string>? deletedWidgetIds = null)
    {
        string fullPath = Path.GetFullPath(path);
        var deleted = deletedWidgetIds?.ToHashSet(StringComparer.OrdinalIgnoreCase)
                      ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return widgets.FirstOrDefault(widget =>
            IsCategorizingManagedWidget(widget) &&
            !deleted.Contains(widget.Id) &&
            widget.Items.Any(item => item.Path.Equals(fullPath, StringComparison.OrdinalIgnoreCase)));
    }
}
