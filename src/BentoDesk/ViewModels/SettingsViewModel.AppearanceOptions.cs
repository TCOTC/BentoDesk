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
    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (!SetProperty(ref _selectedTheme, value))
            {
                return;
            }

            string themeValue = value is ThemeLight or ThemeDark ? value : ThemeSystem;

            if (_isRestoringDefaults)
            {
                return;
            }

            _themeService.SetTheme(themeValue);
            OnPropertyChanged(nameof(SelectedThemeText));
        }
    }

    public string SelectedThemeText => GetThemeDisplayName(SelectedTheme);

    public string SelectedTrayIconStyle
    {
        get => _selectedTrayIconStyle;
        set
        {
            if (!SetProperty(ref _selectedTrayIconStyle, value))
            {
                return;
            }

            string styleValue = value is TrayIconStyleColorful or TrayIconStyleBlack or TrayIconStyleWhite
                ? value
                : TrayIconStyleSystem;

            if (_isRestoringDefaults)
            {
                return;
            }

            _settingsService.Settings.TrayIconStyle = styleValue;
            _settingsService.SaveDebounced();
            App.Current.UpdateTrayIcon();
            OnPropertyChanged(nameof(SelectedTrayIconStyleText));
        }
    }

    public string SelectedTrayIconStyleText => GetTrayIconStyleDisplayName(SelectedTrayIconStyle);

    public string[] AvailableTrayIconStyles { get; } =
    [
        TrayIconStyleSystem,
        TrayIconStyleColorful,
        TrayIconStyleBlack,
        TrayIconStyleWhite
    ];

    public string[] AvailableTrayIconStyleDisplayNames => _cachedTrayIconStyleDisplayNames ??= AvailableTrayIconStyles.Select(GetTrayIconStyleDisplayName).ToArray();

    public bool UseSystemAccentColor
    {
        get => _useSystemAccentColor;
        set
        {
            if (!SetProperty(ref _useSystemAccentColor, value))
            {
                return;
            }

            if (_isRestoringDefaults)
            {
                return;
            }

            _themeService.SetAccentMode(value ? ThemeService.AccentModeSystem : ThemeService.AccentModeCustom);
            RefreshAccentPreview();
            OnPropertyChanged(nameof(CanEditCustomAccent));
            OnPropertyChanged(nameof(AccentColorDescription));
        }
    }

    public bool CanEditCustomAccent => !UseSystemAccentColor;

    public string SelectedWidgetCornerPreference
    {
        get => _selectedWidgetCornerPreference;
        set
        {
            if (!SetProperty(ref _selectedWidgetCornerPreference, value))
            {
                return;
            }

            if (_isRestoringDefaults)
            {
                return;
            }

            _settingsService.Settings.WidgetCornerPreference = value is CornerDefault or CornerSquare or CornerSmall or CornerRound
                ? value
                : SettingsService.WidgetCornerPreferenceSmall;
            _settingsService.SaveDebounced();
            OnPropertyChanged(nameof(SelectedWidgetCornerPreferenceText));
        }
    }

    public string SelectedWidgetCornerPreferenceText => GetCornerDisplayName(SelectedWidgetCornerPreference);

    public string SelectedWidgetMaterialType
    {
        get => _selectedWidgetMaterialType;
        set
        {
            if (!SetProperty(ref _selectedWidgetMaterialType, value))
            {
                return;
            }

            if (_isRestoringDefaults)
            {
                return;
            }

            _settingsService.Settings.WidgetMaterialType = value is
                MaterialMica or MaterialMicaAlt or MaterialAcrylic or MaterialAcrylicBase or MaterialSolid
                ? value
                : SettingsService.WidgetMaterialTypeAcrylic;

            bool forcedSolidOpacity =
                _settingsService.Settings.WidgetMaterialType == MaterialSolid &&
                Math.Abs(WidgetOpacity - SettingsService.MaxWidgetOpacity) > 0.0001;
            if (forcedSolidOpacity)
            {
                WidgetOpacity = SettingsService.MaxWidgetOpacity;
            }
            else
            {
                _settingsService.RequestAppearancePreview();
            }

            _settingsService.SaveDebounced();
            OnPropertyChanged(nameof(SelectedWidgetMaterialTypeText));
            OnPropertyChanged(nameof(IsOpacitySliderEnabled));
            OnPropertyChanged(nameof(WidgetOpacityVisibility));
            OnPropertyChanged(nameof(MaterialIntensityVisibility));
        }
    }

    public string SelectedWidgetMaterialTypeText => GetMaterialTypeDisplayName(SelectedWidgetMaterialType);

    public bool IsOpacitySliderEnabled =>
        SettingsService.SupportsWidgetOpacity(_selectedWidgetMaterialType);

    public Visibility WidgetOpacityVisibility => IsOpacitySliderEnabled
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility MaterialIntensityVisibility =>
        SettingsService.SupportsMaterialIntensity(_selectedWidgetMaterialType)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string SelectedWidgetBorderColorMode
    {
        get => _selectedWidgetBorderColorMode;
        set
        {
            if (!SetProperty(ref _selectedWidgetBorderColorMode, value))
            {
                return;
            }

            if (_isRestoringDefaults)
            {
                return;
            }

            _settingsService.Settings.WidgetBorderColorMode = value is
                BorderColorNeutral or BorderColorAccent or BorderColorNone
                    ? value
                    : BorderColorNeutral;
            _settingsService.SaveDebounced();
            OnPropertyChanged(nameof(SelectedWidgetBorderColorModeText));
            OnPropertyChanged(nameof(IsWidgetBorderStyleEnabled));
        }
    }

    public string SelectedWidgetBorderColorModeText =>
        GetBorderColorModeDisplayName(SelectedWidgetBorderColorMode);


    public bool IsWidgetBorderStyleEnabled =>
        _selectedWidgetBorderColorMode != BorderColorNone;

    public string SelectedWidgetBorderStyle
    {
        get => _selectedWidgetBorderStyle;
        set
        {
            if (!SetProperty(ref _selectedWidgetBorderStyle, value))
            {
                return;
            }

            if (_isRestoringDefaults)
            {
                return;
            }

            _settingsService.Settings.WidgetBorderStyle = value is BorderThin or BorderMedium or BorderThick
                ? value
                : SettingsService.WidgetBorderStyleThin;
            _settingsService.SaveDebounced();
            OnPropertyChanged(nameof(SelectedWidgetBorderStyleText));
        }
    }

    public string SelectedWidgetBorderStyleText => GetBorderStyleDisplayName(SelectedWidgetBorderStyle);

    public string SelectedWidgetCollapseBehavior
    {
        get => _selectedWidgetCollapseBehavior;
        set
        {
            string normalized = SettingsService.NormalizeWidgetCollapseBehavior(value);
            if (normalized == SettingsService.WidgetCollapseBehaviorExpanded)
            {
                normalized = SettingsService.WidgetCollapseBehaviorClick;
            }
            if (!SetProperty(ref _selectedWidgetCollapseBehavior, normalized))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedWidgetCollapseBehaviorText));
            OnPropertyChanged(nameof(IsSmartWidgetCollapseBehavior));
            OnPropertyChanged(nameof(IsSmartWidgetCollapseBehaviorSelected));
            OnPropertyChanged(nameof(CollapseHoverResponseEntryVisibility));

            if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
            {
                return;
            }

            _settingsService.Settings.WidgetCollapseBehavior = normalized;
            _settingsService.SaveDebounced();
        }
    }

    public string SelectedWidgetCollapseBehaviorText =>
        GetWidgetCollapseBehaviorDisplayName(SelectedWidgetCollapseBehavior);


    public string SelectedLayoutDensity
    {
        get => _selectedLayoutDensity;
        set
        {
            string normalizedValue = value is
                SettingsService.LayoutDensityCompact or
                SettingsService.LayoutDensityStandard or
                SettingsService.LayoutDensityRelaxed or
                SettingsService.LayoutDensityCustom
                    ? value
                    : SettingsService.LayoutDensityCustom;
            if (!SetProperty(ref _selectedLayoutDensity, normalizedValue))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedLayoutDensityText));
            if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
            {
                return;
            }

            if (normalizedValue == SettingsService.LayoutDensityCustom)
            {
                _settingsService.Settings.LayoutDensity = normalizedValue;
                _settingsService.SaveDebounced();
                return;
            }

            ApplyLayoutDensityPreset(normalizedValue);
        }
    }

    public string SelectedLayoutDensityText => GetLayoutDensityDisplayName(SelectedLayoutDensity);

    public string SelectedAnimationPreset
    {
        get => _selectedAnimationPreset;
        set
        {
            string normalizedValue = value == AnimationPresetNone
                ? AnimationPresetNone
                : AnimationPresetFade;
            if (!SetProperty(ref _selectedAnimationPreset, normalizedValue))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedAnimationPresetText));
            if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
            {
                return;
            }

            ApplyAnimationPreset(normalizedValue);
        }
    }

    public string SelectedAnimationPresetText => GetAnimationPresetDisplayName(SelectedAnimationPreset);

    public string SelectedWidgetAnimationEffect
    {
        get => _selectedWidgetAnimationEffect;
        set
        {
            if (!SetProperty(ref _selectedWidgetAnimationEffect, NormalizeWidgetAnimationEffect(value)))
            {
                return;
            }

            if (_isRestoringDefaults)
            {
                return;
            }

            _settingsService.Settings.WidgetAnimationEffect = _selectedWidgetAnimationEffect;

            if (!_isApplyingAnimationPreset)
            {
                _settingsService.SaveDebounced();
            }

            SyncAnimationPresetSelection();
        }
    }

    public string SelectedWidgetAnimationSpeed
    {
        get => _selectedWidgetAnimationSpeed;
        set
        {
            if (!SetProperty(ref _selectedWidgetAnimationSpeed, NormalizeWidgetAnimationSpeed(value)))
            {
                return;
            }

            if (_isRestoringDefaults)
            {
                return;
            }

            _settingsService.Settings.WidgetAnimationSpeed = _selectedWidgetAnimationSpeed;
            if (!_isApplyingAnimationPreset)
            {
                _settingsService.SaveDebounced();
            }

            SyncAnimationPresetSelection();
        }
    }

    public string SelectedWidgetAnimationSlideDirection
    {
        get => _selectedWidgetAnimationSlideDirection;
        set
        {
            if (!SetProperty(ref _selectedWidgetAnimationSlideDirection, NormalizeWidgetAnimationSlideDirection(value)))
            {
                return;
            }

            if (_isRestoringDefaults)
            {
                return;
            }

            _settingsService.Settings.WidgetAnimationSlideDirection = _selectedWidgetAnimationSlideDirection;
            if (!_isApplyingAnimationPreset)
            {
                _settingsService.SaveDebounced();
            }

            SyncAnimationPresetSelection();
        }
    }

    public string SelectedWidgetAnimationEasingIntensity
    {
        get => _selectedWidgetAnimationEasingIntensity;
        set
        {
            if (!SetProperty(ref _selectedWidgetAnimationEasingIntensity, NormalizeWidgetAnimationEasingIntensity(value)))
            {
                return;
            }

            if (_isRestoringDefaults)
            {
                return;
            }

            _settingsService.Settings.WidgetAnimationEasingIntensity = _selectedWidgetAnimationEasingIntensity;
            if (!_isApplyingAnimationPreset)
            {
                _settingsService.SaveDebounced();
            }

            SyncAnimationPresetSelection();
        }
    }

    public string SelectedWidgetTitleIconMode
    {
        get => _selectedWidgetTitleIconMode;
        set
        {
            if (!SetProperty(ref _selectedWidgetTitleIconMode, NormalizeWidgetTitleIconModeSetting(value)))
            {
                return;
            }

            if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
            {
                return;
            }

            _settingsService.Settings.WidgetTitleIconMode = _selectedWidgetTitleIconMode;
            SaveAppearanceChange();
            OnPropertyChanged(nameof(SelectedWidgetTitleIconModeText));
        }
    }

    public string SelectedWidgetTitleIconModeText => GetWidgetTitleIconModeDisplayName(SelectedWidgetTitleIconMode);
}
