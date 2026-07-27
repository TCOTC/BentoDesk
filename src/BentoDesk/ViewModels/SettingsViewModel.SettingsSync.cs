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
    private void OnLanguageChanged()
    {
        RefreshLocalizedProperties();
    }

    private void OnSettingsChanged()
    {
        if (App.UiDispatcherQueue is { } dispatcherQueue && !dispatcherQueue.HasThreadAccess)
        {
            dispatcherQueue.TryEnqueue(OnSettingsChanged);
            return;
        }

        ApplySettingsSnapshot();
    }

    private void ApplySettingsSnapshot()
    {
        var settings = _settingsService.Settings;
        bool wasRestoringDefaults = _isRestoringDefaults;

        _isApplyingSettingsSnapshot = true;
        _isRestoringDefaults = true;
        try
        {
            SelectedTheme = settings.Theme is ThemeLight or ThemeDark ? settings.Theme : ThemeSystem;
            SelectedTrayIconStyle = settings.TrayIconStyle is TrayIconStyleColorful or TrayIconStyleBlack or TrayIconStyleWhite
                ? settings.TrayIconStyle
                : TrayIconStyleSystem;
            UseSystemAccentColor = !string.Equals(
                settings.AccentColorMode,
                ThemeService.AccentModeCustom,
                StringComparison.OrdinalIgnoreCase);

            AutoCheckForUpdates = settings.AutoCheckForUpdates;
            DoubleClickDesktopToHideAll = settings.DoubleClickDesktopToHideAll;
            DefaultWidth = settings.DefaultWidgetWidth;
            DefaultHeight = settings.DefaultWidgetHeight;
            HideShortcutArrowOverlay = settings.HideShortcutArrowOverlay;
            ShowImageFilesAsIcons = settings.ShowImageFilesAsIcons;
            ResizeSnapEnabled = settings.ResizeSnapEnabled;
            ShowListItemDetails = settings.ShowListItemDetails;
            ShowFileItemPathTooltips = settings.ShowFileItemPathTooltips;

            WidgetOpacity = settings.WidgetOpacity;
            WidgetMaterialIntensity = settings.WidgetMaterialIntensity;
            SelectedWidgetCornerPreference = settings.WidgetCornerPreference is CornerDefault or CornerSquare or CornerSmall or CornerRound
                ? settings.WidgetCornerPreference
                : CornerSmall;
            SelectedWidgetMaterialType = settings.WidgetMaterialType is MaterialMica or MaterialMicaAlt or MaterialAcrylic or MaterialAcrylicBase or MaterialSolid
                ? settings.WidgetMaterialType
                : MaterialAcrylic;
            SelectedWidgetBorderColorMode = settings.WidgetBorderColorMode is BorderColorNeutral or BorderColorAccent or BorderColorNone
                ? settings.WidgetBorderColorMode
                : BorderColorNeutral;
            SelectedWidgetBorderStyle = settings.WidgetBorderStyle is BorderThin or BorderMedium or BorderThick
                ? settings.WidgetBorderStyle
                : BorderThin;

            SelectedWidgetCollapseBehavior = SettingsService.NormalizeWidgetCollapseBehavior(settings.WidgetCollapseBehavior) == SettingsService.WidgetCollapseBehaviorSmart
                ? SettingsService.WidgetCollapseBehaviorSmart
                : SettingsService.WidgetCollapseBehaviorClick;
            SelectedWidgetCompactAnimationEffect = SettingsService.NormalizeWidgetCompactAnimationEffect(settings.WidgetCompactAnimationEffect);
            WidgetCompactAnimationDurationMs = SettingsService.NormalizeWidgetCompactAnimationDurationMs(settings.WidgetCompactAnimationDurationMs);
            WidgetCompactExpandDelayMs = SettingsService.NormalizeWidgetCompactExpandDelayMs(settings.WidgetCompactExpandDelayMs);
            WidgetCompactCollapseDelayMs = SettingsService.NormalizeWidgetCompactCollapseDelayMs(settings.WidgetCompactCollapseDelayMs);
            SelectedWidgetCompactHoverResponse = SettingsService.ResolveWidgetCompactHoverResponse(
                settings.WidgetCompactExpandDelayMs,
                settings.WidgetCompactCollapseDelayMs);
            SelectedWidgetCompactMediaCornerMode = SettingsService.NormalizeWidgetCompactMediaCornerMode(settings.WidgetCompactMediaCornerMode);

            SelectedWidgetAnimationEffect = NormalizeWidgetAnimationEffect(settings.WidgetAnimationEffect);
            SelectedWidgetAnimationSpeed = NormalizeWidgetAnimationSpeed(settings.WidgetAnimationSpeed);
            SelectedWidgetAnimationSlideDirection = NormalizeWidgetAnimationSlideDirection(settings.WidgetAnimationSlideDirection);
            SelectedWidgetAnimationEasingIntensity = NormalizeWidgetAnimationEasingIntensity(settings.WidgetAnimationEasingIntensity);
            SelectedAnimationPreset = ResolveAnimationPreset();
            SelectedWidgetTitleIconMode = NormalizeWidgetTitleIconModeSetting(settings.WidgetTitleIconMode);

            IconSize = settings.IconSize;
            TextSize = settings.TextSize;
            LayoutDensityScale = settings.LayoutDensityScale;
            HorizontalSpacingScale = settings.HorizontalSpacingScale;
            VerticalSpacingScale = settings.VerticalSpacingScale;
            FileNameWidthScale = settings.FileNameWidthScale;
            SelectedLayoutDensity = SettingsService.ResolveLayoutDensityPreset(settings);
            ShowFileExtensions = settings.ShowFileExtensions;
            HideShortcutExtensionWhenShowingFileExtensions = settings.HideShortcutExtensionWhenShowingFileExtensions;

            ApplyFileStackSettingsSnapshot(settings);

            SelectedManagedDropAction = settings.ManagedDropAction == SettingsService.ManagedDropActionMove
                ? SettingsService.ManagedDropActionMove
                : SettingsService.ManagedDropActionCopy;

            MusicWidgetEnabled = settings.MusicWidgetEnabled;
            MusicUseArtworkBackdrop = settings.MusicUseArtworkBackdrop;
            MusicEnableCoverHoverMotion = settings.MusicEnableCoverHoverMotion;
            SelectedMusicDisplayMode = SettingsService.NormalizeMusicDisplayMode(settings.MusicDisplayMode);

            GlobalHotkeyEnabled = settings.GlobalHotkeyEnabled;
        }
        finally
        {
            _isApplyingSettingsSnapshot = false;
            _isRestoringDefaults = wasRestoringDefaults;
        }

        RefreshNumberInputs();
        RefreshSelectionProperties(refreshLocalizedOptions: false);
        RefreshGlobalHotkeyState();
        OnPropertyChanged(nameof(CanEditCustomAccent));
        OnPropertyChanged(nameof(AccentColorDescription));
        NotifyCapsuleOverridePropertiesChanged();
    }

    private void RefreshLocalizedProperties()
    {
        RefreshSelectionProperties(refreshLocalizedOptions: true);
        OnPropertyChanged(nameof(AccentColorDescription));
        OnPropertyChanged(nameof(AboutVersionText));
        OnPropertyChanged(nameof(DistributionChannelText));
        OnPropertyChanged(nameof(AboutDeveloperText));
        OnPropertyChanged(nameof(UpdateDownloadActionText));
        if (!IsCheckingForUpdates && !IsDownloadingUpdate)
        {
            if (_appUpdateService.LastCheckResult is not null)
            {
                ApplyCachedUpdateResult();
            }
            else
            {
                UpdateStatusText = _localizationService.T("Settings.Update.Status.Ready");
                UpdateDetailText = GetReadyUpdateDetailText();
            }
        }
        OnPropertyChanged(nameof(GlobalHotkeyDescription));
        OnPropertyChanged(nameof(GlobalHotkeyText));
        OnPropertyChanged(nameof(GlobalHotkeyStatusText));
        OnPropertyChanged(nameof(GlobalHotkeyStatusKind));
        OnPropertyChanged(nameof(CanShowGlobalHotkeyWarning));
        NotifyDragDropPermissionPropertiesChanged();
        NotifyCapsuleOverridePropertiesChanged();
    }

    private void RefreshSelectionProperties(bool refreshLocalizedOptions)
    {
        // Replacing localized option arrays during an ordinary settings sync makes
        // WinUI reset every bound ComboBox.SelectedIndex to -1.
        if (refreshLocalizedOptions)
        {
            RefreshFileStackSelectionProperties();
            _cachedThemeDisplayNames = null;
            _cachedTrayIconStyleDisplayNames = null;
            _cachedWidgetCornerPreferenceDisplayNames = null;
            _cachedWidgetMaterialTypeDisplayNames = null;
            _cachedWidgetBorderColorModeDisplayNames = null;
            _cachedWidgetBorderStyleDisplayNames = null;
            _cachedWidgetCollapseBehaviorDisplayNames = null;
            _cachedWidgetCompactAnimationEffectDisplayNames = null;
            _cachedWidgetCompactHoverResponseDisplayNames = null;
            _cachedWidgetCompactMediaCornerDisplayNames = null;
            _cachedLayoutDensityDisplayNames = null;
            _cachedAnimationPresetDisplayNames = null;
            _cachedWidgetTitleIconModeDisplayNames = null;
            _cachedMusicDisplayModeDisplayNames = null;
            _cachedManagedDropActionDisplayNames = null;
            OnPropertyChanged(nameof(AvailableThemeDisplayNames));
            OnPropertyChanged(nameof(AvailableTrayIconStyleDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetCornerPreferenceDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetMaterialTypeDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetBorderColorModeDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetBorderStyleDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetCollapseBehaviorDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetCompactAnimationEffectDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetCompactHoverResponseDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetCompactMediaCornerDisplayNames));
            OnPropertyChanged(nameof(AvailableLayoutDensityDisplayNames));
            OnPropertyChanged(nameof(AvailableAnimationPresetDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetTitleIconModeDisplayNames));
            OnPropertyChanged(nameof(AvailableManagedDropActionDisplayNames));
            OnPropertyChanged(nameof(AvailableMusicDisplayModeDisplayNames));
            NotifySelectionOptionsChanged();
        }

        OnPropertyChanged(nameof(IsOpacitySliderEnabled));
        OnPropertyChanged(nameof(WidgetOpacityVisibility));
        OnPropertyChanged(nameof(MaterialIntensityVisibility));
        OnPropertyChanged(nameof(WidgetTransparency));
        OnPropertyChanged(nameof(IsWidgetBorderStyleEnabled));
        OnPropertyChanged(nameof(SelectedThemeText));
        OnPropertyChanged(nameof(SelectedTrayIconStyleText));
        OnPropertyChanged(nameof(SelectedWidgetCornerPreferenceText));
        OnPropertyChanged(nameof(SelectedWidgetMaterialTypeText));
        OnPropertyChanged(nameof(SelectedWidgetBorderColorModeText));
        OnPropertyChanged(nameof(SelectedWidgetBorderStyleText));
        OnPropertyChanged(nameof(SelectedWidgetCollapseBehaviorText));
        OnPropertyChanged(nameof(IsSmartWidgetCollapseBehavior));
        OnPropertyChanged(nameof(IsSmartWidgetCollapseBehaviorSelected));
        OnPropertyChanged(nameof(CapsuleHoverResponseEntryVisibility));
        OnPropertyChanged(nameof(CanOpenWidgetCompactHoverResponseDetails));
        OnPropertyChanged(nameof(CanOpenWidgetCompactAnimationDetails));
        OnPropertyChanged(nameof(SelectedWidgetCompactAnimationEffectText));
        OnPropertyChanged(nameof(IsWidgetCompactAnimationCustom));
        OnPropertyChanged(nameof(WidgetCompactAnimationCustomVisibility));
        OnPropertyChanged(nameof(SelectedWidgetCompactHoverResponseText));
        OnPropertyChanged(nameof(IsWidgetCompactHoverResponseCustom));
        OnPropertyChanged(nameof(WidgetCompactHoverResponseCustomVisibility));
        OnPropertyChanged(nameof(SelectedWidgetCompactMediaCornerText));
        OnPropertyChanged(nameof(SelectedLayoutDensityText));
        OnPropertyChanged(nameof(SelectedAnimationPresetText));
        OnPropertyChanged(nameof(SelectedWidgetTitleIconModeText));
        OnPropertyChanged(nameof(SelectedMusicDisplayModeText));
    }
}
