using BentoDesk.Models;

namespace BentoDesk.Services;

public sealed class OrganizerService
{
    private readonly SettingsService _settingsService;
    private readonly FileService _fileService;
    private readonly Func<string> _desktopPathProvider;
    private Action? _membershipChanged;

    public OrganizerService(
        SettingsService settingsService,
        FileService fileService,
        Func<string>? desktopPathProvider = null)
    {
        _settingsService = settingsService;
        _fileService = fileService;
        _desktopPathProvider = desktopPathProvider ?? (() => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
    }

    /// <summary>
    /// Raised after managed-desktop membership changes (for free-desktop refresh).
    /// </summary>
    public void SetMembershipChangedCallback(Action? callback) => _membershipChanged = callback;

    /// <summary>
    /// Notifies listeners that managed desktop membership changed outside OrganizeDrop
    /// (e.g. folder watcher rename/delete on a category widget).
    /// </summary>
    public void RaiseMembershipChanged() => NotifyMembershipChanged();

    public IReadOnlyList<OrganizationHistoryEntry> GetRecentHistory(int maxCount = 6)
    {
        return _settingsService.Settings.RecentOrganizationHistory
            .OrderByDescending(entry => entry.TimestampUtc)
            .Take(Math.Max(0, maxCount))
            .ToList();
    }

    public OrganizationHistoryEntry? GetLatestUndoableEntry()
    {
        return _settingsService.Settings.RecentOrganizationHistory
            .Where(entry => entry.CanUndo && !entry.IsUndone && !entry.IsFailed && entry.Items.Count > 0)
            .OrderByDescending(entry => entry.TimestampUtc)
            .FirstOrDefault();
    }

    public async Task<OrganizationHistoryEntry> OrganizeDropAsync(
        WidgetConfig widget,
        string widgetName,
        IEnumerable<string> sourcePaths,
        bool move,
        bool useShellProgress = false)
    {
        if (ManagedDesktopMembership.IsManagedDesktopWidget(widget))
        {
            return await OrganizeManagedDesktopDropAsync(widget, widgetName, sourcePaths, move, useShellProgress);
        }

        if (string.IsNullOrWhiteSpace(widget.MappedFolderPath))
        {
            throw new InvalidOperationException("This widget does not have a managed folder path.");
        }

        string rootPath = Path.GetFullPath(widget.MappedFolderPath);
        var normalizedSourcePaths = sourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .ToList();

        if (normalizedSourcePaths.Count == 0)
        {
            throw new InvalidOperationException("No items were available to organize.");
        }

        try
        {
            var reservedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var plans = normalizedSourcePaths
                .Select(path =>
                {
                    string destinationPath = FileService.GetAvailablePath(
                        Path.Combine(rootPath, Path.GetFileName(path)),
                        reservedPaths);
                    return new FileService.FileTransferPlan(path, destinationPath);
                })
                .ToList();

            var results = await _fileService.ExecuteTransferPlanAsync(plans, move, useShellProgress);
            var historyEntry = CreateHistoryEntry(
                widget.Id,
                widgetName,
                OrganizationActionType.ManagedDrop,
                move,
                results.Select(result => new OrganizationHistoryItem
                {
                    Name = Path.GetFileName(result.DestinationPath),
                    SourcePath = result.SourcePath,
                    DestinationPath = result.DestinationPath
                }).ToList(),
                canUndo: move);

            await AddHistoryEntryAsync(historyEntry);
            return historyEntry;
        }
        catch (Exception ex)
        {
            await AddHistoryEntryAsync(CreateFailureEntry(
                widget.Id,
                widgetName,
                OrganizationActionType.ManagedDrop,
                move,
                normalizedSourcePaths,
                ex.Message));
            throw;
        }
    }

    public async Task<OrganizationHistoryEntry> MoveItemBackToDesktopAsync(
        WidgetConfig widget,
        string widgetName,
        WidgetItem item,
        bool useShellProgress = false)
    {
        return await MoveItemsBackToDesktopAsync(widget, widgetName, [item.Path], useShellProgress);
    }

    public async Task<OrganizationHistoryEntry> MoveItemsBackToDesktopAsync(
        WidgetConfig widget,
        string widgetName,
        IEnumerable<string> sourcePaths,
        bool useShellProgress = false)
    {
        if (ManagedDesktopMembership.IsManagedDesktopWidget(widget))
        {
            return await ReleaseManagedDesktopMembershipAsync(widget, widgetName, sourcePaths);
        }

        if (string.IsNullOrWhiteSpace(widget.MappedFolderPath))
        {
            throw new InvalidOperationException("This widget does not have a folder path.");
        }

        var normalizedSourcePaths = sourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .ToList();
        if (normalizedSourcePaths.Count == 0)
        {
            throw new FileNotFoundException("No items to restore could be found.");
        }

        string desktopPath = _desktopPathProvider();
        var reservedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var plans = normalizedSourcePaths
            .Select(sourcePath => new FileService.FileTransferPlan(
                sourcePath,
                FileService.GetAvailablePath(Path.Combine(desktopPath, Path.GetFileName(sourcePath)), reservedPaths)))
            .ToList();

        try
        {
            var results = await _fileService.ExecuteTransferPlanAsync(plans, move: true, useShellProgress);

            var historyEntry = CreateHistoryEntry(
                widget.Id,
                widgetName,
                OrganizationActionType.MoveBackToDesktop,
                move: true,
                results.Select(result => new OrganizationHistoryItem
                {
                    Name = Path.GetFileName(result.DestinationPath),
                    SourcePath = result.SourcePath,
                    DestinationPath = result.DestinationPath
                }).ToList(),
                canUndo: true);

            await AddHistoryEntryAsync(historyEntry);
            return historyEntry;
        }
        catch (Exception ex)
        {
            await AddHistoryEntryAsync(CreateFailureEntry(
                widget.Id,
                widgetName,
                OrganizationActionType.MoveBackToDesktop,
                move: true,
                normalizedSourcePaths,
                ex.Message));
            throw;
        }
    }

    public async Task<bool> UndoLatestAsync()
    {
        var latestEntry = GetLatestUndoableEntry();
        if (latestEntry is null)
        {
            return false;
        }

        await UndoAsync(latestEntry.Id);
        return true;
    }

    public async Task UndoAsync(string historyEntryId)
    {
        var historyEntry = _settingsService.Settings.RecentOrganizationHistory
            .FirstOrDefault(entry => string.Equals(entry.Id, historyEntryId, StringComparison.Ordinal));

        if (historyEntry is null || !historyEntry.CanUndo || historyEntry.IsUndone || historyEntry.IsFailed)
        {
            throw new InvalidOperationException("The selected history entry cannot be undone.");
        }

        var widget = _settingsService.Settings.Widgets
            .FirstOrDefault(candidate => string.Equals(candidate.Id, historyEntry.WidgetId, StringComparison.Ordinal));

        if (widget is not null &&
            ManagedDesktopMembership.IsManagedDesktopWidget(widget) &&
            historyEntry.ActionType is OrganizationActionType.ManagedDrop or OrganizationActionType.MoveBackToDesktop)
        {
            await UndoManagedDesktopAsync(historyEntry, widget);
            return;
        }

        var reservedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var plans = new List<FileService.FileTransferPlan>(historyEntry.Items.Count);

        foreach (var item in historyEntry.Items)
        {
            if (!File.Exists(item.DestinationPath) && !Directory.Exists(item.DestinationPath))
            {
                throw new InvalidOperationException($"Could not find undo target: {item.Name}");
            }

            string restorePath = FileService.GetAvailablePath(item.SourcePath, reservedPaths);
            plans.Add(new FileService.FileTransferPlan(item.DestinationPath, restorePath));
        }

        await _fileService.ExecuteTransferPlanAsync(plans, move: true);

        historyEntry.IsUndone = true;
        historyEntry.CanUndo = false;
        for (int index = 0; index < plans.Count; index++)
        {
            historyEntry.Items[index].DestinationPath = plans[index].DestinationPath;
        }

        await _settingsService.SaveAsync(notifySubscribers: false);
    }

    private async Task<OrganizationHistoryEntry> OrganizeManagedDesktopDropAsync(
        WidgetConfig widget,
        string widgetName,
        IEnumerable<string> sourcePaths,
        bool move,
        bool useShellProgress)
    {
        string desktopPath = Path.GetFullPath(_desktopPathProvider());
        var normalizedSourcePaths = sourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .ToList();

        if (normalizedSourcePaths.Count == 0)
        {
            throw new InvalidOperationException("No items were available to organize.");
        }

        try
        {
            var claimed = ManagedDesktopMembership.CollectClaimedPaths(
                _settingsService.Settings.Widgets,
                _settingsService.Settings.DeletedWidgetIds);
            var historyItems = new List<OrganizationHistoryItem>();
            var reservedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var transferPlans = new List<FileService.FileTransferPlan>();
            var transferSources = new List<string>();

            foreach (string sourcePath in normalizedSourcePaths)
            {
                if (widget.Items.Any(item => item.Path.Equals(sourcePath, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                // Steal from another managed widget if needed.
                var previousOwner = ManagedDesktopMembership.FindOwner(
                    _settingsService.Settings.Widgets,
                    sourcePath,
                    _settingsService.Settings.DeletedWidgetIds);
                if (previousOwner is not null &&
                    !string.Equals(previousOwner.Id, widget.Id, StringComparison.Ordinal))
                {
                    ManagedDesktopMembership.RemoveMembership(previousOwner, sourcePath);
                }

                string destinationPath = sourcePath;
                if (!ManagedDesktopMembership.IsOnUserDesktop(sourcePath, desktopPath))
                {
                    destinationPath = FileService.GetAvailablePath(
                        Path.Combine(desktopPath, Path.GetFileName(sourcePath)),
                        reservedPaths);
                    transferPlans.Add(new FileService.FileTransferPlan(sourcePath, destinationPath));
                    transferSources.Add(sourcePath);
                }
                else if (claimed.Contains(sourcePath) && previousOwner is null)
                {
                    // Already claimed by this widget — skipped above.
                }

                historyItems.Add(new OrganizationHistoryItem
                {
                    Name = Path.GetFileName(destinationPath),
                    SourcePath = sourcePath,
                    DestinationPath = destinationPath
                });
            }

            if (transferPlans.Count > 0)
            {
                var results = await _fileService.ExecuteTransferPlanAsync(transferPlans, move, useShellProgress);
                for (int i = 0; i < results.Count; i++)
                {
                    var match = historyItems.FirstOrDefault(item =>
                        item.SourcePath.Equals(transferSources[i], StringComparison.OrdinalIgnoreCase));
                    if (match is not null)
                    {
                        match.DestinationPath = results[i].DestinationPath;
                        match.Name = Path.GetFileName(results[i].DestinationPath);
                    }
                }
            }

            // Uncategorized inbox shows desktop − claimed; never stores Items membership.
            if (!widget.IsUncategorizedDefault)
            {
                foreach (var historyItem in historyItems)
                {
                    if (string.IsNullOrWhiteSpace(historyItem.DestinationPath))
                    {
                        continue;
                    }

                    ManagedDesktopMembership.AddMembership(widget, historyItem.DestinationPath);
                }
            }

            await _settingsService.SaveAsync(notifySubscribers: false);
            NotifyMembershipChanged();

            bool canUndo = historyItems.Count > 0;
            var historyEntry = CreateHistoryEntry(
                widget.Id,
                widgetName,
                OrganizationActionType.ManagedDrop,
                move,
                historyItems,
                canUndo);
            await AddHistoryEntryAsync(historyEntry);
            return historyEntry;
        }
        catch (Exception ex)
        {
            await AddHistoryEntryAsync(CreateFailureEntry(
                widget.Id,
                widgetName,
                OrganizationActionType.ManagedDrop,
                move,
                normalizedSourcePaths,
                ex.Message));
            throw;
        }
    }

    private async Task<OrganizationHistoryEntry> ReleaseManagedDesktopMembershipAsync(
        WidgetConfig widget,
        string widgetName,
        IEnumerable<string> sourcePaths)
    {
        var normalizedSourcePaths = sourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var historyItems = new List<OrganizationHistoryItem>();
        foreach (string path in normalizedSourcePaths)
        {
            if (!ManagedDesktopMembership.RemoveMembership(widget, path))
            {
                continue;
            }

            historyItems.Add(new OrganizationHistoryItem
            {
                Name = Path.GetFileName(path),
                SourcePath = path,
                DestinationPath = path
            });
        }

        if (historyItems.Count == 0)
        {
            throw new FileNotFoundException("No items to restore could be found.");
        }

        await _settingsService.SaveAsync(notifySubscribers: false);
        NotifyMembershipChanged();

        var historyEntry = CreateHistoryEntry(
            widget.Id,
            widgetName,
            OrganizationActionType.MoveBackToDesktop,
            move: true,
            historyItems,
            canUndo: true);
        await AddHistoryEntryAsync(historyEntry);
        return historyEntry;
    }

    private async Task UndoManagedDesktopAsync(OrganizationHistoryEntry historyEntry, WidgetConfig widget)
    {
        if (historyEntry.ActionType == OrganizationActionType.ManagedDrop)
        {
            foreach (var item in historyEntry.Items)
            {
                ManagedDesktopMembership.RemoveMembership(widget, item.DestinationPath);

                // If we physically moved onto the desktop from elsewhere, move back.
                if (!string.Equals(item.SourcePath, item.DestinationPath, StringComparison.OrdinalIgnoreCase) &&
                    (File.Exists(item.DestinationPath) || Directory.Exists(item.DestinationPath)) &&
                    historyEntry.TransferMode == "Move")
                {
                    string restorePath = FileService.GetAvailablePath(item.SourcePath);
                    await _fileService.ExecuteTransferPlanAsync(
                        [new FileService.FileTransferPlan(item.DestinationPath, restorePath)],
                        move: true);
                    item.DestinationPath = restorePath;
                }
            }
        }
        else if (historyEntry.ActionType == OrganizationActionType.MoveBackToDesktop)
        {
            foreach (var item in historyEntry.Items)
            {
                if (File.Exists(item.SourcePath) || Directory.Exists(item.SourcePath))
                {
                    ManagedDesktopMembership.AddMembership(widget, item.SourcePath);
                }
            }
        }

        historyEntry.IsUndone = true;
        historyEntry.CanUndo = false;
        await _settingsService.SaveAsync(notifySubscribers: false);
        NotifyMembershipChanged();
    }

    private void NotifyMembershipChanged()
    {
        try
        {
            _membershipChanged?.Invoke();
        }
        catch (Exception ex)
        {
            App.Log($"[Organizer] MembershipChanged callback failed: {ex}");
        }
    }

    private async Task AddHistoryEntryAsync(OrganizationHistoryEntry entry)
    {
        _settingsService.Settings.RecentOrganizationHistory.Insert(0, entry);
        await _settingsService.SaveAsync(notifySubscribers: false);
    }

    private static OrganizationHistoryEntry CreateHistoryEntry(
        string widgetId,
        string widgetName,
        string actionType,
        bool move,
        List<OrganizationHistoryItem> items,
        bool canUndo)
    {
        return new OrganizationHistoryEntry
        {
            WidgetId = widgetId,
            WidgetName = widgetName,
            ActionType = actionType,
            TransferMode = move ? "Move" : "Copy",
            CanUndo = canUndo,
            Items = items
        };
    }

    private static OrganizationHistoryEntry CreateFailureEntry(
        string widgetId,
        string widgetName,
        string actionType,
        bool move,
        IEnumerable<string> sourcePaths,
        string errorMessage)
    {
        return new OrganizationHistoryEntry
        {
            WidgetId = widgetId,
            WidgetName = widgetName,
            ActionType = actionType,
            TransferMode = move ? "Move" : "Copy",
            ErrorMessage = errorMessage,
            Items = sourcePaths
                .Select(path => new OrganizationHistoryItem
                {
                    Name = Path.GetFileName(path),
                    SourcePath = path,
                    DestinationPath = string.Empty
                })
                .ToList()
        };
    }
}
