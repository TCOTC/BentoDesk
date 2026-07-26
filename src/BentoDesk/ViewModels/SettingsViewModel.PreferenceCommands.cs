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
    public Color GetCurrentAccentColor() => _currentAccentColor;

    public bool SuppressAppearanceNotifications { get; set; }
    public bool DeferAppearancePersistence { get; set; }

    public void CommitAppearanceChanges()
    {
        _settingsService.NotifyAppearancePreviewNow();
        // The preview notification has already updated every widget with the
        // final slider value. Persist without broadcasting the same appearance
        // pass a second time through SettingsChanged.
        _settingsService.SaveDebounced(notifySubscribers: false);
        App.ScheduleLightMemoryCleanup();
    }

    public void SetCustomAccentColor(Color color)
    {
        _themeService.SetCustomAccentColor(color);

        if (UseSystemAccentColor)
        {
            _useSystemAccentColor = false;
            OnPropertyChanged(nameof(UseSystemAccentColor));
            OnPropertyChanged(nameof(CanEditCustomAccent));
            OnPropertyChanged(nameof(AccentColorDescription));
        }

        RefreshAccentPreview();
    }

    public async Task RestoreDefaultPreferencesAsync()
    {
        _isRestoringDefaults = true;
        SuppressAppearanceNotifications = true;
        DeferAppearancePersistence = false;

        try
        {
            SettingsService.ApplyDefaultPreferences(_settingsService.Settings);
            ApplySettingsSnapshot();
            IconHelper.ClearAllThumbnailCaches();

            if (App.Current is { } app)
            {
                app.ResizeGuideOverlay.IsSnapEnabled = _settingsService.Settings.ResizeSnapEnabled;
            }

            App.Current?.GlobalHotkeyService?.RefreshRegistration();
            App.Current?.DesktopDoubleClickService?.RefreshRegistration();
            App.Current?.UpdateTrayIcon();
            RefreshGlobalHotkeyState();
            _themeService.RefreshAppearance();
            RefreshAccentPreview();
            await _settingsService.SaveAsync();
            _settingsService.NotifyAppearancePreviewNow();
        }
        finally
        {
            SuppressAppearanceNotifications = false;
            _isRestoringDefaults = false;
        }
    }

}
