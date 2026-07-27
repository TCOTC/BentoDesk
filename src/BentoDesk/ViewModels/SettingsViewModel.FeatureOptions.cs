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
    public string SelectedManagedDropAction
    {
        get => _selectedManagedDropAction;
        set
        {
            string normalized = value == SettingsService.ManagedDropActionCopy
                ? SettingsService.ManagedDropActionCopy
                : SettingsService.ManagedDropActionMove;
            if (!SetProperty(ref _selectedManagedDropAction, normalized))
            {
                return;
            }

            if (!_isRestoringDefaults && !_isApplyingSettingsSnapshot)
            {
                _settingsService.Settings.ManagedDropAction = normalized;
                _settingsService.SaveDebounced();
            }

        }
    }


    public string SelectedMusicDisplayMode
    {
        get => _selectedMusicDisplayMode;
        set
        {
            string normalizedValue = SettingsService.NormalizeMusicDisplayMode(value);
            if (!SetProperty(ref _selectedMusicDisplayMode, normalizedValue))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedMusicDisplayModeText));
            if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
            {
                return;
            }

            _settingsService.Settings.MusicDisplayMode = normalizedValue;
            _settingsService.SaveDebounced();
        }
    }

    public string SelectedMusicDisplayModeText => GetMusicDisplayModeDisplayName(SelectedMusicDisplayMode);

    public string AccentColorHex
    {
        get => _accentColorHex;
        private set => SetProperty(ref _accentColorHex, value);
    }

    public Color SelectedAccentColor
    {
        get => _currentAccentColor;
        set
        {
            if (_currentAccentColor.Equals(value))
            {
                return;
            }

            SetCustomAccentColor(value);
        }
    }

    public bool GlobalHotkeyEnabled
    {
        get => _globalHotkeyEnabled;
        set
        {
            if (!SetProperty(ref _globalHotkeyEnabled, value))
            {
                return;
            }

            if (_isRestoringDefaults)
            {
                return;
            }

            App.Current?.GlobalHotkeyService?.SetEnabled(value);
            RefreshGlobalHotkeyStatus();
        }
    }

    public string GlobalHotkeyText
    {
        get => _globalHotkeyText;
        private set => SetProperty(ref _globalHotkeyText, value);
    }

    public string GlobalHotkeyStatusText
    {
        get => _globalHotkeyStatusText;
        private set => SetProperty(ref _globalHotkeyStatusText, value);
    }

    public string GlobalHotkeyStatusKind
    {
        get => _globalHotkeyStatusKind;
        private set => SetProperty(ref _globalHotkeyStatusKind, value);
    }

    public string IconSizeValueText => $"{Math.Round(IconSize):0}px";
    public string WidgetOpacityValueText => $"{Math.Round((1.0 - WidgetOpacity) * 100):0}%";

    /// <summary>
    /// UI-facing transparency value (inverted from internal WidgetOpacity).
    /// 0 = fully opaque, 1 = most transparent.  The slider binds to this.
    /// </summary>
    public double WidgetTransparency
    {
        get => 1.0 - WidgetOpacity;
        set => WidgetOpacity = 1.0 - Math.Clamp(value, 0.0, 1.0);
    }
    public string WidgetMaterialIntensityValueText => $"{Math.Round(WidgetMaterialIntensity * 100):0}%";
    public string TextSizeValueText => $"{TextSize:0.#}pt";
    public string LayoutDensityValueText => $"{Math.Round(LayoutDensityScale * 100):0}%";
    public string HorizontalSpacingValueText => $"{Math.Round(HorizontalSpacingScale * 100):0}%";
    public string VerticalSpacingValueText => $"{Math.Round(VerticalSpacingScale * 100):0}%";
    public string FileNameWidthValueText => $"{Math.Round(FileNameWidthScale * 100):0}%";
    public string DefaultWidthInput
    {
        get => FormatNumber(DefaultWidth, 0);
        set => ApplyNumberInput(value, () => DefaultWidth, next => DefaultWidth = next, SettingsService.MinWidgetWidth, 1200d, 0);
    }

    public string DefaultHeightInput
    {
        get => FormatNumber(DefaultHeight, 0);
        set => ApplyNumberInput(value, () => DefaultHeight, next => DefaultHeight = next, SettingsService.MinWidgetHeight, 1200d, 0);
    }

    public string WidgetOpacityPercentInput
    {
        get => FormatNumber(WidgetOpacityPercent, 0);
        set => ApplyNumberInput(value, () => WidgetOpacityPercent, next => WidgetOpacityPercent = next, 0d, 100d, 0);
    }

    public string IconSizeInput
    {
        get => FormatNumber(IconSize, 0);
        set => ApplyNumberInput(value, () => IconSize, next => IconSize = next, SettingsService.MinIconSize, SettingsService.MaxIconSize, 0);
    }

    public string TextSizeInput
    {
        get => FormatNumber(TextSize, 1);
        set => ApplyNumberInput(value, () => TextSize, next => TextSize = next, SettingsService.MinTextSize, SettingsService.MaxTextSize, 1);
    }

    public string LayoutDensityPercentInput
    {
        get => FormatNumber(LayoutDensityPercent, 0);
        set => ApplyNumberInput(value, () => LayoutDensityPercent, next => LayoutDensityPercent = next, 0d, 100d, 0);
    }

    public string HorizontalSpacingPercentInput
    {
        get => FormatNumber(HorizontalSpacingPercent, 0);
        set => ApplyNumberInput(value, () => HorizontalSpacingPercent, next => HorizontalSpacingPercent = next, 0d, 100d, 0);
    }

    public string VerticalSpacingPercentInput
    {
        get => FormatNumber(VerticalSpacingPercent, 0);
        set => ApplyNumberInput(value, () => VerticalSpacingPercent, next => VerticalSpacingPercent = next, 0d, 100d, 0);
    }

    public string FileNameWidthPercentInput
    {
        get => FormatNumber(FileNameWidthPercent, 0);
        set => ApplyNumberInput(value, () => FileNameWidthPercent, next => FileNameWidthPercent = next, 0d, 100d, 0);
    }

public double WidgetOpacityPercent
{
get => Math.Round((1.0 - WidgetOpacity) * 100);
set => WidgetOpacity = Math.Clamp(1.0 - value / 100d, SettingsService.MinWidgetOpacity, SettingsService.MaxWidgetOpacity);
}

    public double LayoutDensityPercent
    {
        get => Math.Round(LayoutDensityScale * 100);
        set => LayoutDensityScale = Math.Clamp(value / 100d, SettingsService.MinLayoutDensityScale, SettingsService.MaxLayoutDensityScale);
    }

    public double HorizontalSpacingPercent
    {
        get => Math.Round(HorizontalSpacingScale * 100);
        set => HorizontalSpacingScale = Math.Clamp(value / 100d, SettingsService.MinSpacingScale, SettingsService.MaxSpacingScale);
    }

    public double VerticalSpacingPercent
    {
        get => Math.Round(VerticalSpacingScale * 100);
        set => VerticalSpacingScale = Math.Clamp(value / 100d, SettingsService.MinSpacingScale, SettingsService.MaxSpacingScale);
    }

    public double FileNameWidthPercent
    {
        get => Math.Round(FileNameWidthScale * 100);
        set => FileNameWidthScale = Math.Clamp(value / 100d, SettingsService.MinSpacingScale, SettingsService.MaxSpacingScale);
    }

    public string AccentColorDescription => UseSystemAccentColor
        ? _localizationService.T("Settings.Accent.SystemDescription")
        : _localizationService.T("Settings.Accent.CustomDescription");

    public string GlobalHotkeyDescription => _localizationService.T("Settings.GlobalHotkey.Description");
    public bool CanShowGlobalHotkeyWarning => GlobalHotkeyEnabled && GlobalHotkeyService.IsRiskyGesture(GetCurrentGlobalHotkeyGesture());

    public bool IsMusicWidgetEnabled()
    {
        return App.Current?.WidgetManager?.IsMusicWidgetEnabled() ??
               _settingsService.Settings.MusicWidgetEnabled;
    }

    public async Task ResetMusicWidgetPreferencesAsync()
    {
        try
        {
            await ApplyMusicWidgetDefaultSettingsAsync();
        }
        catch (Exception ex)
        {
            App.Log($"[SettingsViewModel] Failed to reset music widget preferences: {ex}");
        }
    }

    private async Task ApplyMusicWidgetDefaultSettingsAsync()
    {
        bool wasApplyingSnapshot = _isApplyingSettingsSnapshot;
        _isApplyingSettingsSnapshot = true;
        try
        {
            MusicUseArtworkBackdrop = true;
            MusicEnableCoverHoverMotion = true;
            SelectedMusicDisplayMode = SettingsService.MusicDisplayModeAuto;
            _settingsService.Settings.MusicUseArtworkBackdrop = true;
            _settingsService.Settings.MusicEnableCoverHoverMotion = true;
            _settingsService.Settings.MusicDisplayMode = SettingsService.MusicDisplayModeAuto;
        }
        finally
        {
            _isApplyingSettingsSnapshot = wasApplyingSnapshot;
        }

        await _settingsService.SaveAsync();
    }

    private async Task SyncMusicWidgetEnabledAsync(bool enabled)
    {
        try
        {
            if (App.Current?.WidgetManager is not { } widgetManager)
            {
                _settingsService.Settings.MusicWidgetEnabled = enabled;
                await _settingsService.SaveAsync();
                return;
            }

            await widgetManager.SetMusicWidgetEnabledAsync(enabled, reveal: enabled);
            MusicWidgetEnabled = widgetManager.IsMusicWidgetEnabled();
        }
        catch (Exception ex)
        {
            App.Log($"[SettingsViewModel] Failed to sync music widget enabled state: {ex}");
            MusicWidgetEnabled = _settingsService.Settings.MusicWidgetEnabled;
        }
    }

    public SolidColorBrush AccentPreviewBrush { get; } = new(AccentColorHelper.DefaultAccentColor);

    public string[] AvailableThemes { get; } = [ThemeSystem, ThemeLight, ThemeDark];
    public string[] AvailableThemeDisplayNames => _cachedThemeDisplayNames ??= AvailableThemes.Select(GetThemeDisplayName).ToArray();
    public string[] AvailableWidgetCornerPreferences { get; } =
        [CornerDefault, CornerSmall, CornerRound, CornerSquare];
    public string[] AvailableWidgetCornerPreferenceDisplayNames => _cachedWidgetCornerPreferenceDisplayNames ??= AvailableWidgetCornerPreferences.Select(GetCornerDisplayName).ToArray();

    public string[] AvailableWidgetMaterialTypes { get; } =
        [MaterialAcrylic, MaterialAcrylicBase, MaterialMica, MaterialMicaAlt, MaterialSolid];
    public string[] AvailableWidgetMaterialTypeDisplayNames => _cachedWidgetMaterialTypeDisplayNames ??= AvailableWidgetMaterialTypes.Select(GetMaterialTypeDisplayName).ToArray();

    public string[] AvailableWidgetBorderColorModes { get; } =
        [BorderColorNeutral, BorderColorAccent, BorderColorNone];
    public string[] AvailableWidgetBorderColorModeDisplayNames =>
        _cachedWidgetBorderColorModeDisplayNames ??=
            AvailableWidgetBorderColorModes.Select(GetBorderColorModeDisplayName).ToArray();

    public string[] AvailableWidgetBorderStyles { get; } = [BorderThin, BorderMedium, BorderThick];
    public string[] AvailableWidgetBorderStyleDisplayNames => _cachedWidgetBorderStyleDisplayNames ??= AvailableWidgetBorderStyles.Select(GetBorderStyleDisplayName).ToArray();

    public string[] AvailableWidgetCollapseBehaviors { get; } =
    [
        SettingsService.WidgetCollapseBehaviorClick,
        SettingsService.WidgetCollapseBehaviorSmart
    ];
    public string[] AvailableWidgetCollapseBehaviorDisplayNames =>
        _cachedWidgetCollapseBehaviorDisplayNames ??=
            AvailableWidgetCollapseBehaviors.Select(GetWidgetCollapseBehaviorDisplayName).ToArray();

    public string[] AvailableLayoutDensities { get; } =
    [
        SettingsService.LayoutDensityCompact,
        SettingsService.LayoutDensityStandard,
        SettingsService.LayoutDensityRelaxed,
        SettingsService.LayoutDensityCustom
    ];
    public string[] AvailableLayoutDensityDisplayNames =>
        _cachedLayoutDensityDisplayNames ??= AvailableLayoutDensities.Select(GetLayoutDensityDisplayName).ToArray();
    public string[] AvailableMusicDisplayModes { get; } =
    [
        SettingsService.MusicDisplayModeAuto,
        SettingsService.MusicDisplayModeCover,
        SettingsService.MusicDisplayModeControls,
        SettingsService.MusicDisplayModeRecordVertical,
        SettingsService.MusicDisplayModeRecordHorizontal
    ];
    public string[] AvailableMusicDisplayModeDisplayNames =>
        _cachedMusicDisplayModeDisplayNames ??= AvailableMusicDisplayModes.Select(GetMusicDisplayModeDisplayName).ToArray();
    public string[] AvailableAnimationPresets { get; } =
    [
        AnimationPresetNone,
        AnimationPresetFade
    ];
    public string[] AvailableAnimationPresetDisplayNames =>
        _cachedAnimationPresetDisplayNames ??= AvailableAnimationPresets.Select(GetAnimationPresetDisplayName).ToArray();

    public string[] AvailableWidgetTitleIconModes { get; } =
    [
        SettingsService.WidgetTitleIconModeFilledMono,
        SettingsService.WidgetTitleIconModeLineMono,
        SettingsService.WidgetTitleIconModeColor,
        SettingsService.WidgetTitleIconModeHidden,
        SettingsService.WidgetTitleIconModeTextLabel
    ];

    public string[] AvailableWidgetTitleIconModeDisplayNames => _cachedWidgetTitleIconModeDisplayNames ??= AvailableWidgetTitleIconModes.Select(GetWidgetTitleIconModeDisplayName).ToArray();

    public string[] AvailableManagedDropActions { get; } =
    [
        SettingsService.ManagedDropActionCopy,
        SettingsService.ManagedDropActionMove
    ];

    public string[] AvailableManagedDropActionDisplayNames =>
        _cachedManagedDropActionDisplayNames ??= AvailableManagedDropActions
            .Select(GetManagedDropActionDisplayName)
            .ToArray();

    public string GetManagedDropActionDisplayName(string action) =>
        action == SettingsService.ManagedDropActionMove
            ? _localizationService.T("Settings.DropAction.Move")
            : _localizationService.T("Settings.DropAction.Copy");
}
