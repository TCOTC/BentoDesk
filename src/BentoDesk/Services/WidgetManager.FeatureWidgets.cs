// Copyright (c) BentoDesk. All rights reserved.

using BentoDesk.Models;
using BentoDesk.Helpers;
using BentoDesk.Controls.WidgetContents;
using BentoDesk.ViewModels;
using BentoDesk.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace BentoDesk.Services;

/// <summary>
/// Partial class containing FeatureWidgets logic for WidgetManager.
/// </summary>
public sealed partial class WidgetManager
{

    private readonly Dictionary<WidgetKind, bool> _lastFeatureWidgetEnabledStates = new();
    private readonly Dictionary<WidgetKind, FeatureWidgetHandler> _featureWidgetHandlers;
    private readonly Dictionary<WidgetKind, WidgetWindowProvider> _windowProviders;
    private bool _isApplyingAppearancePreview;

    private void ApplyFeatureWidgetEnabledState(WidgetKind kind, bool enabled)
    {
        if (App.UiDispatcherQueue is { } dispatcherQueue && !dispatcherQueue.HasThreadAccess)
        {
            dispatcherQueue.TryEnqueue(() => ApplyFeatureWidgetEnabledState(kind, enabled));
            return;
        }

        if (!enabled)
        {
            if (_featureWidgetHandlers.TryGetValue(kind, out var handler))
            {
                handler.HideLoaded();
            }
            else
            {
                HideAndCloseFeatureWidgetAsync(kind);
            }

            return;
        }

        CreateOrShowFeatureWidgetAsync(kind).ContinueWith(
            task =>
            {
                if (task.Exception is not null)
                {
                    App.Log($"[WidgetManager] Failed to show feature widget after enabling kind={kind}: {task.Exception}");
                }
            },
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private string GetDefaultFeatureWidgetTitle(WidgetKind kind, WidgetContentDescriptor descriptor)
    {
        string key = kind switch
        {
            WidgetKind.Music => "Music.Title",
            WidgetKind.Search => "Search.Title",
            WidgetKind.Tags => "Tags.Title",
            WidgetKind.SystemMonitor => "SystemMonitor.Title",
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(key))
        {
            string localized = _localizationService.T(key);
            if (!string.IsNullOrWhiteSpace(localized))
            {
                return localized;
            }
        }

        return descriptor.DefaultTitle;
    }

    private async Task<ContentWidgetWindow> CreateSingletonContentFeatureWidgetAsync(WidgetKind kind)
    {
        if (!IsContentFeatureWidgetKind(kind))
        {
            throw new NotSupportedException($"Widget kind '{kind}' is not a content feature widget.");
        }

        SetFeatureWidgetEnabledState(kind, true);

        var existingConfig = _settingsService.Settings.Widgets
            .FirstOrDefault(w => w.WidgetKind == kind && !IsDeleted(w.Id));
        if (existingConfig is not null)
        {
            await ShowWidgetAsync(existingConfig.Id, reveal: true, autoRestoreOnReveal: false);
            if (_contentWidgets.TryGetValue(existingConfig.Id, out var existing))
            {
                return existing;
            }
        }

        var descriptor = new WidgetContentFactory(_localizationService).GetDescriptor(kind);
        var config = new WidgetConfig
        {
            Name = GetDefaultFeatureWidgetTitle(kind, descriptor),
            WidgetKind = kind,
            BoundsCoordinateVersion = WidgetConfig.CurrentBoundsCoordinateVersion,
            Width = kind switch
            {
                WidgetKind.Music => 380,
                WidgetKind.Search => 280,
                _ => Math.Max(_settingsService.Settings.DefaultWidgetWidth, 320)
            },
            Height = kind switch
            {
                WidgetKind.Music => 190,
                WidgetKind.Search => 90,
                _ => Math.Max(_settingsService.Settings.DefaultWidgetHeight, 360)
            }
        };

        _settingsService.Settings.Widgets.Add(config);
        await _settingsService.SaveAsync();

        return await CreateContentWidgetFromConfigAsync(config, revealAfterCreate: true);
    }

    internal int RepairLegacyContentFeatureFileShells()
    {
        if (!FeatureWidgetSettings.IsEnabled(_settingsService.Settings, WidgetKind.Music))
        {
            return 0;
        }

        bool hasMusicConfig = _settingsService.Settings.Widgets.Any(widget =>
            widget.WidgetKind == WidgetKind.Music &&
            !IsDeleted(widget.Id));
        if (!hasMusicConfig)
        {
            return 0;
        }

        var fileShells = _settingsService.Settings.Widgets
            .Where(IsLegacyEmptyContentFeatureFileShell)
            .ToList();
        if (fileShells.Count == 0)
        {
            return 0;
        }

        foreach (var shell in fileShells)
        {
            _settingsService.Settings.Widgets.Remove(shell);
            if (!_settingsService.Settings.DeletedWidgetIds.Contains(shell.Id))
            {
                _settingsService.Settings.DeletedWidgetIds.Add(shell.Id);
            }

            App.Log($"[WidgetManager] Repaired legacy empty Music file shell: {FormatWidget(shell)}");
        }

        _settingsService.SaveDebounced();
        return fileShells.Count;
    }

    private bool IsLegacyEmptyContentFeatureFileShell(WidgetConfig widget)
    {
        return widget.WidgetKind == WidgetKind.File &&
               string.IsNullOrWhiteSpace(widget.MappedFolderPath) &&
               !widget.FollowsDefaultStoragePath &&
               string.IsNullOrWhiteSpace(widget.ManagedFolderName) &&
               widget.Items.Count == 0 &&
               IsDefaultMusicTitle(widget.Name);
    }

    private bool IsDefaultMusicTitle(string title)
    {
        string normalized = title.Trim();
        return string.Equals(normalized, "Music", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "\u97F3\u4E50", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, _localizationService.T("Music.Title"), StringComparison.OrdinalIgnoreCase);
    }

    private void DeduplicateFeatureWidgets()
    {
        var seen = new HashSet<WidgetKind>();
        var toRemove = new List<string>();

        foreach (var config in _settingsService.Settings.Widgets.ToList())
        {
            if (config.WidgetKind == WidgetKind.File) continue;
            if (IsDeleted(config.Id)) continue;

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

    internal IDesktopWidgetWindow? GetFeatureWidget(WidgetKind kind)
    {
        return _contentWidgets.Values
            .FirstOrDefault(w => w.Config.WidgetKind == kind);
    }

    internal bool IsFeatureWidgetEnabled(WidgetKind kind)
    {
        return FeatureWidgetSettings.IsFeatureWidget(kind)
            ? GetFeatureWidgetEnabledState(kind)
            : GetFeatureWidget(kind)?.Visible == true;
    }

    internal async Task<IDesktopWidgetWindow?> CreateOrShowFeatureWidgetAsync(WidgetKind kind)
    {
        if (!HasUiThreadAccess())
        {
            return await RunOnUiThreadAsync(() => CreateOrShowFeatureWidgetAsync(kind));
        }

        if (_featureWidgetHandlers.TryGetValue(kind, out var handler))
        {
            return await handler.CreateOrShowAsync(true);
        }

        App.Log($"[WidgetManager] CreateOrShowFeatureWidget: unsupported kind={kind}");
        return null;
    }

    public async Task SetFeatureWidgetEnabledAsync(WidgetKind kind, bool enabled, bool reveal = true)
    {
        if (!HasUiThreadAccess())
        {
            await RunOnUiThreadAsync(() => SetFeatureWidgetEnabledAsync(kind, enabled, reveal));
            return;
        }

        if (_featureWidgetHandlers.TryGetValue(kind, out var handler))
        {
            await handler.SetEnabledAsync(enabled, reveal);
            return;
        }

        App.Log($"[WidgetManager] SetFeatureWidgetEnabled: unsupported kind={kind}");
    }

    public async Task ResetFeatureWidgetAsync(WidgetKind kind)
    {
        if (!HasUiThreadAccess())
        {
            await RunOnUiThreadAsync(() => ResetFeatureWidgetAsync(kind));
            return;
        }

        if (!FeatureWidgetSettings.IsFeatureWidget(kind))
        {
            App.Log($"[WidgetManager] ResetFeatureWidget: unsupported kind={kind}");
            return;
        }

        var suppressedClosedIds = _settingsService.Settings.Widgets
            .Where(widget => widget.WidgetKind == kind)
            .Select(widget => widget.Id)
            .ToList();
        foreach (string id in suppressedClosedIds)
        {
            _suppressClosedVisibilityPersistence.Add(id);
        }

        try
        {
            CloseLoadedFeatureWidgetWindows(kind);

            var configs = _settingsService.Settings.Widgets
                .Where(widget => widget.WidgetKind == kind)
                .ToList();

            SetFeatureWidgetEnabledState(kind, false);
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
                App.Log($"[WidgetManager] ResetFeatureWidget removed duplicate kind={kind} id={duplicate.Id}");
            }

            if (config is null)
            {
                config = CreateDefaultFeatureWidgetConfig(kind, isEnabled: false);
                _settingsService.Settings.Widgets.Add(config);
            }
            else
            {
                ResetFeatureWidgetConfig(config, kind, isEnabled: false);
            }

            _settingsService.Settings.DeletedWidgetIds.RemoveAll(id =>
                string.Equals(id, config.Id, StringComparison.Ordinal));
            _deletedWidgetIds.Remove(config.Id);

            await _settingsService.SaveAsync();
            App.Log($"[WidgetManager] ResetFeatureWidget kind={kind} enabled=false id={config.Id}");
        }
        finally
        {
            foreach (string id in suppressedClosedIds)
            {
                _suppressClosedVisibilityPersistence.Remove(id);
            }
        }
    }

    private WidgetConfig CreateDefaultFeatureWidgetConfig(WidgetKind kind, bool isEnabled)
    {
        var config = new WidgetConfig();
        ResetFeatureWidgetConfig(config, kind, isEnabled);
        return config;
    }

    private void ResetFeatureWidgetConfig(WidgetConfig config, WidgetKind kind, bool isEnabled)
    {
        var descriptor = new WidgetContentFactory(_localizationService).GetDescriptor(kind);
        config.WidgetKind = kind;
        config.Name = GetDefaultFeatureWidgetTitle(kind, descriptor);
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
        (config.Width, config.Height) = GetDefaultFeatureWidgetSize(kind);
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

    private void CloseLoadedFeatureWidgetWindows(WidgetKind kind)
    {
        foreach (var window in _contentWidgets.Values
                     .Where(window => window.Config.WidgetKind == kind)
                     .ToList())
        {
            CloseFeatureWidgetInstance(window);
        }
    }

    private async Task SetContentFeatureWidgetEnabledAsync(WidgetKind kind, bool enabled, bool reveal = true)
    {
        SetFeatureWidgetEnabledState(kind, enabled);

        if (enabled)
        {
            if (reveal)
            {
                await CreateSingletonContentFeatureWidgetAsync(kind);
            }
            else
            {
                var config = _settingsService.Settings.Widgets
                    .FirstOrDefault(w => w.WidgetKind == kind && !IsDeleted(w.Id));
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
                     widget.WidgetKind == kind &&
                     !IsDeleted(widget.Id)))
        {
            config.IsVisible = false;
            config.IsDisabled = false;
        }

        HideAndCloseFeatureWidgetAsync(kind);
        await _settingsService.SaveAsync();
    }

    private Task SetSearchFeatureWidgetEnabledAsync(bool enabled, bool reveal)
    {
        return SetContentFeatureWidgetEnabledAsync(WidgetKind.Search, enabled, reveal);
    }

    private bool GetFeatureWidgetEnabledState(WidgetKind? kind)
    {
        return kind is { } featureKind &&
               FeatureWidgetSettings.IsFeatureWidget(featureKind) &&
               FeatureWidgetSettings.IsEnabled(_settingsService.Settings, featureKind);
    }

    private static bool IsContentFeatureWidgetKind(WidgetKind kind)
    {
        return FeatureWidgetSettings.IsFeatureWidget(kind);
    }

    private void SetFeatureWidgetEnabledState(WidgetKind kind, bool enabled)
    {
        FeatureWidgetSettings.SetEnabled(_settingsService.Settings, kind, enabled);
        _lastFeatureWidgetEnabledStates[kind] = enabled;
    }

    public void HideAndCloseFeatureWidgetAsync(WidgetKind kind)
    {
        var existing = GetFeatureWidget(kind);
        if (existing is not null)
        {
            CloseFeatureWidgetInstance(existing);
        }
    }

    private void CloseFeatureWidgetInstance(IDesktopWidgetWindow window)
    {
        if (!HasUiThreadAccess())
        {
            _ = RunOnUiThreadAsync(() =>
            {
                CloseFeatureWidgetInstance(window);
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

}
