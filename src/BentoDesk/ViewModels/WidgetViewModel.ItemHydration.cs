using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BentoDesk.Helpers;
using BentoDesk.Models;
using BentoDesk.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace BentoDesk.ViewModels;

public partial class WidgetViewModel
{
    private void EnsureFolderBackedConfig()
    {
        if (Config.FollowsDefaultStoragePath)
        {
            EnsureManagedDesktopConfig();
            return;
        }

        if (!string.IsNullOrWhiteSpace(Config.MappedFolderPath))
        {
            Config.MappedFolderPath = Path.GetFullPath(Config.MappedFolderPath);
            return;
        }

        // No mapped path: use painted-desktop membership mode.
        Config.FollowsDefaultStoragePath = true;
        EnsureManagedDesktopConfig();
    }

    private void EnsureManagedDesktopConfig()
    {
        Config.FollowsDefaultStoragePath = true;
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (!string.Equals(Config.MappedFolderPath, desktopPath, StringComparison.OrdinalIgnoreCase))
        {
            Config.MappedFolderPath = desktopPath;
            _settingsService.SaveDebounced();
        }
    }

    /// <summary>
    /// Loads desktop files that are not claimed by any categorizing managed widget.
    /// </summary>
    public async Task LoadUncategorizedDesktopAsync(bool clearIconCacheBeforeHydration = false)
    {
        using var perfScope = PerformanceLogger.Measure(
            "WidgetViewModel.LoadUncategorizedDesktop",
            $"id={Config.Id}");

        var claimed = ManagedDesktopMembership.CollectClaimedPaths(
            _settingsService.Settings.Widgets,
            _settingsService.Settings.DeletedWidgetIds);

        var settings = _settingsService.Settings;
        var (userDesktop, publicDesktop) = FileService.GetDesktopPaths();
        var userItems = await _fileService.EnumerateDirectoryAsync(
            userDesktop,
            hideShortcutArrowOverlay: settings.HideShortcutArrowOverlay,
            showImageFilesAsIcons: settings.ShowImageFilesAsIcons,
            showFileExtensions: settings.ShowFileExtensions,
            hideShortcutExtensionWhenShowingFileExtensions: settings.HideShortcutExtensionWhenShowingFileExtensions,
            loadIcons: false,
            loadFolderItemCounts: false);

        List<WidgetItem> publicItems = [];
        if (!string.IsNullOrWhiteSpace(publicDesktop) && Directory.Exists(publicDesktop))
        {
            publicItems = await _fileService.EnumerateDirectoryAsync(
                publicDesktop,
                hideShortcutArrowOverlay: settings.HideShortcutArrowOverlay,
                showImageFilesAsIcons: settings.ShowImageFilesAsIcons,
                showFileExtensions: settings.ShowFileExtensions,
                hideShortcutExtensionWhenShowingFileExtensions: settings.HideShortcutExtensionWhenShowingFileExtensions,
                loadIcons: false,
                loadFolderItemCounts: false);
        }

        var freeItems = userItems.Concat(publicItems)
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Where(item => !claimed.Contains(Path.GetFullPath(item.Path)))
            .OrderBy(item => !item.IsFolder)
            .ThenBy(item => item.Name, NaturalStringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (clearIconCacheBeforeHydration)
        {
            ClearCurrentItemIconCache();
        }

        // Inbox does not persist membership Items.
        if (Config.Items.Count > 0)
        {
            Config.Items = [];
            _settingsService.SaveDebounced(notifySubscribers: false);
        }

        ApplyPersistedAddedTimes(freeItems);
        SyncFolderItems(freeItems);
        SortItems();
        StartItemHydration();
    }

    private async Task LoadManagedMembershipAsync(bool clearIconCacheBeforeHydration = false)
    {
        using var perfScope = PerformanceLogger.Measure(
            "WidgetViewModel.LoadManagedMembership",
            $"id={Config.Id} count={Config.Items.Count}");

        var settings = _settingsService.Settings;
        var loaded = new List<WidgetItem>();
        var surviving = new List<WidgetItemConfig>();

        foreach (var configItem in Config.Items
                     .Where(item => !string.IsNullOrWhiteSpace(item.Path))
                     .OrderBy(item => item.SortOrder))
        {
            string fullPath = Path.GetFullPath(configItem.Path);
            if (clearIconCacheBeforeHydration)
            {
                _fileService.ClearIconCache(
                    fullPath,
                    settings.HideShortcutArrowOverlay,
                    settings.ShowImageFilesAsIcons);
            }

            var item = await _fileService.TryCreateWidgetItemAsync(
                fullPath,
                hideShortcutArrowOverlay: settings.HideShortcutArrowOverlay,
                showImageFilesAsIcons: settings.ShowImageFilesAsIcons,
                showFileExtensions: settings.ShowFileExtensions,
                hideShortcutExtensionWhenShowingFileExtensions: settings.HideShortcutExtensionWhenShowingFileExtensions,
                loadIcon: false,
                loadFolderItemCount: false);
            if (item is null)
            {
                continue;
            }

            item.SortOrder = configItem.SortOrder;
            loaded.Add(item);
            surviving.Add(new WidgetItemConfig
            {
                Path = fullPath,
                SortOrder = configItem.SortOrder
            });
        }

        if (surviving.Count != Config.Items.Count)
        {
            Config.Items = surviving;
            _settingsService.SaveDebounced(notifySubscribers: false);
        }

        ApplyPersistedAddedTimes(loaded);
        // loaded 已按 Config.Items 排序，必须应用该顺序（不能走 Manual 保序分支）。
        SyncFolderItems(loaded, preserveManualOrder: false);
        SortItems();
        StartItemHydration();
    }

    private string CreateAvailableManagedFolderName(string displayName, string widgetId)
    {
        string baseFolderName = FileService.SanitizeFileSystemName(displayName);
        if (string.IsNullOrWhiteSpace(baseFolderName))
        {
            baseFolderName = _localizationService.T("Widget.ManagedFolderBaseName");
        }

        string rootPath = SettingsService.NormalizeManagedStorageRootPath(_settingsService.Settings.DefaultManagedStorageRootPath);
        var usedNames = _settingsService.Settings.Widgets
            .Where(widget => widget.WidgetKind == WidgetKind.File &&
                             widget.FollowsDefaultStoragePath &&
                             !string.IsNullOrWhiteSpace(widget.ManagedFolderName) &&
                             !string.Equals(widget.Id, widgetId, StringComparison.Ordinal))
            .Select(widget => widget.ManagedFolderName!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string candidate = baseFolderName;
        int suffix = 2;
        while (usedNames.Contains(candidate) || Directory.Exists(Path.Combine(rootPath, candidate)))
        {
            candidate = $"{baseFolderName} ({suffix++})";
        }

        return candidate;
    }

    private async Task LoadFolderContentsAsync(string folderPath, bool clearIconCacheBeforeHydration = false)
    {
        using var perfScope = PerformanceLogger.Measure(
            "WidgetViewModel.LoadFolderContents",
            $"id={Config.Id} path={folderPath}");

        List<WidgetItem> items;
        var (userDesktop, publicDesktop) = FileService.GetDesktopPaths();
        if (folderPath.Equals(userDesktop, StringComparison.OrdinalIgnoreCase))
        {
            var userItems = await _fileService.EnumerateDirectoryAsync(
                userDesktop,
                hideShortcutArrowOverlay: _hideShortcutArrowOverlay,
                showImageFilesAsIcons: _showImageFilesAsIcons,
                showFileExtensions: _showFileExtensions,
                hideShortcutExtensionWhenShowingFileExtensions: _hideShortcutExtensionWhenShowingFileExtensions,
                loadIcons: false,
                loadFolderItemCounts: false);
            var publicItems = await _fileService.EnumerateDirectoryAsync(
                publicDesktop,
                hideShortcutArrowOverlay: _hideShortcutArrowOverlay,
                showImageFilesAsIcons: _showImageFilesAsIcons,
                showFileExtensions: _showFileExtensions,
                hideShortcutExtensionWhenShowingFileExtensions: _hideShortcutExtensionWhenShowingFileExtensions,
                loadIcons: false,
                loadFolderItemCounts: false);

            items = userItems.Concat(publicItems)
                .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(item => !item.IsFolder)
                .ThenBy(item => item.Name, NaturalStringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        else
        {
            items = await _fileService.EnumerateDirectoryAsync(
                folderPath,
                hideShortcutArrowOverlay: _hideShortcutArrowOverlay,
                showImageFilesAsIcons: _showImageFilesAsIcons,
                showFileExtensions: _showFileExtensions,
                hideShortcutExtensionWhenShowingFileExtensions: _hideShortcutExtensionWhenShowingFileExtensions,
                loadIcons: false,
                loadFolderItemCounts: false);
        }

        ApplyPersistedAddedTimes(items);
        if (Config.SortMode == WidgetSortMode.Manual)
        {
            items = ApplyManualConfigOrder(items);
        }

        SyncFolderItems(items);
        SortItems();
        if (clearIconCacheBeforeHydration)
        {
            ClearCurrentItemIconCache();
        }

        StartItemHydration();
    }

    /// <summary>
    /// Orders folder enumeration results by persisted <see cref="WidgetConfig.Items"/>
    /// when SortMode is Manual. Unknown new files are appended.
    /// </summary>
    private List<WidgetItem> ApplyManualConfigOrder(List<WidgetItem> items)
    {
        if (items.Count == 0 || Config.Items.Count == 0)
        {
            return items;
        }

        var byPath = items
            .GroupBy(item => Path.GetFullPath(item.Path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var ordered = new List<WidgetItem>(items.Count);
        foreach (var configItem in Config.Items
                     .Where(item => !string.IsNullOrWhiteSpace(item.Path))
                     .OrderBy(item => item.SortOrder))
        {
            string fullPath = Path.GetFullPath(configItem.Path);
            if (byPath.Remove(fullPath, out var item))
            {
                ordered.Add(item);
            }
        }

        foreach (var item in items)
        {
            string fullPath = Path.GetFullPath(item.Path);
            if (byPath.Remove(fullPath, out var remaining))
            {
                ordered.Add(remaining);
            }
        }

        return ordered;
    }

    private void SyncFolderItems(
        IReadOnlyList<WidgetItem> refreshedItems,
        bool preserveManualOrder = true)
    {
        var existingByPath = Items
            .GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var refreshedPaths = refreshedItems
            .Select(item => item.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (int index = Items.Count - 1; index >= 0; index--)
        {
            if (!refreshedPaths.Contains(Items[index].Path))
            {
                Items.RemoveAt(index);
            }
        }

        // Manual 模式：只增删/更新元数据，不按刷新列表重排，避免拖放后被目录枚举顺序冲掉。
        if (preserveManualOrder && Config.SortMode == WidgetSortMode.Manual)
        {
            foreach (var refreshedItem in refreshedItems)
            {
                if (existingByPath.TryGetValue(refreshedItem.Path, out var existingItem))
                {
                    ApplyRuntimeItemData(existingItem, refreshedItem);
                    continue;
                }

                Items.Add(refreshedItem);
            }

            NormalizeSortOrder();
            return;
        }

        for (int targetIndex = 0; targetIndex < refreshedItems.Count; targetIndex++)
        {
            var refreshedItem = refreshedItems[targetIndex];
            if (!existingByPath.TryGetValue(refreshedItem.Path, out var existingItem))
            {
                Items.Insert(targetIndex, refreshedItem);
                continue;
            }

            ApplyRuntimeItemData(existingItem, refreshedItem);
            int currentIndex = Items.IndexOf(existingItem);
            if (currentIndex < 0)
            {
                Items.Insert(targetIndex, existingItem);
            }
            else if (currentIndex != targetIndex)
            {
                Items.Move(currentIndex, targetIndex);
            }
        }

        NormalizeSortOrder();
    }

    private void StartItemHydration()
    {
        int generation = Interlocked.Increment(ref _itemHydrationGeneration);
        _ = HydrateIconsWithRetryAsync(generation);
        _ = HydrateFolderItemCountsAsync(generation);
        _ = HydrateShellKindsAsync(generation);
    }

    private void ClearCurrentItemIconCache()
    {
        foreach (var item in Items)
        {
            if (!string.IsNullOrWhiteSpace(item.Path))
            {
                item.Icon = null;
                _fileService.ClearIconCache(item.Path, _hideShortcutArrowOverlay, _showImageFilesAsIcons);
            }
        }
    }

    private void RefreshAllIcons()
    {
        ClearCurrentItemIconCache();
        StartItemHydration();
    }

    private async Task HydrateIconsWithRetryAsync(int generation)
    {
        await HydrateIconsAsync(generation, clearCacheBeforeLoad: false);

        for (int retry = 0; retry < IconHydrationRetryCount; retry++)
        {
            if (generation != Volatile.Read(ref _itemHydrationGeneration) ||
                !Items.Any(item => item.Icon is null))
            {
                return;
            }

            await Task.Delay(s_iconHydrationRetryDelays[Math.Min(retry, s_iconHydrationRetryDelays.Length - 1)]);
            await HydrateIconsAsync(generation, clearCacheBeforeLoad: true);
        }
    }

    private async Task HydrateIconsAsync(int generation, bool clearCacheBeforeLoad)
    {
        var items = Items
            .Where(item => item.Icon is null)
            .OrderByDescending(item => item.IsShortcut)
            .ThenBy(item => item.SortOrder)
            .ToList();

        for (int start = 0; start < items.Count; start += IconHydrationBatchSize)
        {
            if (generation != Volatile.Read(ref _itemHydrationGeneration))
            {
                return;
            }

            var batch = items
                .Skip(start)
                .Take(IconHydrationBatchSize)
                .Where(item => Items.Contains(item) && !string.IsNullOrWhiteSpace(item.Path))
                .Select(item => HydrateIconAsync(item, generation, clearCacheBeforeLoad))
                .ToArray();
            var results = await Task.WhenAll(batch);

            foreach (var (item, icon) in results)
            {
                if (item is null)
                {
                    continue;
                }

                if (generation != Volatile.Read(ref _itemHydrationGeneration) ||
                    !Items.Contains(item))
                {
                    return;
                }

                SetItemIcon(item, icon, item.Path, generation);
            }

            await Task.Yield();
        }
    }

    private async Task<(WidgetItem? Item, Microsoft.UI.Xaml.Media.Imaging.BitmapImage? Icon)> HydrateIconAsync(
        WidgetItem item,
        int generation,
        bool clearCacheBeforeLoad)
    {
        string path = item.Path;
        if (string.IsNullOrWhiteSpace(path))
        {
            return (null, null);
        }

        try
        {
            if (clearCacheBeforeLoad)
            {
                _fileService.ClearIconCache(path, _hideShortcutArrowOverlay, _showImageFilesAsIcons);
            }

            var icon = await _fileService.GetIconAsync(path, _hideShortcutArrowOverlay, _showImageFilesAsIcons);
            return (item, icon);
        }
        catch (Exception ex)
        {
            App.Log($"[IconHydration] Failed to load icon for '{path}' in widget '{Name}' ({Config.Id}): {ex.Message}");
            return (item, null);
        }
    }

    private async Task HydrateFolderItemCountsAsync(int generation)
    {
        var folders = Items
            .Where(item => item.IsFolder && !item.IsFolderItemCountLoaded)
            .ToList();
        int processed = 0;

        foreach (var item in folders)
        {
            if (generation != Volatile.Read(ref _itemHydrationGeneration) ||
                !Items.Contains(item) ||
                !Directory.Exists(item.Path))
            {
                return;
            }

            string path = item.Path;
            try
            {
                int count = await _fileService.CountVisibleChildrenAsync(path);
                SetFolderItemCount(item, count, path, generation);
            }
            catch
            {
                SetFolderItemCount(item, 0, path, generation);
            }
            processed++;

            if (processed % FolderCountHydrationBatchSize == 0)
            {
                await Task.Delay(FolderCountHydrationYieldMs);
            }
        }
    }

    private async Task HydrateShellKindsAsync(int generation)
    {
        var items = Items
            .Where(item => !item.IsShellKindLoaded)
            .OrderBy(item => item.SortOrder)
            .ToList();

        for (int start = 0; start < items.Count; start += ShellKindHydrationBatchSize)
        {
            if (generation != Volatile.Read(ref _itemHydrationGeneration))
            {
                return;
            }

            var batch = items
                .Skip(start)
                .Take(ShellKindHydrationBatchSize)
                .Where(item => Items.Contains(item) && !string.IsNullOrWhiteSpace(item.Path))
                .Select(async item =>
                {
                    string expectedPath = item.Path;
                    string kind = await _fileService.GetShellKindAsync(item);
                    return (Item: item, ExpectedPath: expectedPath, Kind: kind);
                })
                .ToArray();
            var results = await Task.WhenAll(batch);

            foreach (var result in results)
            {
                SetShellKind(
                    result.Item,
                    result.Kind,
                    result.ExpectedPath,
                    generation);
            }

            await Task.Yield();
        }
    }

    private void SetItemIcon(
        WidgetItem item,
        Microsoft.UI.Xaml.Media.Imaging.BitmapImage? icon,
        string expectedPath,
        int generation)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            if (CanApplyHydrationResult(item, expectedPath, generation))
            {
                item.Icon = icon;
            }

            return;
        }

        _dispatcherQueue.TryEnqueue(() =>
        {
            if (CanApplyHydrationResult(item, expectedPath, generation))
            {
                item.Icon = icon;
            }
        });
    }

    private void SetFolderItemCount(WidgetItem item, int count, string expectedPath, int generation)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            if (CanApplyHydrationResult(item, expectedPath, generation))
            {
                item.FolderItemCount = count;
                item.IsFolderItemCountLoaded = true;
            }

            return;
        }

        _dispatcherQueue.TryEnqueue(() =>
        {
            if (CanApplyHydrationResult(item, expectedPath, generation))
            {
                item.FolderItemCount = count;
                item.IsFolderItemCountLoaded = true;
            }
        });
    }

    private void SetShellKind(WidgetItem item, string kind, string expectedPath, int generation)
    {
        void Apply()
        {
            if (!CanApplyHydrationResult(item, expectedPath, generation))
            {
                return;
            }

            bool categoryMayChange = !string.Equals(item.ShellKind, kind, StringComparison.OrdinalIgnoreCase);
            item.ShellKind = kind;
            item.IsShellKindLoaded = true;
            if (categoryMayChange && FileStackGroupBy == SettingsService.FileStackGroupByKind)
            {
                QueueStackDisplayRebuild();
            }
        }

        if (_dispatcherQueue.HasThreadAccess)
        {
            Apply();
        }
        else
        {
            _dispatcherQueue.TryEnqueue(Apply);
        }
    }

    private bool CanApplyHydrationResult(WidgetItem item, string expectedPath, int generation)
    {
        return generation == Volatile.Read(ref _itemHydrationGeneration) &&
               Items.Contains(item) &&
               string.Equals(item.Path, expectedPath, StringComparison.OrdinalIgnoreCase);
    }

    private async Task RefreshShortcutIconsAsync()
    {
        int shortcutCount = Items.Count(item => item.IsShortcut);
        using var perfScope = PerformanceLogger.Measure(
            "WidgetViewModel.RefreshShortcutIcons",
            $"id={Config.Id} count={shortcutCount}");

        foreach (var item in Items.Where(item => item.IsShortcut))
        {
            item.Icon = await _fileService.GetIconAsync(item.Path, _hideShortcutArrowOverlay, _showImageFilesAsIcons);
        }
    }

    private void RefreshItemDisplayNames()
    {
        foreach (var item in Items)
        {
            item.Name = FileService.GetDisplayName(
                item.Path,
                item.IsFolder,
                _showFileExtensions,
                _hideShortcutExtensionWhenShowingFileExtensions);
        }

        SortItems();
    }
}
