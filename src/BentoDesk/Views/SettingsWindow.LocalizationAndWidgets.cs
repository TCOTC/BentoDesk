using BentoDesk.Helpers;
using BentoDesk.Services;
using BentoDesk.ViewModels;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace BentoDesk.Views;

public sealed partial class SettingsWindow
{
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
    }

    private void OnLanguageChanged()
    {
        RefreshLocalizedContent();
    }

    public void RefreshLocalizedContent()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(RefreshLocalizedContent);
            return;
        }

        if (_isClosed)
        {
            return;
        }

        ApplyLocalizedText();
    }

    private void ApplyLocalizedText()
    {
        Title = _localizationService.T("Settings.WindowTitle");
        Localized.RefreshAll(_localizationService);
        RefreshSettingsSearchResults();
        ApplyToggleSwitchContentVisibility();
        ViewModel.RefreshGlobalHotkeyState();
        RefreshGlobalHotkeyControls();
        if (TryGetSectionRoute(_currentSettingsSection, out SettingsSectionRoute? route))
        {
            UpdateBreadcrumb(route);
        }
        if (string.Equals(_currentSettingsSection, "BackupRestoreSettings", StringComparison.Ordinal))
        {
            _ = RefreshBackupSnapshotInventoryAsync();
        }
    }

    private void ApplyToggleSwitchContentVisibility()
    {
        foreach (var toggle in FindDescendants<ToggleSwitch>(SettingsRoot))
        {
            ClearToggleSwitchContent(toggle);
        }
    }

    private static void ClearToggleSwitchContent(ToggleSwitch toggle)
    {
        toggle.OnContent = string.Empty;
        toggle.OffContent = string.Empty;
    }
}
