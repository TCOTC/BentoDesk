using BentoDesk.Helpers;
using BentoDesk.Models;
using BentoDesk.Services;
using BentoDesk.ViewModels;
using System.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Shapes;
using System.Runtime.InteropServices;
using Windows.System;
using WinRT.Interop;

namespace BentoDesk.Views;

public sealed partial class SettingsWindow
{
    private void OpenRepositoryButton_Click(object sender, RoutedEventArgs e)
    {
        Win32Helper.OpenFile(ViewModel.OpenSourceRepositoryUrl);
    }

    private async void OneClickUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        // If update is already downloaded, show install confirmation dialog.
        if (ViewModel.IsUpdateDownloaded)
        {
            if (SettingsRoot.XamlRoot is null) return;

            var dialog = new ContentDialog
            {
                XamlRoot = SettingsRoot.XamlRoot,
                Title = _localizationService.T("Settings.Update.InstallConfirmTitle"),
                Content = new TextBlock
                {
                    Text = _localizationService.T("Settings.Update.InstallConfirmBody"),
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = _localizationService.T("Settings.Update.OneClick.Install"),
                CloseButtonText = _localizationService.T("Common.Cancel"),
                DefaultButton = ContentDialogButton.Primary
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            var result = ViewModel.StartDownloadedUpdateInstall();
            if (!result.Success)
            {
                await ShowInfoDialogAsync(
                    _localizationService.T("Settings.Update.InstallStartFailedTitle"),
                    result.ErrorMessage ?? _localizationService.T("Settings.Update.InstallStartFailedBody"));
                return;
            }

            await App.Current.ShutdownForUpdateAsync();
            return;
        }

        // Otherwise, trigger one-click check → download flow.
        await ViewModel.OneClickUpdateActionAsync();
    }

    private void OpenManualUpdateDownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(ViewModel.UpdateFallbackUrl))
        {
            Win32Helper.OpenFile(ViewModel.UpdateFallbackUrl);
        }
    }
}
