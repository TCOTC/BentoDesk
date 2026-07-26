using System.Globalization;
using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BentoDesk.Helpers;
using BentoDesk.Models;
using BentoDesk.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace BentoDesk.ViewModels;

public partial class SettingsViewModel
{
    partial void OnTodoEnabledChanged(bool value)
    {
        if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
        {
            return;
        }

        FeatureWidgetSettings.SetEnabled(_settingsService.Settings, WidgetKind.Todo, value);
        _ = SyncTodoEnabledAsync(value);
        OnPropertyChanged(nameof(FeatureWidgetEntries));
        App.Current?.TodoReminderService?.Refresh();
    }

    partial void OnTodoShowTabBarChanged(bool value) => PersistTodoTabSettings();
    partial void OnTodoShowAllTabChanged(bool value) => PersistTodoTabSettings();
    partial void OnTodoShowActiveTabChanged(bool value) => PersistTodoTabSettings();
    partial void OnTodoShowTodayTabChanged(bool value) => PersistTodoTabSettings();
    partial void OnTodoShowThisWeekTabChanged(bool value) => PersistTodoTabSettings();
    partial void OnTodoShowThisMonthTabChanged(bool value) => PersistTodoTabSettings();
    partial void OnTodoShowImportantTabChanged(bool value) => PersistTodoTabSettings();
    partial void OnTodoShowCompletedTabChanged(bool value) => PersistTodoTabSettings();

    private void PersistTodoTabSettings()
    {
        if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
        {
            return;
        }

        var settings = _settingsService.Settings;
        settings.TodoShowTabBar = TodoShowTabBar;
        settings.TodoShowAllTab = TodoShowAllTab;
        settings.TodoShowActiveTab = TodoShowActiveTab;
        settings.TodoShowTodayTab = TodoShowTodayTab;
        settings.TodoShowThisWeekTab = TodoShowThisWeekTab;
        settings.TodoShowThisMonthTab = TodoShowThisMonthTab;
        settings.TodoShowImportantTab = TodoShowImportantTab;
        settings.TodoShowCompletedTab = TodoShowCompletedTab;
        if (!TodoShowAllTab && !TodoShowActiveTab && !TodoShowTodayTab &&
            !TodoShowThisWeekTab && !TodoShowThisMonthTab &&
            !TodoShowImportantTab && !TodoShowCompletedTab)
        {
            _isApplyingSettingsSnapshot = true;
            try
            {
                TodoShowAllTab = true;
            }
            finally
            {
                _isApplyingSettingsSnapshot = false;
            }

            settings.TodoShowAllTab = true;
        }

        if (!SettingsService.IsTodoTabVisible(settings, settings.TodoDefaultFilter))
        {
            SelectedTodoDefaultFilter = SettingsService.GetFirstVisibleTodoTab(settings);
        }

        _settingsService.SaveDebounced();
    }

    partial void OnTodoShowCompletedTasksChanged(bool value)
    {
        if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
        {
            return;
        }

        _settingsService.Settings.TodoShowCompletedTasks = value;
        _settingsService.SaveDebounced();
    }

    partial void OnTodoShowFooterStatsChanged(bool value)
    {
        if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
        {
            return;
        }

        _settingsService.Settings.TodoShowFooterStats = value;
        _settingsService.SaveDebounced();
    }

    partial void OnTodoShowClearCompletedButtonChanged(bool value)
    {
        if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
        {
            return;
        }

        _settingsService.Settings.TodoShowClearCompletedButton = value;
        _settingsService.SaveDebounced();
    }

    partial void OnTodoConfirmBeforeDeleteChanged(bool value)
    {
        if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
        {
            return;
        }

        _settingsService.Settings.TodoConfirmBeforeDelete = value;
        _settingsService.SaveDebounced();
    }

    partial void OnTodoReminderEnabledChanged(bool value)
    {
        if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
        {
            return;
        }

        _settingsService.Settings.TodoReminderEnabled = value;
        _settingsService.SaveDebounced();
        App.Current?.TodoReminderService?.Refresh();
        if (value && App.Current?.TodoReminderService is { } reminderService)
        {
            _ = reminderService.CheckNowAsync(DateTimeOffset.Now);
        }
    }

    partial void OnMusicUseArtworkBackdropChanged(bool value)
    {
        if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
        {
            return;
        }

        _settingsService.Settings.MusicUseArtworkBackdrop = value;
        _settingsService.SaveDebounced();
    }

    partial void OnMusicEnableCoverHoverMotionChanged(bool value)
    {
        if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
        {
            return;
        }

        _settingsService.Settings.MusicEnableCoverHoverMotion = value;
        _settingsService.SaveDebounced();
    }

    private async Task SyncTodoEnabledAsync(bool enabled)
    {
        try
        {
            if (App.Current?.WidgetManager is { } widgetManager)
            {
                await widgetManager.SetFeatureWidgetEnabledAsync(WidgetKind.Todo, enabled, reveal: enabled);
                return;
            }

            await _settingsService.SaveAsync();
        }
        catch (Exception ex)
        {
            App.Log($"[SettingsViewModel] Failed to sync Todo enabled state: {ex}");
        }
        finally
        {
            OnPropertyChanged(nameof(FeatureWidgetEntries));
        }
    }
}
