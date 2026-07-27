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
    public IEnumerable<FeatureWidgetEntry> FeatureWidgetEntries
    {
        get
        {
            var factory = new FeatureWidgetEntryFactory(
                _localizationService,
                new WidgetContentFactory(_localizationService),
                WidgetRegistry.Default,
                IsWidgetEnabled);
            return factory.CreateEntries();
        }
    }

    public bool IsWidgetEnabled(WidgetKind kind)
    {
        return App.Current?.WidgetManager?.IsFeatureWidgetEnabled(kind) ??
               FeatureWidgetSettings.IsEnabled(_settingsService.Settings, kind);
    }

    public void SetWidgetEnabled(WidgetKind kind, bool enabled)
    {
        FeatureWidgetSettings.SetEnabled(_settingsService.Settings, kind, enabled);
        _ = SyncFeatureWidgetAsync(kind, enabled);
    }

    public async Task ResetFeatureWidgetAsync(WidgetKind kind)
    {
        if (!FeatureWidgetSettings.IsFeatureWidget(kind))
        {
            return;
        }

        try
        {
            await ApplyFeatureWidgetDefaultSettingsAsync(kind);

            if (App.Current?.WidgetManager is { } widgetManager)
            {
                await widgetManager.ResetFeatureWidgetAsync(kind);
            }
            else
            {
                await _settingsService.SaveAsync();
            }
        }
        catch (Exception ex)
        {
            App.Log($"[SettingsViewModel] Failed to reset feature widget kind={kind}: {ex}");
        }
        finally
        {
            OnPropertyChanged(nameof(FeatureWidgetEntries));
        }
    }

    private async Task ApplyFeatureWidgetDefaultSettingsAsync(WidgetKind kind)
    {
        bool wasApplyingSnapshot = _isApplyingSettingsSnapshot;
        _isApplyingSettingsSnapshot = true;
        try
        {
            switch (kind)
            {
                case WidgetKind.Music:
                    MusicUseArtworkBackdrop = true;
                    MusicEnableCoverHoverMotion = true;
                    SelectedMusicDisplayMode = SettingsService.MusicDisplayModeAuto;
                    _settingsService.Settings.MusicUseArtworkBackdrop = true;
                    _settingsService.Settings.MusicEnableCoverHoverMotion = true;
                    _settingsService.Settings.MusicDisplayMode = SettingsService.MusicDisplayModeAuto;
                    break;
            }
        }
        finally
        {
            _isApplyingSettingsSnapshot = wasApplyingSnapshot;
        }

        await _settingsService.SaveAsync();
    }

    private async Task SyncFeatureWidgetAsync(WidgetKind kind, bool enabled)
    {
        try
        {
            if (App.Current?.WidgetManager is not { } widgetManager)
            {
                await _settingsService.SaveAsync();
                return;
            }

            await widgetManager.SetFeatureWidgetEnabledAsync(kind, enabled, reveal: enabled);
        }
        catch (Exception ex)
        {
            App.Log($"[SettingsViewModel] Failed to sync feature widget enabled state kind={kind}: {ex}");
        }
        finally
        {
            OnPropertyChanged(nameof(FeatureWidgetEntries));
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

    public string[] AvailableWidgetCompactContentModes { get; } =
    [
        SettingsService.WidgetCompactContentModeSmart,
        SettingsService.WidgetCompactContentModeSummary,
        SettingsService.WidgetCompactContentModeMinimal
    ];
    public string[] AvailableWidgetCompactContentModeDisplayNames =>
        _cachedWidgetCompactContentModeDisplayNames ??=
            AvailableWidgetCompactContentModes.Select(GetWidgetCompactContentModeDisplayName).ToArray();
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

    public string[] AvailableDisplayWidgetChromeModes { get; } =
    [
        SettingsService.WidgetChromeModeStandard,
        SettingsService.WidgetChromeModeCompact,
        SettingsService.WidgetChromeModeOverlay,
        SettingsService.WidgetChromeModeHidden
    ];

    public string[] AvailableInteractiveWidgetChromeModes { get; } =
    [
        SettingsService.WidgetChromeModeStandard,
        SettingsService.WidgetChromeModeCompact,
        SettingsService.WidgetChromeModeOverlay,
        SettingsService.WidgetChromeModeHidden
    ];

    public string[] AvailableDisplayWidgetChromeModeDisplayNames => _cachedDisplayWidgetChromeModeDisplayNames ??= AvailableDisplayWidgetChromeModes.Select(GetWidgetChromeModeDisplayName).ToArray();
    public string[] AvailableInteractiveWidgetChromeModeDisplayNames => _cachedInteractiveWidgetChromeModeDisplayNames ??= AvailableInteractiveWidgetChromeModes.Select(GetWidgetChromeModeDisplayName).ToArray();

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
