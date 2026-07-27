// Copyright (c) BentoDesk. All rights reserved.

using BentoDesk.Models;
using BentoDesk.Helpers;
using BentoDesk.Controls.WidgetContents;
using BentoDesk.Services.WidgetKinds;
using BentoDesk.ViewModels;
using BentoDesk.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace BentoDesk.Services;

/// <summary>
/// Music widget enable/disable and singleton lifecycle for WidgetManager.
/// </summary>
public sealed partial class WidgetManager
{
    private bool _lastMusicWidgetEnabled;
    private readonly Dictionary<WidgetKind, WidgetWindowProvider> _windowProviders;
    private readonly WidgetKindHandlerRegistry _kindHandlers;
    private bool _isApplyingAppearancePreview;

    private void ApplyMusicWidgetEnabledState(bool enabled)
    {
        if (App.UiDispatcherQueue is { } dispatcherQueue && !dispatcherQueue.HasThreadAccess)
        {
            dispatcherQueue.TryEnqueue(() => ApplyMusicWidgetEnabledState(enabled));
            return;
        }

        if (!enabled)
        {
            HideAndCloseMusicWidget();
            return;
        }

        CreateOrShowMusicWidgetAsync().ContinueWith(
            task =>
            {
                if (task.Exception is not null)
                {
                    App.Log($"[WidgetManager] Failed to show music widget after enabling: {task.Exception}");
                }
            },
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private string GetDefaultWidgetTitle(WidgetKind kind, WidgetContentDescriptor descriptor)
    {
        if (_kindHandlers.TryGet(kind, out var handler) &&
            !string.IsNullOrWhiteSpace(handler.DefaultTitleLocalizationKey))
        {
            string localized = _localizationService.T(handler.DefaultTitleLocalizationKey);
            if (!string.IsNullOrWhiteSpace(localized))
            {
                return localized;
            }
        }

        return descriptor.DefaultTitle;
    }

    private async Task<ContentWidgetWindow> CreateOrShowMusicWidgetCoreAsync()
    {
        SetMusicWidgetEnabledState(true);

        var existingConfig = _settingsService.Settings.Widgets
            .FirstOrDefault(w => w.WidgetKind == WidgetKind.Music && !IsDeleted(w.Id));
        if (existingConfig is not null)
        {
            await ShowWidgetAsync(existingConfig.Id, reveal: true, autoRestoreOnReveal: false);
            if (_contentWidgets.TryGetValue(existingConfig.Id, out var existing))
            {
                return existing;
            }
        }

        var descriptor = new WidgetContentFactory(_localizationService).GetDescriptor(WidgetKind.Music);
        var handler = _kindHandlers.Get(WidgetKind.Music);
        (double width, double height) = handler.GetDefaultSize(_settingsService.Settings);
        var config = new WidgetConfig
        {
            Name = GetDefaultWidgetTitle(WidgetKind.Music, descriptor),
            WidgetKind = WidgetKind.Music,
            BoundsCoordinateVersion = WidgetConfig.CurrentBoundsCoordinateVersion,
            Width = width,
            Height = height
        };

        _settingsService.Settings.Widgets.Add(config);
        await _settingsService.SaveAsync();

        return await CreateContentWidgetFromConfigAsync(config, revealAfterCreate: true);
    }

    private void DeduplicateSingletonWidgets()
    {
        var seen = new HashSet<WidgetKind>();
        var toRemove = new List<string>();

        foreach (var config in _settingsService.Settings.Widgets.ToList())
        {
            if (IsDeleted(config.Id))
            {
                continue;
            }

            if (!_kindHandlers.TryGet(config.WidgetKind, out var handler) ||
                handler.SupportsMultiInstance)
            {
                continue;
            }

            if (!seen.Add(config.WidgetKind))
            {
                toRemove.Add(config.Id);
                App.Log($"[WidgetManager] Dedup: removing duplicate {config.WidgetKind} widget {config.Id}");
            }
        }

        if (toRemove.Count > 0)
        {
            foreach (var id in toRemove)
            {
                _settingsService.Settings.Widgets.RemoveAll(w => w.Id == id);
                _settingsService.Settings.DeletedWidgetIds.Add(id);
            }

            _settingsService.SaveDebounced();
        }
    }

    internal IDesktopWidgetWindow? GetMusicWidget()
    {
        return _contentWidgets.Values
            .FirstOrDefault(w => w.Config.WidgetKind == WidgetKind.Music);
    }

    internal bool IsMusicWidgetEnabled()
    {
        return _settingsService.Settings.MusicWidgetEnabled;
    }

    internal async Task<IDesktopWidgetWindow?> CreateOrShowMusicWidgetAsync()
    {
        if (!HasUiThreadAccess())
        {
            return await RunOnUiThreadAsync(CreateOrShowMusicWidgetAsync);
        }

        return await CreateOrShowMusicWidgetCoreAsync();
    }

    public async Task SetMusicWidgetEnabledAsync(bool enabled, bool reveal = true)
    {
        if (!HasUiThreadAccess())
        {
            await RunOnUiThreadAsync(() => SetMusicWidgetEnabledAsync(enabled, reveal));
            return;
        }

        await SetMusicWidgetEnabledCoreAsync(enabled, reveal);
    }

    /// <summary>
    /// Compatibility shim for callers still using the FeatureWidget naming.
    /// </summary>
    public Task SetFeatureWidgetEnabledAsync(WidgetKind kind, bool enabled, bool reveal = true)
    {
        if (kind != WidgetKind.Music)
        {
            App.Log($"[WidgetManager] SetFeatureWidgetEnabled: unsupported kind={kind}");
            return Task.CompletedTask;
        }

        return SetMusicWidgetEnabledAsync(enabled, reveal);
    }

    internal bool IsFeatureWidgetEnabled(WidgetKind kind)
    {
        return kind == WidgetKind.Music && IsMusicWidgetEnabled();
    }

    public async Task ResetMusicWidgetAsync()
    {
        if (!HasUiThreadAccess())
        {
            await RunOnUiThreadAsync(ResetMusicWidgetAsync);
            return;
        }

        var suppressedClosedIds = _settingsService.Settings.Widgets
            .Where(widget => widget.WidgetKind == WidgetKind.Music)
            .Select(widget => widget.Id)
            .ToList();
        foreach (string id in suppressedClosedIds)
        {
            _suppressClosedVisibilityPersistence.Add(id);
        }

        try
        {
            CloseLoadedMusicWidgetWindows();

            var configs = _settingsService.Settings.Widgets
                .Where(widget => widget.WidgetKind == WidgetKind.Music)
                .ToList();

            SetMusicWidgetEnabledState(false);
            var config = configs.FirstOrDefault(widget => !IsDeleted(widget.Id)) ??
                         configs.FirstOrDefault();

            foreach (var duplicate in configs.Where(widget => !ReferenceEquals(widget, config)).ToList())
            {
                _settingsService.Settings.Widgets.Remove(duplicate);
                if (!_settingsService.Settings.DeletedWidgetIds.Contains(duplicate.Id))
                {
                    _settingsService.Settings.DeletedWidgetIds.Add(duplicate.Id);
                }

                _deletedWidgetIds.Remove(duplicate.Id);
                App.Log($"[WidgetManager] ResetMusicWidget removed duplicate id={duplicate.Id}");
            }

            if (config is null)
            {
                config = CreateDefaultMusicWidgetConfig(isEnabled: false);
                _settingsService.Settings.Widgets.Add(config);
            }
            else
            {
                ResetMusicWidgetConfig(config, isEnabled: false);
            }

            _settingsService.Settings.DeletedWidgetIds.RemoveAll(id =>
                string.Equals(id, config.Id, StringComparison.Ordinal));
            _deletedWidgetIds.Remove(config.Id);

            await _settingsService.SaveAsync();
            App.Log($"[WidgetManager] ResetMusicWidget enabled=false id={config.Id}");
        }
        finally
        {
            foreach (string id in suppressedClosedIds)
            {
                _suppressClosedVisibilityPersistence.Remove(id);
            }
        }
    }

    private WidgetConfig CreateDefaultMusicWidgetConfig(bool isEnabled)
    {
        var config = new WidgetConfig();
        ResetMusicWidgetConfig(config, isEnabled);
        return config;
    }

    private void ResetMusicWidgetConfig(WidgetConfig config, bool isEnabled)
    {
        var descriptor = new WidgetContentFactory(_localizationService).GetDescriptor(WidgetKind.Music);
        var handler = _kindHandlers.Get(WidgetKind.Music);
        config.WidgetKind = WidgetKind.Music;
        config.Name = GetDefaultWidgetTitle(WidgetKind.Music, descriptor);
        config.IsDefaultTitle = true;
        config.X = 100;
        config.Y = 100;
        config.PositionAnchor = null;
        config.PositionMarginX = 0;
        config.PositionMarginY = 0;
        config.PositionMonitorKey = null;
        config.PositionMonitorDeviceName = null;
        config.PositionMonitorWasPrimary = null;
        config.BoundsCoordinateVersion = WidgetConfig.CurrentBoundsCoordinateVersion;
        (config.Width, config.Height) = handler.GetDefaultSize(_settingsService.Settings);
        config.ViewMode = ViewMode.Icon;
        config.IsVisible = isEnabled;
        config.IsDisabled = false;
        config.IsPositionLocked = false;
        config.IsSizeLocked = false;
        config.Metadata ??= [];
        config.Metadata.Clear();
        config.MappedFolderPath = null;
        config.FollowsDefaultStoragePath = false;
        config.ManagedFolderName = null;
        config.SortMode = WidgetSortMode.Name;
        config.SortDescending = false;
        config.Items ??= [];
        config.Items.Clear();
    }

    private void CloseLoadedMusicWidgetWindows()
    {
        foreach (var window in _contentWidgets.Values
                     .Where(window => window.Config.WidgetKind == WidgetKind.Music)
                     .ToList())
        {
            CloseContentWidgetInstance(window);
        }
    }

    private async Task SetMusicWidgetEnabledCoreAsync(bool enabled, bool reveal = true)
    {
        SetMusicWidgetEnabledState(enabled);

        if (enabled)
        {
            if (reveal)
            {
                await CreateOrShowMusicWidgetCoreAsync();
            }
            else
            {
                var config = _settingsService.Settings.Widgets
                    .FirstOrDefault(w => w.WidgetKind == WidgetKind.Music && !IsDeleted(w.Id));
                if (config is not null)
                {
                    config.IsDisabled = false;
                    config.IsVisible = true;
                }

                await _settingsService.SaveAsync();
            }

            return;
        }

        foreach (var config in _settingsService.Settings.Widgets.Where(widget =>
                     widget.WidgetKind == WidgetKind.Music &&
                     !IsDeleted(widget.Id)))
        {
            config.IsVisible = false;
            config.IsDisabled = false;
        }

        HideAndCloseMusicWidget();
        await _settingsService.SaveAsync();
    }

    private void SetMusicWidgetEnabledState(bool enabled)
    {
        _settingsService.Settings.MusicWidgetEnabled = enabled;
        _lastMusicWidgetEnabled = enabled;
    }

    public void HideAndCloseMusicWidget()
    {
        var existing = GetMusicWidget();
        if (existing is not null)
        {
            CloseContentWidgetInstance(existing);
        }
    }

    private void CloseContentWidgetInstance(IDesktopWidgetWindow window)
    {
        if (!HasUiThreadAccess())
        {
            _ = RunOnUiThreadAsync(() =>
            {
                CloseContentWidgetInstance(window);
                return Task.CompletedTask;
            });
            return;
        }

        window.Config.IsVisible = false;

        if (window.Config.WidgetKind == WidgetKind.File &&
            _widgets.TryGetValue(window.Config.Id, out var fileEntry) &&
            ReferenceEquals(fileEntry.Window, window))
        {
            _widgets.Remove(window.Config.Id);
            _widgetWindowHandles.Remove(window.WindowHandle);
            fileEntry.ViewModel.Dispose();
        }
        else if (_contentWidgets.TryGetValue(window.Config.Id, out var contentWindow) &&
                 ReferenceEquals(contentWindow, window))
        {
            _contentWidgets.Remove(window.Config.Id);
            _widgetWindowHandles.Remove(window.WindowHandle);
        }

        try
        {
            window.CloseWindow();
        }
        catch
        {
        }

        _settingsService.SaveDebounced();
    }

    private bool IsDetachedContentHost(WidgetKind kind)
    {
        return _kindHandlers.TryGet(kind, out var handler) &&
               handler.HostKind == WidgetWindowHostKind.DetachedContentWindow;
    }

    private (double Width, double Height) GetDefaultWidgetSize(WidgetKind kind)
    {
        if (_kindHandlers.TryGet(kind, out var handler))
        {
            return handler.GetDefaultSize(_settingsService.Settings);
        }

        return (
            Math.Max(_settingsService.Settings.DefaultWidgetWidth, 320),
            Math.Max(_settingsService.Settings.DefaultWidgetHeight, 360));
    }
}
