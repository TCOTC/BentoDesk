using BentoDesk.Contracts;
using BentoDesk.Controls;
using BentoDesk.Controls.WidgetContents;
using BentoDesk.Helpers;
using BentoDesk.Models;
using BentoDesk.Services;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT;
using WinRT.Interop;

namespace BentoDesk.Views;

public sealed partial class ContentWidgetWindow
{
    private void ApplyTitleBarLayout()
    {
        var chromeMode = _chromeModeResolver.Resolve(_config, _descriptor);
        double titleTextSize = chromeMode == WidgetChromeMode.Compact
            ? SettingsService.NormalizeTextSize(SettingsService.Settings.TextSize)
            : _titleViewModel.TitleTextSize;
        var metrics = WidgetTitleBarMetricsCalculator.Create(
            _titleViewModel.TitleIconSize,
            titleTextSize,
            includeInnerPadding: false,
            chromeMode);

        ContentWidgetShell.ChromeMode = chromeMode;
        ContentWidgetShell.TitleIconElement.IconSize = metrics.TitleIconSize;
        ContentWidgetShell.TitleTextElement.FontSize = metrics.TitleTextSize;
        ApplyLockActionIconState();

        WidgetTitleBarMetricsCalculator.ApplyActionButton(ContentWidgetShell.LockActionButton, metrics);
        WidgetTitleBarMetricsCalculator.ApplyActionButton(ContentWidgetShell.CollapseActionButton, metrics);

        WidgetActionIconHelper.ApplyPairSize(
            ContentWidgetShell.LockActionIcon,
            ContentWidgetShell.LockFilledActionIcon,
            metrics);

        ContentWidgetShell.SetTitleBarRowHeight(metrics.RowHeight);
        ContentWidgetShell.SetTitleBarPadding(WidgetTitleBarMetricsCalculator.CreateOuterPadding(chromeMode));
    }

    private void ApplyLockActionIconState()
    {
        WidgetActionIconHelper.ApplyLockState(
            ContentWidgetShell.LockActionIcon,
            ContentWidgetShell.LockFilledActionIcon,
            _config.IsPositionLocked && _config.IsSizeLocked);
    }

    // ── Button click handlers ──────────────────────────────────

    private void LockButton_Click(object sender, RoutedEventArgs e)
    {
        bool locked = !(_config.IsPositionLocked && _config.IsSizeLocked);
        SetPositionLocked(locked);
        SetSizeLocked(locked);
        ApplyLockActionIconState();
    }

    private void TitleBarGrid_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (ContentWidgetShell.IsCollapsed)
        {
            ShowFlyoutWithInteraction(CreateMoreFlyout(), ContentWidgetShell, e.GetPosition(ContentWidgetShell));
        }
        else
        {
            ShowFlyoutWithInteraction(CreateMoreFlyout(), ContentWidgetShell.TitleBar, e.GetPosition(ContentWidgetShell.TitleBar));
        }
        e.Handled = true;
    }

    private void ContentWidgetShell_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (ContentWidgetShell.ChromeMode is not (WidgetChromeMode.Overlay or WidgetChromeMode.Hidden))
        {
            return;
        }

        ShowFlyoutWithInteraction(CreateMoreFlyout(), ContentWidgetShell, e.GetPosition(ContentWidgetShell));
        e.Handled = true;
    }

    private void ContentWidgetShell_TitleDoubleTapped(object? sender, DoubleTappedRoutedEventArgs e)
    {
        e.Handled = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (IsDragging || IsResizing || ContentWidgetShell.TitleEditorContent is not null)
            {
                return;
            }

            StartTitleRename();
        });
    }

    // ── Flyout ─────────────────────────────────────────────────

    private MenuFlyout CreateMoreFlyout()
    {
        var flyout = new MenuFlyout();

        flyout.Items.Add(WidgetCollapseMenuBuilder.Create(
            _config,
            App.Current.LocalizationService,
            SetCollapseBehaviorOverride,
            ResetCompactWidthOverride,
            IsWidgetCollapsed,
            SyncWidthToOtherState,
            CanSyncWidthToOtherState()));
        flyout.Items.Add(new MenuFlyoutSeparator());

        var rename = new MenuFlyoutItem
        {
            Text = App.Current.LocalizationService.T("Common.Rename"),
            Icon = new FontIcon { Glyph = "\uE8AC" }
        };
        bool startRenameWhenClosed = false;
        rename.Click += (_, _) => startRenameWhenClosed = true;
        flyout.Closed += (_, _) =>
        {
            if (startRenameWhenClosed)
            {
                DispatcherQueue.TryEnqueue(StartTitleRename);
            }
        };
        flyout.Items.Add(rename);

        if (_descriptor.HasSettingsPage && !string.IsNullOrWhiteSpace(_descriptor.SettingsSectionTag))
        {
            var settingsItem = new MenuFlyoutItem
            {
                Text = App.Current.LocalizationService.T(GetSettingsMenuTextKey(_config.WidgetKind)),
                Icon = new FontIcon { Glyph = "\uE713" }
            };
            settingsItem.Click += (_, _) => App.Current.ShowSettings(_descriptor.SettingsSectionTag);
            flyout.Items.Add(settingsItem);
        }

        flyout.Items.Add(new MenuFlyoutSeparator());
        var disableWidget = new MenuFlyoutItem
        {
            Text = App.Current.LocalizationService.T("Settings.Music.Disable"),
            Icon = new FontIcon { Glyph = "\uE7E8" }
        };
        disableWidget.Click += async (_, _) =>
        {
            if (App.Current.WidgetManager is { } widgetManager &&
                _config.WidgetKind == WidgetKind.Music)
            {
                await widgetManager.SetMusicWidgetEnabledAsync(enabled: false, reveal: false);
            }
        };
        flyout.Items.Add(disableWidget);

        return flyout;
    }

    private static string GetSettingsMenuTextKey(WidgetKind kind)
    {
        if (Services.WidgetKinds.WidgetKindHandlerRegistry.Default.TryGet(kind, out var handler) &&
            !string.IsNullOrWhiteSpace(handler.SettingsMenuTextKey))
        {
            return handler.SettingsMenuTextKey;
        }

        return "Common.Configure";
    }

    private void SetPositionLocked(bool value)
    {
        if (_config.IsPositionLocked == value)
        {
            return;
        }

        _config.IsPositionLocked = value;
        SettingsService.UpdateWidget(_config);
        ApplyLockActionIconState();
    }

    private void SetSizeLocked(bool value)
    {
        if (_config.IsSizeLocked == value)
        {
            return;
        }

        _config.IsSizeLocked = value;
        SettingsService.UpdateWidget(_config);
        ApplyLockActionIconState();
    }

    // ── Title rename ───────────────────────────────────────────

    private void StartTitleRename()
    {
        if (IsDragging ||
            IsResizing ||
            ContentWidgetShell.TitleEditorContent is not null)
        {
            return;
        }

        _isCancellingTitleRename = false;
        BeginCompactInteraction();
        App.Current.WidgetManager?.BeginWidgetInteraction("content-title-rename-opened");
        var editor = CreateTitleRenameEditor();
        ContentWidgetShell.TitleEditorContent = editor;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (ReferenceEquals(ContentWidgetShell.TitleEditorContent, editor))
            {
                editor.Focus(FocusState.Programmatic);
                editor.SelectAll();
            }
        });
    }

    private TextBox CreateTitleRenameEditor()
    {
        var localization = App.Current.LocalizationService;
        var titleText = ContentWidgetShell.TitleTextElement;
        double maxWidth = ResolveTitleRenameMaxWidth(titleText);

        var editor = new TextBox
        {
            Text = _titleViewModel.DisplayName,
            PlaceholderText = localization.T("Widget.TitlePlaceholder"),
            MaxWidth = maxWidth,
            FontSize = titleText.FontSize,
            FontFamily = titleText.FontFamily,
            FontWeight = titleText.FontWeight,
            Margin = titleText.Margin,
            Padding = new Thickness(0),
            Style = GetTextBoxStyleResource("WidgetTitleRenameTextBoxStyle"),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (titleText.ActualHeight > 0)
        {
            editor.Height = titleText.ActualHeight;
        }

        if (titleText.Foreground is SolidColorBrush titleBrush)
        {
            editor.Foreground = titleBrush;
        }

        WidgetTitleRenameEditorHelper.ApplyAutoWidth(editor, maxWidth);
        editor.TextChanged += TitleRenameEditor_TextChanged;
        editor.KeyDown += TitleRenameEditor_KeyDown;
        editor.LostFocus += TitleRenameEditor_LostFocus;
        return editor;
    }

    private double ResolveTitleRenameMaxWidth(TextBlock titleText)
    {
        double fallback = 300;
        double titleBarWidth = ContentWidgetShell.TitleBar.ActualWidth;
        if (titleBarWidth > 0)
        {
            double reserved =
                ContentWidgetShell.TitleIconElement.ActualWidth +
                ContentWidgetShell.RightActionButtonHost.ActualWidth +
                24;
            fallback = Math.Max(120, titleBarWidth - reserved);
        }

        return WidgetTitleRenameEditorHelper.ResolveMaxWidth(titleText, fallback);
    }

    private void TitleRenameEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ContentWidgetShell.TitleEditorContent is TextBox editor)
        {
            WidgetTitleRenameEditorHelper.ApplyAutoWidth(editor, editor.MaxWidth);
        }
    }

    private static Style? GetTextBoxStyleResource(string resourceKey)
    {
        return Application.Current.Resources.TryGetValue(resourceKey, out object? resource) && resource is Style style
            ? style
            : null;
    }

    private async void TitleRenameEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isCancellingTitleRename)
        {
            _isCancellingTitleRename = false;
            return;
        }

        await CommitTitleRenameAsync();
    }

    private async void TitleRenameEditor_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await CommitTitleRenameAsync();
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            CancelTitleRename();
            e.Handled = true;
        }
    }

    private async Task CommitTitleRenameAsync()
    {
        if (_isCommittingTitleRename ||
            ContentWidgetShell.TitleEditorContent is not TextBox editor)
        {
            return;
        }

        string newName = editor.Text.Trim();
        _isCommittingTitleRename = true;
        try
        {
            if (!string.IsNullOrEmpty(newName))
            {
                await App.Current.WidgetManager!.RenameWidgetAsync(_config.Id, newName);
                _titleViewModel.RefreshDisplayName();
            }

            CompleteTitleRename("content-title-rename-committed");
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync(App.Current.LocalizationService.T("Widget.RenameFailed"), ex.Message);
            editor.Focus(FocusState.Programmatic);
            editor.SelectAll();
        }
        finally
        {
            _isCommittingTitleRename = false;
        }
    }

    private void CancelTitleRename()
    {
        _isCancellingTitleRename = true;
        CompleteTitleRename("content-title-rename-canceled");
    }

    private void CompleteTitleRename(string reason)
    {
        if (ContentWidgetShell.TitleEditorContent is TextBox editor)
        {
            editor.TextChanged -= TitleRenameEditor_TextChanged;
            editor.KeyDown -= TitleRenameEditor_KeyDown;
            editor.LostFocus -= TitleRenameEditor_LostFocus;
        }

        ContentWidgetShell.TitleEditorContent = null;
        EndCompactInteraction();
        App.Current.WidgetManager?.EndWidgetInteraction(reason);
        if (App.Current.WidgetManager?.RequestRestoreRaisedWidgetsToDesktopLayer(reason) == true)
        {
            return;
        }

        RestoreDesktopLayerFromManager();
    }

    private async Task ShowErrorDialogAsync(string title, string message)
    {
        var localization = App.Current.LocalizationService;
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = title,
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.WrapWholeWords,
                MaxWidth = 320
            },
            CloseButtonText = localization.T("Common.Ok"),
            DefaultButton = ContentDialogButton.Close
        };

        await dialog.ShowAsync();
    }

    private void ShowFlyoutWithInteraction(MenuFlyout flyout, FrameworkElement target, Windows.Foundation.Point? position = null)
    {
        BeginCompactInteraction();
        App.Current.WidgetManager?.BeginWidgetInteraction("content-flyout-opened");
        flyout.Closed += (_, _) =>
        {
            EndCompactInteraction();
            App.Current.WidgetManager?.EndWidgetInteraction("content-flyout-closed");
            if (App.Current.WidgetManager?.RequestRestoreRaisedWidgetsToDesktopLayer("content-flyout-closed") == true)
            {
                return;
            }

            RestoreDesktopLayerFromManager();
        };
        WidgetFlyoutDesktopDismiss.Track(flyout);

        if (position is Windows.Foundation.Point point)
        {
            flyout.ShowAt(target, point);
        }
        else
        {
            flyout.ShowAt(target);
        }
    }

    // ── Color helpers ──────────────────────────────────────────
}
