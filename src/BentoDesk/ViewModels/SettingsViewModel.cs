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

/// <summary>
/// ViewModel for the settings window.
/// </summary>
public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private const string ThemeSystem = "System";
    private const string ThemeLight = "Light";
    private const string ThemeDark = "Dark";
    private const string TrayIconStyleSystem = "System";
    private const string TrayIconStyleColorful = "Colorful";
    private const string TrayIconStyleBlack = "Black";
    private const string TrayIconStyleWhite = "White";
    private const string CornerDefault = SettingsService.WidgetCornerPreferenceDefault;
    private const string CornerSquare = SettingsService.WidgetCornerPreferenceSquare;
    private const string CornerSmall = SettingsService.WidgetCornerPreferenceSmall;
    private const string CornerRound = SettingsService.WidgetCornerPreferenceRound;
    private const string MaterialMica = SettingsService.WidgetMaterialTypeMica;
    private const string MaterialMicaAlt = SettingsService.WidgetMaterialTypeMicaAlt;
    private const string MaterialAcrylic = SettingsService.WidgetMaterialTypeAcrylic;
    private const string MaterialAcrylicBase = SettingsService.WidgetMaterialTypeAcrylicBase;
    private const string MaterialSolid = SettingsService.WidgetMaterialTypeSolid;
    private const string BorderColorNeutral = SettingsService.WidgetBorderColorModeNeutral;
    private const string BorderColorAccent = SettingsService.WidgetBorderColorModeAccent;
    private const string BorderColorNone = SettingsService.WidgetBorderColorModeNone;
    private const string BorderThin = SettingsService.WidgetBorderStyleThin;
    private const string BorderMedium = SettingsService.WidgetBorderStyleMedium;
    private const string BorderThick = SettingsService.WidgetBorderStyleThick;
    private const string AnimationPresetNone = "None";
    private const string AnimationPresetFade = "Fade";
    private const string RepositoryUrl = "https://github.com/TCOTC/BentoDesk";
    private const string OfficialWebsiteUrl = "https://github.com/TCOTC/BentoDesk";

    private readonly SettingsService _settingsService;
    private readonly ThemeService _themeService;
    private readonly LocalizationService _localizationService;
    private readonly WidgetContentFactory _widgetContentFactory;
    private readonly IAppUpdateService _appUpdateService;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private bool _isDisposed;
    private CancellationTokenSource? _updateOperationCts;
    private AppUpdateManifest? _availableUpdateManifest;
    private string? _downloadedUpdateInstallerPath;
    private bool _showManualUpdateFallback;
    private ImageSource? _donationWechatImageSource;
    private ImageSource? _donationAlipayImageSource;
    private Color _currentAccentColor;
    private string _selectedTheme = ThemeSystem;
    private string _selectedTrayIconStyle = TrayIconStyleSystem;
    private string _selectedWidgetCornerPreference = CornerRound;
    private string _selectedWidgetMaterialType = MaterialMica;
    private string _selectedWidgetBorderColorMode = BorderColorNeutral;
    private string _selectedWidgetBorderStyle = BorderThin;
    private string _selectedWidgetCollapseBehavior = SettingsService.WidgetCollapseBehaviorClick;
    private string _selectedLayoutDensity = SettingsService.LayoutDensityStandard;
    private string _selectedAnimationPreset = AnimationPresetFade;
    private string _selectedWidgetAnimationEffect = SettingsService.WidgetAnimationEffectFade;
    private string _selectedWidgetAnimationSpeed = SettingsService.WidgetAnimationSpeedStandard;
    private string _selectedWidgetAnimationSlideDirection = SettingsService.WidgetAnimationSlideDirectionNone;
    private string _selectedWidgetAnimationEasingIntensity = SettingsService.WidgetAnimationEasingStandard;
    private string _selectedDisplayWidgetChromeMode = SettingsService.WidgetChromeModeOverlay;
    private string _selectedInteractiveWidgetChromeMode = SettingsService.WidgetChromeModeStandard;
    private string _selectedWidgetTitleIconMode = SettingsService.WidgetTitleIconModeColor;
    private string _selectedManagedDropAction = SettingsService.ManagedDropActionMove;
    private string _selectedMusicDisplayMode = SettingsService.MusicDisplayModeAuto;
    private bool _useSystemAccentColor;
    private string _accentColorHex = AccentColorHelper.DefaultAccentColorHex;
    private bool _globalHotkeyEnabled;
    private string _globalHotkeyText = string.Empty;
    private string _globalHotkeyStatusText = string.Empty;
    private string _globalHotkeyStatusKind = "Normal";
    private DragDropPermissionDiagnostic? _dragDropPermissionDiagnostic;
    private string _dragDropPermissionRepairStatusText = string.Empty;
    private bool _isDragDropPermissionRepairing;
    private bool _isRestoringDefaults;
    private bool _isApplyingSettingsSnapshot;
    private bool _isApplyingLayoutDensityPreset;
    private bool _isApplyingAnimationPreset;
    private bool _isUpdatingHoverButtonActionSelection;

    private string[]? _cachedTrayIconStyleDisplayNames;
    private string[]? _cachedThemeDisplayNames;
    private string[]? _cachedWidgetCornerPreferenceDisplayNames;
    private string[]? _cachedWidgetMaterialTypeDisplayNames;
    private string[]? _cachedWidgetBorderColorModeDisplayNames;
    private string[]? _cachedWidgetBorderStyleDisplayNames;
    private string[]? _cachedWidgetCollapseBehaviorDisplayNames;
    private string[]? _cachedLayoutDensityDisplayNames;
    private string[]? _cachedAnimationPresetDisplayNames;
    private string[]? _cachedDisplayWidgetChromeModeDisplayNames;
    private string[]? _cachedInteractiveWidgetChromeModeDisplayNames;
    private string[]? _cachedWidgetTitleIconModeDisplayNames;
    private string[]? _cachedManagedDropActionDisplayNames;
    private string[]? _cachedMusicDisplayModeDisplayNames;

    [ObservableProperty]
    public partial bool AutoStart { get; set; }

    [ObservableProperty]
    public partial bool AutoCheckForUpdates { get; set; } = true;

    [ObservableProperty]
    public partial bool DoubleClickToOpen { get; set; }

    [ObservableProperty]
    public partial bool DoubleClickDesktopToHideAll { get; set; }

    [ObservableProperty]
    public partial double DefaultWidth { get; set; }

    [ObservableProperty]
    public partial double DefaultHeight { get; set; }

    [ObservableProperty]
    public partial bool HideShortcutArrowOverlay { get; set; }

    [ObservableProperty]
    public partial bool ShowImageFilesAsIcons { get; set; }

    [ObservableProperty]
    public partial bool ShowHoverButtons { get; set; } = true;

    [ObservableProperty]
    public partial bool ResizeSnapEnabled { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowHoverActionLockPosition { get; set; }

    [ObservableProperty]
    public partial bool ShowHoverActionLockSize { get; set; }

    [ObservableProperty]
    public partial bool ShowHoverActionAdd { get; set; }

    [ObservableProperty]
    public partial bool ShowHoverActionMore { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowHoverActionDelete { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowListItemDetails { get; set; }

    [ObservableProperty]
    public partial bool ShowFileItemPathTooltips { get; set; } = true;

    [ObservableProperty]
    public partial double WidgetOpacity { get; set; } = SettingsService.DefaultWidgetOpacity;

    [ObservableProperty]
    public partial double WidgetMaterialIntensity { get; set; } = SettingsService.DefaultWidgetMaterialIntensity;

    [ObservableProperty]
    public partial double IconSize { get; set; } = SettingsService.DefaultIconSize;

    [ObservableProperty]
    public partial double TextSize { get; set; } = SettingsService.DefaultTextSize;

    [ObservableProperty]
    public partial double LayoutDensityScale { get; set; } = SettingsService.DefaultLayoutDensityScale;

    [ObservableProperty]
    public partial double HorizontalSpacingScale { get; set; } = SettingsService.DefaultHorizontalSpacingScale;

    [ObservableProperty]
    public partial double VerticalSpacingScale { get; set; } = SettingsService.DefaultVerticalSpacingScale;

    [ObservableProperty]
    public partial double FileNameWidthScale { get; set; } = SettingsService.DefaultFileNameWidthScale;

    [ObservableProperty]
    public partial bool ShowFileExtensions { get; set; }

    [ObservableProperty]
    public partial bool HideShortcutExtensionWhenShowingFileExtensions { get; set; } = true;

    [ObservableProperty]
    public partial bool MusicUseArtworkBackdrop { get; set; } = true;

    [ObservableProperty]
    public partial bool MusicEnableCoverHoverMotion { get; set; } = true;

    [ObservableProperty]
    public partial bool IsCheckingForUpdates { get; set; }

    [ObservableProperty]
    public partial bool IsDownloadingUpdate { get; set; }

    [ObservableProperty]
    public partial string UpdateStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UpdateDetailText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double UpdateProgressValue { get; set; }

    public SettingsViewModel(
        SettingsService settingsService,
        ThemeService themeService,
        LocalizationService? localizationService = null,
        IAppUpdateService? appUpdateService = null)
    {
        _settingsService = settingsService;
        _themeService = themeService;
        _localizationService = localizationService ?? new LocalizationService(settingsService);
        _widgetContentFactory = new WidgetContentFactory(_localizationService);
        _appUpdateService = appUpdateService ?? new AppUpdateService();
        _dragDropPermissionRepairStatusText = string.Empty;

        var settings = settingsService.Settings;
        bool wasRestoringDefaults = _isRestoringDefaults;
        _isRestoringDefaults = true;
        try
        {
            UpdateStatusText = _localizationService.T("Settings.Update.Status.Ready");
            UpdateDetailText = GetReadyUpdateDetailText();

            _selectedTheme = settings.Theme is ThemeLight or ThemeDark ? settings.Theme : ThemeSystem;
            _selectedTrayIconStyle = settings.TrayIconStyle is TrayIconStyleColorful or TrayIconStyleBlack or TrayIconStyleWhite
                ? settings.TrayIconStyle
                : TrayIconStyleSystem;

            _useSystemAccentColor = !string.Equals(settings.AccentColorMode, ThemeService.AccentModeCustom, StringComparison.OrdinalIgnoreCase);
            AutoStart = StartupService.IsEnabled();
            AutoCheckForUpdates = settings.AutoCheckForUpdates;
            DoubleClickToOpen = settings.DoubleClickToOpen;
            DoubleClickDesktopToHideAll = settings.DoubleClickDesktopToHideAll;
            DefaultWidth = settings.DefaultWidgetWidth;
            DefaultHeight = settings.DefaultWidgetHeight;
            HideShortcutArrowOverlay = settings.HideShortcutArrowOverlay;
            ShowImageFilesAsIcons = settings.ShowImageFilesAsIcons;
            ShowHoverButtons = settings.ShowHoverButtons;
            ResizeSnapEnabled = settings.ResizeSnapEnabled;
            ApplyHoverButtonActionSelection(settings.WidgetHoverButtonActions);
            ShowListItemDetails = settings.ShowListItemDetails;
            ShowFileItemPathTooltips = settings.ShowFileItemPathTooltips;
            InitializeFileStackSettings(settings);
            WidgetOpacity = settings.WidgetOpacity;
            WidgetMaterialIntensity = settings.WidgetMaterialIntensity;
            _selectedWidgetCornerPreference = settings.WidgetCornerPreference is CornerDefault or CornerSquare or CornerSmall or CornerRound
                ? settings.WidgetCornerPreference
                : CornerSmall;
            _selectedWidgetMaterialType = settings.WidgetMaterialType is
                MaterialMica or MaterialMicaAlt or MaterialAcrylic or MaterialAcrylicBase or MaterialSolid
                ? settings.WidgetMaterialType
                : MaterialAcrylic;
            _selectedWidgetBorderColorMode = settings.WidgetBorderColorMode is
                BorderColorNeutral or BorderColorAccent or BorderColorNone
                    ? settings.WidgetBorderColorMode
                    : BorderColorNeutral;
            _selectedWidgetBorderStyle = settings.WidgetBorderStyle is BorderThin or BorderMedium or BorderThick
                ? settings.WidgetBorderStyle
                : BorderThin;
            _widgetCapsuleModeEnabled = settings.WidgetCapsuleModeEnabled;
            _selectedWidgetCompactWidthMode = SettingsService.NormalizeWidgetCompactWidthMode(
                settings.WidgetCompactWidthMode);
            _selectedWidgetCapsuleArrangementMode = SettingsService.NormalizeWidgetCapsuleArrangementMode(
                settings.WidgetCapsuleArrangementMode);
            _widgetCapsuleBarSpacing = SettingsService.NormalizeWidgetCapsuleBarSpacing(
                settings.WidgetCapsuleBarSpacing);
            _selectedWidgetCapsuleBarPlacement = SettingsService.NormalizeWidgetCapsuleBarPlacement(
                settings.WidgetCapsuleBarPlacement);
            _selectedWidgetCapsuleBarDirection = SettingsService.NormalizeWidgetCapsuleBarDirection(
                settings.WidgetCapsuleBarDirection);
            _selectedWidgetCollapseBehavior = SettingsService.NormalizeWidgetCollapseBehavior(settings.WidgetCollapseBehavior) == SettingsService.WidgetCollapseBehaviorSmart
                ? SettingsService.WidgetCollapseBehaviorSmart
                : SettingsService.WidgetCollapseBehaviorClick;
            _selectedWidgetCompactAnimationEffect = SettingsService.NormalizeWidgetCompactAnimationEffect(settings.WidgetCompactAnimationEffect);
            _widgetCompactAnimationDurationMs = SettingsService.NormalizeWidgetCompactAnimationDurationMs(settings.WidgetCompactAnimationDurationMs);
            _widgetCompactExpandDelayMs = SettingsService.NormalizeWidgetCompactExpandDelayMs(settings.WidgetCompactExpandDelayMs);
            _widgetCompactCollapseDelayMs = SettingsService.NormalizeWidgetCompactCollapseDelayMs(settings.WidgetCompactCollapseDelayMs);
            _selectedWidgetCompactHoverResponse = SettingsService.ResolveWidgetCompactHoverResponse(
                settings.WidgetCompactExpandDelayMs,
                settings.WidgetCompactCollapseDelayMs);
            _selectedWidgetCompactMediaCornerMode = SettingsService.NormalizeWidgetCompactMediaCornerMode(settings.WidgetCompactMediaCornerMode);
            _selectedWidgetAnimationEffect = NormalizeWidgetAnimationEffect(settings.WidgetAnimationEffect);
            _selectedWidgetAnimationSpeed = NormalizeWidgetAnimationSpeed(settings.WidgetAnimationSpeed);
            _selectedWidgetAnimationSlideDirection = NormalizeWidgetAnimationSlideDirection(settings.WidgetAnimationSlideDirection);
            _selectedWidgetAnimationEasingIntensity = NormalizeWidgetAnimationEasingIntensity(settings.WidgetAnimationEasingIntensity);
            _selectedAnimationPreset = ResolveAnimationPreset();
            _selectedDisplayWidgetChromeMode = NormalizeWidgetChromeModeSetting(settings.DisplayWidgetChromeMode, WidgetChromeMode.Overlay);
            _selectedInteractiveWidgetChromeMode = NormalizeWidgetChromeModeSetting(settings.InteractiveWidgetChromeMode, WidgetChromeMode.Standard);
            _selectedWidgetTitleIconMode = NormalizeWidgetTitleIconModeSetting(settings.WidgetTitleIconMode);
            IconSize = settings.IconSize;
            TextSize = settings.TextSize;
            LayoutDensityScale = settings.LayoutDensityScale;
            HorizontalSpacingScale = settings.HorizontalSpacingScale;
            VerticalSpacingScale = settings.VerticalSpacingScale;
            FileNameWidthScale = settings.FileNameWidthScale;
            _selectedLayoutDensity = SettingsService.ResolveLayoutDensityPreset(settings);
            ShowFileExtensions = settings.ShowFileExtensions;
            HideShortcutExtensionWhenShowingFileExtensions = settings.HideShortcutExtensionWhenShowingFileExtensions;
            _selectedManagedDropAction = settings.ManagedDropAction == SettingsService.ManagedDropActionMove
                ? SettingsService.ManagedDropActionMove
                : SettingsService.ManagedDropActionCopy;
            MusicUseArtworkBackdrop = settings.MusicUseArtworkBackdrop;
            MusicEnableCoverHoverMotion = settings.MusicEnableCoverHoverMotion;
            _selectedMusicDisplayMode = SettingsService.NormalizeMusicDisplayMode(settings.MusicDisplayMode);
        }
        finally
        {
            _isRestoringDefaults = wasRestoringDefaults;
        }

        ApplyCachedUpdateResult();
        RefreshAccentPreview();
        RefreshDragDropPermissionDiagnostic();
        _settingsService.SettingsChanged += OnSettingsChanged;
        _themeService.AppearanceChanged += OnAppearanceChanged;
        _localizationService.LanguageChanged += OnLanguageChanged;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _settingsService.SaveAsync();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _lifetimeCts.Cancel();
        _updateOperationCts?.Cancel();
        _updateOperationCts?.Dispose();
        _settingsService.SettingsChanged -= OnSettingsChanged;
        _themeService.AppearanceChanged -= OnAppearanceChanged;
        _localizationService.LanguageChanged -= OnLanguageChanged;
        DisposeFileStackSettings();
        _lifetimeCts.Dispose();
    }

    private void OnAppearanceChanged()
    {
        RefreshAccentPreview();
    }

    private void RefreshAccentPreview()
    {
        _currentAccentColor = _themeService.GetEffectiveAccentColor();
        AccentPreviewBrush.Color = _currentAccentColor;
        AccentColorHex = AccentColorHelper.ToHex(_currentAccentColor);
        OnPropertyChanged(nameof(SelectedAccentColor));
    }

    private static string FormatNumber(double value, int decimals)
    {
        string format = decimals <= 0 ? "0" : $"0.{new string('#', decimals)}";
        return value.ToString(format, CultureInfo.CurrentCulture);
    }

    public string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return string.Format(CultureInfo.CurrentCulture, 
                $"{Math.Max(0, bytes)} {_localizationService.T("Size.Unit.Bytes")}", 
                CultureInfo.CurrentCulture);
        }

        var units = new[] 
        {
            _localizationService.T("Size.Unit.KB"),
            _localizationService.T("Size.Unit.MB"),
            _localizationService.T("Size.Unit.GB")
        };
        double value = bytes;
        int unitIndex = -1;
        do
        {
            value /= 1024d;
            unitIndex++;
        }
        while (value >= 1024d && unitIndex < units.Length - 1);

        return string.Format(CultureInfo.CurrentCulture, 
            $"{value:0.#} {units[unitIndex]}", 
            CultureInfo.CurrentCulture);
    }

    private void ApplyNumberInput(
        string? value,
        Func<double> getCurrentValue,
        Action<double> setValue,
        double min,
        double max,
        int decimals)
    {
        if (!TryParseNumberInput(value, out double parsedValue))
        {
            RefreshNumberInputs();
            return;
        }

        double multiplier = Math.Pow(10, Math.Max(0, decimals));
        double normalizedValue = Math.Clamp(Math.Round(parsedValue * multiplier, MidpointRounding.AwayFromZero) / multiplier, min, max);
        if (Math.Abs(normalizedValue - getCurrentValue()) > 0.0001)
        {
            setValue(normalizedValue);
        }

        RefreshNumberInputs();
    }

    private void RefreshNumberInputs()
    {
        OnPropertyChanged(nameof(DefaultWidthInput));
        OnPropertyChanged(nameof(DefaultHeightInput));
        OnPropertyChanged(nameof(WidgetOpacityPercentInput));
        OnPropertyChanged(nameof(WidgetTransparency));
        OnPropertyChanged(nameof(IconSizeInput));
        OnPropertyChanged(nameof(TextSizeInput));
        OnPropertyChanged(nameof(LayoutDensityPercentInput));
        OnPropertyChanged(nameof(HorizontalSpacingPercentInput));
        OnPropertyChanged(nameof(VerticalSpacingPercentInput));
        OnPropertyChanged(nameof(FileNameWidthPercentInput));
    }

    private static bool TryParseNumberInput(string? value, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out result) ||
               double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    private void ApplyLayoutDensityPreset(string preset)
    {
        if (!SettingsService.TryGetLayoutDensityPresetValues(preset, out LayoutDensityPresetValues values))
        {
            return;
        }

        _isApplyingLayoutDensityPreset = true;
        try
        {
            IconSize = values.IconSize;
            TextSize = values.TextSize;
            LayoutDensityScale = values.DensityScale;
            HorizontalSpacingScale = values.HorizontalSpacingScale;
            VerticalSpacingScale = values.VerticalSpacingScale;
            FileNameWidthScale = values.FileNameWidthScale;
            _settingsService.Settings.LayoutDensity = preset;
        }
        finally
        {
            _isApplyingLayoutDensityPreset = false;
        }

        RefreshNumberInputs();
        SaveAppearanceChange();
    }

    private void SyncLayoutDensitySelection()
    {
        if (_isApplyingLayoutDensityPreset || _isRestoringDefaults || _isApplyingSettingsSnapshot)
        {
            return;
        }

        _settingsService.Settings.LayoutDensity = SettingsService.LayoutDensityCustom;
        if (SetProperty(
            ref _selectedLayoutDensity,
            SettingsService.LayoutDensityCustom,
            nameof(SelectedLayoutDensity)))
        {
            OnPropertyChanged(nameof(SelectedLayoutDensityText));
        }
    }

    private void ApplyAnimationPreset(string preset)
    {
        (string effect, string speed, string direction, string easing) = preset == AnimationPresetNone
            ? (
                SettingsService.WidgetAnimationEffectNone,
                SettingsService.WidgetAnimationSpeedStandard,
                SettingsService.WidgetAnimationSlideDirectionNone,
                SettingsService.WidgetAnimationEasingNone)
            : (
                SettingsService.WidgetAnimationEffectFade,
                SettingsService.WidgetAnimationSpeedStandard,
                SettingsService.WidgetAnimationSlideDirectionNone,
                SettingsService.WidgetAnimationEasingStandard);

        _isApplyingAnimationPreset = true;
        try
        {
            SelectedWidgetAnimationEffect = effect;
            SelectedWidgetAnimationSpeed = speed;
            SelectedWidgetAnimationSlideDirection = direction;
            SelectedWidgetAnimationEasingIntensity = easing;
        }
        finally
        {
            _isApplyingAnimationPreset = false;
        }

        _settingsService.SaveDebounced();
    }

    private void SyncAnimationPresetSelection()
    {
        if (_isApplyingAnimationPreset || _isRestoringDefaults || _isApplyingSettingsSnapshot)
        {
            return;
        }

        string resolvedPreset = ResolveAnimationPreset();
        if (SetProperty(ref _selectedAnimationPreset, resolvedPreset, nameof(SelectedAnimationPreset)))
        {
            OnPropertyChanged(nameof(SelectedAnimationPresetText));
        }
    }

    private string ResolveAnimationPreset()
    {
        return _selectedWidgetAnimationEffect == SettingsService.WidgetAnimationEffectNone
            ? AnimationPresetNone
            : AnimationPresetFade;
    }

    private void ApplySpacingScaleChange(
        double value,
        double currentStoredValue,
        Action<double> setViewModelValue,
        Action<double> setStoredValue,
        params string[] dependentPropertyNames)
    {
        if (double.IsNaN(value))
        {
            setViewModelValue(currentStoredValue);
            return;
        }

        double normalizedValue = Math.Clamp(
            Math.Round(value / 0.02d, MidpointRounding.AwayFromZero) * 0.02d,
            SettingsService.MinSpacingScale,
            SettingsService.MaxSpacingScale);

        if (Math.Abs(normalizedValue - value) > 0.0001)
        {
            setViewModelValue(normalizedValue);
            return;
        }

        setStoredValue(normalizedValue);
        SyncLayoutDensitySelection();
        SaveAppearanceChange();
        foreach (string propertyName in dependentPropertyNames)
        {
            OnPropertyChanged(propertyName);
        }
    }

    private void SaveAppearanceChange()
    {
        if (_isApplyingLayoutDensityPreset)
        {
            return;
        }

        if (DeferAppearancePersistence)
        {
            _settingsService.RequestAppearancePreview();
            return;
        }

        if (!SuppressAppearanceNotifications)
        {
            _settingsService.RequestAppearancePreview();
        }

        _settingsService.SaveDebounced(notifySubscribers: !SuppressAppearanceNotifications);
    }

}
