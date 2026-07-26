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
    public GlobalHotkeyGesture GetCurrentGlobalHotkeyGesture()
    {
        var settings = _settingsService.Settings;
        return GlobalHotkeyService.NormalizeGesture(settings.GlobalHotkeyModifiers, settings.GlobalHotkeyKey);
    }

    public void RefreshGlobalHotkeyState()
    {
        var settings = _settingsService.Settings;
        _globalHotkeyEnabled = settings.GlobalHotkeyEnabled;
        GlobalHotkeyText = GlobalHotkeyService.FormatGesture(GetCurrentGlobalHotkeyGesture(), _localizationService);
        RefreshGlobalHotkeyStatus();
        OnPropertyChanged(nameof(GlobalHotkeyEnabled));
        OnPropertyChanged(nameof(CanShowGlobalHotkeyWarning));
        OnPropertyChanged(nameof(GlobalHotkeyDescription));
        OnPropertyChanged(nameof(GlobalHotkeyText));
        OnPropertyChanged(nameof(GlobalHotkeyStatusText));
        OnPropertyChanged(nameof(GlobalHotkeyStatusKind));
    }

    public void RefreshGlobalHotkeyStatus()
    {
        if (!GlobalHotkeyEnabled)
        {
            GlobalHotkeyStatusKind = "Muted";
            GlobalHotkeyStatusText = _localizationService.T("Settings.GlobalHotkey.Status.Disabled");
            return;
        }

        if (App.Current?.GlobalHotkeyService is not { } hotkeyService)
        {
            GlobalHotkeyStatusKind = "Warning";
            GlobalHotkeyStatusText = _localizationService.T("Settings.GlobalHotkey.Status.Unavailable");
            return;
        }

        if (hotkeyService.IsRegistered)
        {
            GlobalHotkeyStatusKind = GlobalHotkeyService.IsRiskyGesture(hotkeyService.CurrentGesture) ? "Warning" : "Normal";
            GlobalHotkeyStatusText = _localizationService.Format(
                "Settings.GlobalHotkey.Status.Active",
                hotkeyService.CurrentGestureText);
            return;
        }

        GlobalHotkeyStatusKind = "Warning";
        GlobalHotkeyStatusText = string.IsNullOrWhiteSpace(hotkeyService.LastError)
            ? _localizationService.T("Settings.GlobalHotkey.Status.Unregistered")
            : hotkeyService.LastError;
    }
}
