using BentoDesk.Models;
using Microsoft.UI.Xaml.Controls;

namespace BentoDesk.Services;

internal static class WidgetCollapseMenuBuilder
{
    public static MenuFlyoutSubItem Create(
        WidgetConfig config,
        LocalizationService localizationService,
        Action<WidgetCollapseBehavior> applyBehavior,
        Action resetCompactWidth,
        bool isCollapsed,
        Action syncWidthToOtherState,
        bool canSyncWidth)
    {
        WidgetCollapseBehavior selectedBehavior = WidgetCollapseBehaviorNames.GetOverride(config);
        var subItem = new MenuFlyoutSubItem
        {
            Text = localizationService.T("Widget.CollapseBehavior.Title"),
            Icon = new FontIcon { Glyph = "\uE73F" }
        };

        foreach (WidgetCollapseBehavior behavior in new[]
                 {
                     WidgetCollapseBehavior.System,
                     WidgetCollapseBehavior.Expanded,
                     WidgetCollapseBehavior.Click,
                     WidgetCollapseBehavior.Smart
                 })
        {
            var item = new ToggleMenuFlyoutItem
            {
                Text = localizationService.T(GetTextKey(behavior)),
                IsChecked = selectedBehavior == behavior
            };
            item.Click += (_, _) => applyBehavior(behavior);
            subItem.Items.Add(item);
        }

        subItem.Items.Add(new MenuFlyoutSeparator());

        string syncKey = isCollapsed
            ? "Widget.Compact.SyncWidthToExpanded"
            : "Widget.Compact.SyncWidthToCompact";
        var syncWidthItem = new MenuFlyoutItem
        {
            Text = localizationService.T(syncKey),
            IsEnabled = canSyncWidth
        };
        syncWidthItem.Click += (_, _) => syncWidthToOtherState();
        subItem.Items.Add(syncWidthItem);

        var resetWidthItem = new MenuFlyoutItem
        {
            Text = localizationService.T("Widget.Compact.RestoreAutomaticWidth"),
            IsEnabled = config.CompactWidth is not null
        };
        resetWidthItem.Click += (_, _) => resetCompactWidth();
        subItem.Items.Add(resetWidthItem);

        return subItem;
    }

    internal static double ClampExpandedLogicalWidth(double width)
    {
        const double maxExpandedWidth = 1200;
        double finiteWidth = double.IsFinite(width) ? width : SettingsService.DefaultWidgetWidth;
        return Math.Clamp(finiteWidth, SettingsService.MinWidgetWidth, maxExpandedWidth);
    }

    private static string GetTextKey(WidgetCollapseBehavior behavior)
    {
        return behavior switch
        {
            WidgetCollapseBehavior.System => "Widget.CollapseBehavior.System",
            WidgetCollapseBehavior.Expanded => "Widget.CollapseBehavior.Expanded",
            WidgetCollapseBehavior.Smart => "Widget.CollapseBehavior.Smart",
            _ => "Widget.CollapseBehavior.Click"
        };
    }
}
