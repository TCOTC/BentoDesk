// Copyright (c) BentoDesk. All rights reserved.

using BentoDesk.Models;

namespace BentoDesk.Services;

/// <summary>
/// Partial class containing storage helpers for WidgetManager.
/// Desktop-membership widgets keep files on the user desktop; mapped widgets point at an existing folder.
/// </summary>
public sealed partial class WidgetManager
{
    public async Task NotifyItemsMovedOutAsync(string widgetId, IEnumerable<string> sourcePaths)
    {
        if (!_widgets.TryGetValue(widgetId, out var entry) || IsDeleted(widgetId))
        {
            return;
        }

        await entry.ViewModel.HandleItemsMovedOutAsync(sourcePaths);
    }

    private void EnsureManagedWidgetDisplayNameAvailable(string widgetId, string newName)
    {
        bool nameInUse = _settingsService.Settings.Widgets.Any(widget =>
            widget.WidgetKind == WidgetKind.File &&
            widget.FollowsDefaultStoragePath &&
            !IsDeleted(widget.Id) &&
            !string.Equals(widget.Id, widgetId, StringComparison.Ordinal) &&
            string.Equals(widget.Name.Trim(), newName.Trim(), StringComparison.OrdinalIgnoreCase));

        if (nameInUse)
        {
            throw new InvalidOperationException(_localizationService.T("Widget.Error.ManagedFolderNameExists"));
        }
    }
}
