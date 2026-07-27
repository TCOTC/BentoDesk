using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.CompilerServices;
using BentoDesk.Helpers;
using BentoDesk.Models;

[assembly: InternalsVisibleTo("BentoDesk.Tests")]

namespace BentoDesk.Services;

public readonly record struct LayoutDensityPresetValues(
    double IconSize,
    double TextSize,
    double DensityScale,
    double HorizontalSpacingScale,
    double VerticalSpacingScale,
    double FileNameWidthScale);

internal enum DefaultPreferencePreservationReason
{
    UserChoice,
    SystemIntegration,
    UserData,
    RuntimeState
}

/// <summary>
/// Manages application settings persistence using JSON files stored in the application directory.
/// </summary>
public sealed class SettingsService
{
    public const double DefaultWidgetOpacity = 0.80;
    public const double MinWidgetOpacity = 0.0;
    public const double MaxWidgetOpacity = 1.0;
    public const double DefaultWidgetMaterialIntensity = 0.65;
    public const double MinWidgetMaterialIntensity = 0.0;
    public const double MaxWidgetMaterialIntensity = 1.0;
    public const string WidgetMaterialTypeMica = "Mica";
    public const string WidgetMaterialTypeMicaAlt = "MicaAlt";
    public const string WidgetMaterialTypeAcrylic = "Acrylic";
    public const string WidgetMaterialTypeAcrylicBase = "AcrylicBase";
    public const string WidgetMaterialTypeSolid = "Solid";
    public const string WidgetBorderColorModeNeutral = "Neutral";
    public const string WidgetBorderColorModeAccent = "Accent";
    public const string WidgetBorderColorModeNone = "None";
    public const string WidgetBorderStyleThin = "Thin";
    public const string WidgetBorderStyleMedium = "Medium";
    public const string WidgetBorderStyleThick = "Thick";
    public const string WidgetCornerPreferenceDefault = "Default";
    public const string WidgetCornerPreferenceSquare = "Square";
    public const string WidgetCornerPreferenceSmall = "Small";
    public const string WidgetCornerPreferenceRound = "Round";
    public const string WidgetAnimationEffectNone = "None";
    public const string WidgetAnimationEffectFade = "Fade";
    public const string WidgetAnimationEffectScaleFade = "ScaleFade";
    public const string WidgetAnimationEffectSlideFade = "SlideFade";
    public const string WidgetAnimationEffectZoom = "Zoom";
    public const string WidgetAnimationSpeedVeryFast = "VeryFast";
    public const string WidgetAnimationSpeedFast = "Fast";
    public const string WidgetAnimationSpeedStandard = "Standard";
    public const string WidgetAnimationSpeedRelaxed = "Relaxed";
    public const string WidgetAnimationSpeedSlow = "Slow";
    public const string WidgetAnimationSlideDirectionNone = "None";
    public const string WidgetAnimationSlideDirectionLeft = "Left";
    public const string WidgetAnimationSlideDirectionRight = "Right";
    public const string WidgetAnimationSlideDirectionUp = "Up";
    public const string WidgetAnimationSlideDirectionDown = "Down";
    public const string WidgetAnimationEasingNone = "None";
    public const string WidgetAnimationEasingLight = "Light";
    public const string WidgetAnimationEasingStandard = "Standard";
    public const string WidgetAnimationEasingStrong = "Strong";

    public static bool IsMicaMaterial(string? materialType) =>
        materialType is WidgetMaterialTypeMica or WidgetMaterialTypeMicaAlt;

    public static bool IsAcrylicMaterial(string? materialType) =>
        materialType is WidgetMaterialTypeAcrylic or WidgetMaterialTypeAcrylicBase;

    public static bool SupportsWidgetOpacity(string? materialType) =>
        IsAcrylicMaterial(materialType);

    public static bool SupportsMaterialIntensity(string? materialType) =>
        IsMicaMaterial(materialType) || IsAcrylicMaterial(materialType);
    public const string WidgetCollapseBehaviorExpanded = WidgetCollapseBehaviorNames.Expanded;
    public const string WidgetCollapseBehaviorClick = WidgetCollapseBehaviorNames.Click;
    public const string WidgetCollapseBehaviorSmart = WidgetCollapseBehaviorNames.Smart;
    public const string WidgetCompactContentModeMinimal = "Minimal";
    public const string WidgetCompactContentModeSummary = "Summary";
    public const string WidgetCompactContentModeSmart = "Smart";
    public const string WidgetCompactAnimationSmooth = "Smooth";
    public const string WidgetCompactAnimationSlow = "Slow";
    public const string WidgetCompactAnimationSnappy = "Snappy";
    public const string WidgetCompactAnimationCustom = "Custom";
    public const string WidgetCompactAnimationNone = "None";
    public const string WidgetCompactMediaCornerFollowWidget = "FollowWidget";
    public const string WidgetCompactMediaCornerSquare = "Square";
    public const string WidgetCompactMediaCornerSmall = "Small";
    public const string WidgetCompactMediaCornerRound = "Round";
    public const int DefaultWidgetCompactAnimationDurationMs = 220;
    public const int SlowWidgetCompactAnimationDurationMs = 360;
    public const int SnappyWidgetCompactAnimationDurationMs = 160;
    public const int MinWidgetCompactAnimationDurationMs = 120;
    public const int MaxWidgetCompactAnimationDurationMs = 400;
    public const int DefaultWidgetCompactExpandDelayMs = 360;
    public const int MinWidgetCompactExpandDelayMs = 100;
    public const int MaxWidgetCompactExpandDelayMs = 1000;
    public const int DefaultWidgetCompactCollapseDelayMs = 620;
    public const int MinWidgetCompactCollapseDelayMs = 200;
    public const int MaxWidgetCompactCollapseDelayMs = 1500;
    public const string WidgetCompactHoverResponseSensitive = "Sensitive";
    public const string WidgetCompactHoverResponseBalanced = "Balanced";
    public const string WidgetCompactHoverResponsePreventAccidental = "PreventAccidental";
    public const string WidgetCompactHoverResponseCustom = "Custom";
    public const int SensitiveWidgetCompactExpandDelayMs = 180;
    public const int SensitiveWidgetCompactCollapseDelayMs = 420;
    public const int PreventAccidentalWidgetCompactExpandDelayMs = 620;
    public const int PreventAccidentalWidgetCompactCollapseDelayMs = 900;
    public const string WidgetTitleIconModeFilledMono = WidgetTitleIconModeNames.FilledMono;
    public const string WidgetTitleIconModeLineMono = WidgetTitleIconModeNames.LineMono;
    public const string WidgetTitleIconModeColor = WidgetTitleIconModeNames.Color;
    public const string WidgetTitleIconModeHidden = WidgetTitleIconModeNames.Hidden;
    public const string WidgetTitleIconModeTextLabel = WidgetTitleIconModeNames.TextLabel;
    public const string ManagedDropActionMove = "Move";
    public const string ManagedDropActionCopy = "Copy";

    public const string FileStackGroupByKind = "Kind";
    public const string FileStackGroupByDateModified = "DateModified";
    public const string FileStackGroupByCustom = "Custom";
    public const int DefaultFileStackThreshold = 3;
    public const string FileStackOrderByWidget = "Widget";
    public const string FileStackOrderByName = "Name";
    public const string FileStackOrderByDateAdded = "DateAdded";
    public const string FileStackOrderByDateModified = "DateModified";
    public const string FileStackUnmatchedKeepLoose = "KeepLoose";
    public const string FileStackUnmatchedOther = "Other";
    public const double DefaultWidgetWidth = 280;
    public const double DefaultWidgetHeight = 400;
    public const bool DefaultGlobalHotkeyEnabled = true;
    public const int DefaultGlobalHotkeyModifiers = (int)Models.HotkeyModifierKeys.None;
    public const int DefaultGlobalHotkeyKey = (int)Windows.System.VirtualKey.F7;
    public const double MinWidgetWidth = 150;
    public const double MinWidgetHeight = 150;
    public const double DefaultIconSize = 30;
    public const double MinIconSize = 24;
    public const double MaxIconSize = 56;
    public const double DefaultTextSize = 11.5;
    public const double MinTextSize = 10;
    public const double MaxTextSize = 16;
    public const double DefaultLayoutDensityScale = 0.56;
    public const double MinLayoutDensityScale = 0.0;
    public const double MaxLayoutDensityScale = 1.0;
    public const double DefaultHorizontalSpacingScale = 0.40;
    public const double DefaultVerticalSpacingScale = 0.60;
    public const double DefaultFileNameWidthScale = 0.36;
    public const double MinSpacingScale = 0.0;
    public const double MaxSpacingScale = 1.0;
    public const string LayoutDensityCompact = "Compact";
    public const string LayoutDensityStandard = "Standard";
    public const string LayoutDensityRelaxed = "Relaxed";
    public const string LayoutDensityCustom = "Custom";
    public const string MusicDisplayModeAuto = "Auto";
    public const string MusicDisplayModeCover = "Cover";
    public const string MusicDisplayModeControls = "Controls";
    public const string MusicDisplayModeRecordVertical = "RecordVertical";
    public const string MusicDisplayModeRecordHorizontal = "RecordHorizontal";
    public const int MaxRecentOrganizationHistoryCount = 24;
    public const string WidgetTabStylePivot = "Pivot";
    public const string WidgetTabStyleButton = "Button";

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    internal static IReadOnlyDictionary<string, DefaultPreferencePreservationReason>
        DefaultPreferencePreservationPolicy { get; } =
            new Dictionary<string, DefaultPreferencePreservationReason>(StringComparer.Ordinal)
            {
                [nameof(AppSettings.AutoStart)] = DefaultPreferencePreservationReason.SystemIntegration,
                [nameof(AppSettings.FeatureWidgetEnabledStates)] = DefaultPreferencePreservationReason.UserChoice,
                [nameof(AppSettings.Widgets)] = DefaultPreferencePreservationReason.UserData,
                [nameof(AppSettings.DeletedWidgetIds)] = DefaultPreferencePreservationReason.UserData,
                [nameof(AppSettings.RecentOrganizationHistory)] = DefaultPreferencePreservationReason.UserData,
                [nameof(AppSettings.HasCompletedOnboarding)] = DefaultPreferencePreservationReason.RuntimeState,
                [nameof(AppSettings.LastUpdateCheckAt)] = DefaultPreferencePreservationReason.RuntimeState,
                [nameof(AppSettings.SchemaVersion)] = DefaultPreferencePreservationReason.RuntimeState
            };

    private readonly string _settingsPath;
    private AppSettings _settings = new();
    private readonly object _lock = new();
    private readonly SemaphoreSlim _fileWriteLock = new(1, 1);
    private CancellationTokenSource? _debounceCts;
    private CancellationTokenSource? _appearancePreviewCts;

    public event Action? SettingsChanged;
    public event Action? AppearancePreviewChanged;

    public AppSettings Settings
    {
        get { lock (_lock) return _settings; }
    }

    /// <summary>
    /// Restores user preference defaults without touching user data, widget instances, or storage paths.
    /// </summary>
    public static void ApplyDefaultPreferences(AppSettings settings)
    {
        settings.Theme = "System";
        settings.TrayIconStyle = "Colorful";
        settings.AccentColorMode = "System";
        settings.DefaultWidgetWidth = DefaultWidgetWidth;
        settings.DefaultWidgetHeight = DefaultWidgetHeight;
        settings.WidgetCornerPreference = WidgetCornerPreferenceRound;
        settings.WidgetMaterialType = WidgetMaterialTypeMica;
        settings.WidgetMaterialIntensity = DefaultWidgetMaterialIntensity;
        settings.WidgetBorderColorMode = WidgetBorderColorModeNeutral;
        settings.WidgetBorderStyle = WidgetBorderStyleThin;
        settings.WidgetAnimationEffect = WidgetAnimationEffectFade;
        settings.WidgetAnimationSpeed = WidgetAnimationSpeedStandard;
        settings.WidgetAnimationSlideDirection = WidgetAnimationSlideDirectionNone;
        settings.WidgetAnimationEasingIntensity = WidgetAnimationEasingStandard;
        settings.WidgetCollapseBehavior = WidgetCollapseBehaviorSmart;
        settings.WidgetCompactContentMode = WidgetCompactContentModeSmart;
        settings.WidgetCompactHideSensitiveContent = false;
        settings.WidgetCompactAnimationEffect = WidgetCompactAnimationSlow;
        settings.WidgetCompactAnimationDurationMs = DefaultWidgetCompactAnimationDurationMs;
        settings.WidgetCompactExpandDelayMs = SensitiveWidgetCompactExpandDelayMs;
        settings.WidgetCompactCollapseDelayMs = SensitiveWidgetCompactCollapseDelayMs;
        settings.WidgetCompactMediaCornerMode = WidgetCompactMediaCornerFollowWidget;
        settings.WidgetTitleIconMode = WidgetTitleIconModeColor;
        settings.WidgetOpacity = DefaultWidgetOpacity;
        settings.IconSize = DefaultIconSize;
        settings.TextSize = DefaultTextSize;
        settings.LayoutDensityScale = DefaultLayoutDensityScale;
        settings.LayoutDensity = LayoutDensityStandard;
        settings.HorizontalSpacingScale = DefaultHorizontalSpacingScale;
        settings.VerticalSpacingScale = DefaultVerticalSpacingScale;
        settings.FileNameWidthScale = DefaultFileNameWidthScale;
        settings.ShowFileExtensions = false;
        settings.ShowImageFilesAsIcons = false;
        settings.FileStacksEnabled = false;
        settings.FileStackGroupBy = FileStackGroupByKind;
        settings.FileStackThreshold = DefaultFileStackThreshold;
        settings.FileStackOrderBy = FileStackOrderByWidget;
        settings.FileStackCustomRules = [];
        settings.FileStackUnmatchedBehavior = FileStackUnmatchedKeepLoose;
        settings.HideShortcutExtensionWhenShowingFileExtensions = true;
        settings.AutoCheckForUpdates = true;
        settings.MusicUseArtworkBackdrop = true;
        settings.MusicEnableCoverHoverMotion = true;
        settings.MusicDisplayMode = MusicDisplayModeAuto;
        settings.ManagedDropAction = ManagedDropActionMove;
        settings.GlobalHotkeyEnabled = DefaultGlobalHotkeyEnabled;
        settings.GlobalHotkeyModifiers = DefaultGlobalHotkeyModifiers;
        settings.GlobalHotkeyKey = DefaultGlobalHotkeyKey;
        settings.DoubleClickDesktopToHideAll = false;
        settings.HideShortcutArrowOverlay = true;
        settings.ResizeSnapEnabled = true;
settings.ShowListItemDetails = false;
settings.ShowFileItemPathTooltips = true;
settings.CustomAccentColor = "#0078D4";
settings.FocusClickedWidgetOnRaise = false;
    }

    public SettingsService()
    {
        string dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BentoDesk",
            "data");
        _settingsPath = InitializeSettingsPath(dataDir);
    }

    internal SettingsService(string dataDir)
    {
        _settingsPath = InitializeSettingsPath(dataDir);
    }

    private static string InitializeSettingsPath(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        return Path.Combine(dataDir, "settings.json");
    }

    /// <summary>
    /// Load settings from disk. Creates default settings if file doesn't exist.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            bool loadedFromDisk = false;

            if (File.Exists(_settingsPath))
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, s_jsonOptions);
                if (loaded is not null)
                {
                    lock (_lock) _settings = loaded;
                    loadedFromDisk = true;
                }
            }

            bool changed;
            lock (_lock)
            {
                if (!loadedFromDisk)
                {
                    ApplyDefaultPreferences(_settings);
                }

                // Run schema migrations if the loaded version is older than current
                var migrationPipeline = new SettingsMigrationPipeline();
                changed = migrationPipeline.RunMigrations(_settings);

                changed |= NormalizePresentationSettings(_settings);
                changed |= NormalizeAppearanceSettings(_settings);
                changed |= NormalizeFeatureWidgetSettings(_settings);
                changed |= NormalizeWidgetContentSettings(_settings);
                changed |= NormalizeOrganizerSettings(_settings);
                changed |= NormalizeHotkeySettings(_settings);
                changed |= NormalizeDeletionSettings(_settings);
            }

            if (changed)
            {
                await SaveToFileOnlyAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Failed to load settings: {ex.Message}");
            lock (_lock) _settings = new AppSettings();
        }
    }

    /// <summary>
    /// Save settings to disk immediately.
    /// </summary>
    public async Task SaveAsync(bool notifySubscribers = true)
    {
        await SaveToFileOnlyAsync();
        if (notifySubscribers)
        {
            SettingsChanged?.Invoke();
        }
    }

    private async Task SaveToFileOnlyAsync()
    {
        await _fileWriteLock.WaitAsync();
        try
        {
            string json;
            lock (_lock)
            {
                NormalizePresentationSettings(_settings);
                NormalizeAppearanceSettings(_settings);
                NormalizeFeatureWidgetSettings(_settings);
                NormalizeWidgetContentSettings(_settings);
                NormalizeOrganizerSettings(_settings);
                NormalizeHotkeySettings(_settings);
                json = JsonSerializer.Serialize(_settings, s_jsonOptions);
            }

            // Atomic write: serialize to a temp file, then rename to the target path.
            // This prevents corruption if the process crashes or power is lost mid-write.
            string? directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string tempPath = _settingsPath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, _settingsPath, overwrite: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Failed to save settings: {ex.Message}");
        }
        finally
        {
            _fileWriteLock.Release();
        }
    }

    /// <summary>
    /// Save settings with debouncing (waits 1 second after last call before actually saving).
    /// Use this for frequent changes like window drag/resize.
    /// </summary>
    public void SaveDebounced(bool notifySubscribers = true)
    {
        if (notifySubscribers)
        {
            SettingsChanged?.Invoke();
        }

        // Cancel and dispose the previous CTS to avoid leaking native
        // kernel event handles.  Each undisposed CTS holds a native handle
        // that is only reclaimed by the GC finalizer, which may not run
        // for a long time in a large-heap app.
        //
        // Note: The CTS may have already been disposed by a completed
        // Task.Run lambda's finally block (see below).  Catch
        // ObjectDisposedException defensively to handle this race.
        try
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
        }
        catch (ObjectDisposedException) { }
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1000, token);
                if (!token.IsCancellationRequested)
                {
                    await SaveToFileOnlyAsync();
                }
            }
            catch (TaskCanceledException) { }
            // Do NOT dispose the CTS here — _debounceCts may still
            // reference it, and disposing it here would cause the next
            // SaveDebounced call to throw ObjectDisposedException when
            // it tries to Cancel/Dispose the (already-disposed) CTS.
            // The CTS will be disposed by the next SaveDebounced call
            // or by the GC finalizer.
        });
    }

    public void RequestAppearancePreview()
    {
        // Dispose the previous CTS to avoid leaking native handles.
        try
        {
            _appearancePreviewCts?.Cancel();
            _appearancePreviewCts?.Dispose();
        }
        catch (ObjectDisposedException) { }
        _appearancePreviewCts = new CancellationTokenSource();
        var token = _appearancePreviewCts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(66, token);
                if (!token.IsCancellationRequested)
                {
                    AppearancePreviewChanged?.Invoke();
                }
            }
            catch (TaskCanceledException) { }
            // Do NOT dispose the CTS here — same rationale as SaveDebounced.
        });
    }

    public void NotifyAppearancePreviewNow()
    {
        _appearancePreviewCts?.Cancel();
        AppearancePreviewChanged?.Invoke();
    }

    /// <summary>
    /// Update a widget's configuration. If the widget doesn't exist, it will be added.
    /// </summary>
    public void UpdateWidget(WidgetConfig config, bool notifySubscribers = true)
    {
        lock (_lock)
        {
            if (_settings.DeletedWidgetIds.Contains(config.Id))
            {
                return;
            }

            var existing = _settings.Widgets.FindIndex(w => w.Id == config.Id);
            if (existing >= 0)
                _settings.Widgets[existing] = config;
            else
                _settings.Widgets.Add(config);
        }
        SaveDebounced(notifySubscribers);
    }

    /// <summary>
    /// Remove a widget configuration.
    /// </summary>
    public void RemoveWidget(string widgetId)
    {
        lock (_lock)
        {
            if (!_settings.DeletedWidgetIds.Contains(widgetId))
            {
                _settings.DeletedWidgetIds.Add(widgetId);
            }

            _settings.Widgets.RemoveAll(w => w.Id == widgetId);
        }
        SaveDebounced();
    }

    public void RemoveWidgetImmediate(string widgetId)
    {
        lock (_lock)
        {
            if (!_settings.DeletedWidgetIds.Contains(widgetId))
            {
                _settings.DeletedWidgetIds.Add(widgetId);
            }

            _settings.Widgets.RemoveAll(w => w.Id == widgetId);
        }
    }

    private static bool NormalizePresentationSettings(AppSettings settings)
    {
        bool changed = false;

        double normalizedWidgetOpacity = double.IsFinite(settings.WidgetOpacity)
            ? Math.Clamp(settings.WidgetOpacity, MinWidgetOpacity, MaxWidgetOpacity)
            : DefaultWidgetOpacity;
        if (Math.Abs(settings.WidgetOpacity - normalizedWidgetOpacity) > 0.0001)
        {
            settings.WidgetOpacity = normalizedWidgetOpacity;
            changed = true;
        }

        if (settings.WidgetCornerPreference is not (
            WidgetCornerPreferenceDefault or
            WidgetCornerPreferenceSquare or
            WidgetCornerPreferenceSmall or
            WidgetCornerPreferenceRound))
        {
            settings.WidgetCornerPreference = WidgetCornerPreferenceRound;
            changed = true;
        }

        if (settings.WidgetMaterialType is not (
            WidgetMaterialTypeMica or
            WidgetMaterialTypeMicaAlt or
            WidgetMaterialTypeAcrylic or
            WidgetMaterialTypeAcrylicBase or
            WidgetMaterialTypeSolid))
        {
            settings.WidgetMaterialType = WidgetMaterialTypeAcrylic;
            changed = true;
        }

        if (settings.WidgetMaterialType == WidgetMaterialTypeSolid &&
            Math.Abs(settings.WidgetOpacity - MaxWidgetOpacity) > 0.0001)
        {
            settings.WidgetOpacity = MaxWidgetOpacity;
            changed = true;
        }

        double normalizedMaterialIntensity = double.IsFinite(settings.WidgetMaterialIntensity)
            ? Math.Clamp(
                settings.WidgetMaterialIntensity,
                MinWidgetMaterialIntensity,
                MaxWidgetMaterialIntensity)
            : DefaultWidgetMaterialIntensity;
        if (Math.Abs(settings.WidgetMaterialIntensity - normalizedMaterialIntensity) > 0.0001)
        {
            settings.WidgetMaterialIntensity = normalizedMaterialIntensity;
            changed = true;
        }

        if (settings.WidgetBorderColorMode is not (
            WidgetBorderColorModeNeutral or
            WidgetBorderColorModeAccent or
            WidgetBorderColorModeNone))
        {
            settings.WidgetBorderColorMode = WidgetBorderColorModeNeutral;
            changed = true;
        }

        if (settings.WidgetBorderStyle is not (
            WidgetBorderStyleThin or
            WidgetBorderStyleMedium or
            WidgetBorderStyleThick))
        {
            settings.WidgetBorderStyle = WidgetBorderStyleThin;
            changed = true;
        }

        if (settings.WidgetAnimationEffect is not (
            WidgetAnimationEffectNone or
            WidgetAnimationEffectFade))
        {
            settings.WidgetAnimationEffect = WidgetAnimationEffectFade;
            changed = true;
        }

        if (settings.WidgetAnimationSpeed is not (
            WidgetAnimationSpeedVeryFast or
            WidgetAnimationSpeedFast or
            WidgetAnimationSpeedStandard or
            WidgetAnimationSpeedRelaxed or
            WidgetAnimationSpeedSlow))
        {
            settings.WidgetAnimationSpeed = WidgetAnimationSpeedStandard;
            changed = true;
        }

        if (settings.WidgetAnimationSlideDirection is not (
            WidgetAnimationSlideDirectionNone or
            WidgetAnimationSlideDirectionLeft or
            WidgetAnimationSlideDirectionRight or
            WidgetAnimationSlideDirectionUp or
            WidgetAnimationSlideDirectionDown))
        {
            settings.WidgetAnimationSlideDirection = WidgetAnimationSlideDirectionNone;
            changed = true;
        }

        if (settings.WidgetAnimationEasingIntensity is not (
            WidgetAnimationEasingNone or
            WidgetAnimationEasingLight or
            WidgetAnimationEasingStandard or
            WidgetAnimationEasingStrong))
        {
            settings.WidgetAnimationEasingIntensity = WidgetAnimationEasingStandard;
            changed = true;
        }

        if (settings.WidgetAnimationEffect == WidgetAnimationEffectNone)
        {
            if (settings.WidgetAnimationSpeed != WidgetAnimationSpeedStandard)
            {
                settings.WidgetAnimationSpeed = WidgetAnimationSpeedStandard;
                changed = true;
            }

            if (settings.WidgetAnimationSlideDirection != WidgetAnimationSlideDirectionNone)
            {
                settings.WidgetAnimationSlideDirection = WidgetAnimationSlideDirectionNone;
                changed = true;
            }

            if (settings.WidgetAnimationEasingIntensity != WidgetAnimationEasingNone)
            {
                settings.WidgetAnimationEasingIntensity = WidgetAnimationEasingNone;
                changed = true;
            }
        }
        else if (settings.WidgetAnimationSlideDirection != WidgetAnimationSlideDirectionNone)
        {
            settings.WidgetAnimationSlideDirection = WidgetAnimationSlideDirectionNone;
            changed = true;
        }

        string normalizedCollapseBehavior = NormalizeWidgetCollapseBehavior(settings.WidgetCollapseBehavior);
        if (normalizedCollapseBehavior == WidgetCollapseBehaviorExpanded)
        {
            normalizedCollapseBehavior = WidgetCollapseBehaviorClick;
        }
        if (!string.Equals(settings.WidgetCollapseBehavior, normalizedCollapseBehavior, StringComparison.Ordinal))
        {
            settings.WidgetCollapseBehavior = normalizedCollapseBehavior;
            changed = true;
        }

        string normalizedCompactContentMode = WidgetCompactContentModeSmart;
        if (!string.Equals(settings.WidgetCompactContentMode, normalizedCompactContentMode, StringComparison.Ordinal))
        {
            settings.WidgetCompactContentMode = normalizedCompactContentMode;
            changed = true;
        }

        if (settings.WidgetCompactHideSensitiveContent)
        {
            settings.WidgetCompactHideSensitiveContent = false;
            changed = true;
        }

        string normalizedCompactAnimation = NormalizeWidgetCompactAnimationEffect(settings.WidgetCompactAnimationEffect);
        if (!string.Equals(settings.WidgetCompactAnimationEffect, normalizedCompactAnimation, StringComparison.Ordinal))
        {
            settings.WidgetCompactAnimationEffect = normalizedCompactAnimation;
            changed = true;
        }

        int normalizedCompactDuration = NormalizeWidgetCompactAnimationDurationMs(settings.WidgetCompactAnimationDurationMs);
        if (settings.WidgetCompactAnimationDurationMs != normalizedCompactDuration)
        {
            settings.WidgetCompactAnimationDurationMs = normalizedCompactDuration;
            changed = true;
        }

        int normalizedCompactExpandDelay = NormalizeWidgetCompactExpandDelayMs(settings.WidgetCompactExpandDelayMs);
        if (settings.WidgetCompactExpandDelayMs != normalizedCompactExpandDelay)
        {
            settings.WidgetCompactExpandDelayMs = normalizedCompactExpandDelay;
            changed = true;
        }

        int normalizedCompactCollapseDelay = NormalizeWidgetCompactCollapseDelayMs(settings.WidgetCompactCollapseDelayMs);
        if (settings.WidgetCompactCollapseDelayMs != normalizedCompactCollapseDelay)
        {
            settings.WidgetCompactCollapseDelayMs = normalizedCompactCollapseDelay;
            changed = true;
        }

        string normalizedCompactMediaCorner = NormalizeWidgetCompactMediaCornerMode(settings.WidgetCompactMediaCornerMode);
        if (!string.Equals(settings.WidgetCompactMediaCornerMode, normalizedCompactMediaCorner, StringComparison.Ordinal))
        {
            settings.WidgetCompactMediaCornerMode = normalizedCompactMediaCorner;
            changed = true;
        }

        string normalizedTitleIconMode = NormalizeWidgetTitleIconModeSetting(settings.WidgetTitleIconMode);
        if (!string.Equals(settings.WidgetTitleIconMode, normalizedTitleIconMode, StringComparison.Ordinal))
        {
            settings.WidgetTitleIconMode = normalizedTitleIconMode;
            changed = true;
        }

        double normalizedIconSize = NormalizeIconSize(settings.IconSize);
        if (Math.Abs(settings.IconSize - normalizedIconSize) > 0.0001)
        {
            settings.IconSize = normalizedIconSize;
            changed = true;
        }

        double normalizedTextSize = NormalizeTextSize(settings.TextSize);
        if (Math.Abs(settings.TextSize - normalizedTextSize) > 0.0001)
        {
            settings.TextSize = normalizedTextSize;
            changed = true;
        }

        double legacyLayoutDensityScale = settings.LayoutDensityScale;
        if (!double.IsFinite(legacyLayoutDensityScale))
        {
            legacyLayoutDensityScale = DefaultLayoutDensityScale;
        }

        double normalizedLayoutDensityScale = Math.Clamp(legacyLayoutDensityScale, MinLayoutDensityScale, MaxLayoutDensityScale);
        if (Math.Abs(settings.LayoutDensityScale - normalizedLayoutDensityScale) > 0.0001)
        {
            settings.LayoutDensityScale = normalizedLayoutDensityScale;
            changed = true;
        }

        double normalizedHorizontalSpacingScale = NormalizeScale(
            settings.HorizontalSpacingScale,
            DefaultHorizontalSpacingScale,
            MinSpacingScale,
            MaxSpacingScale);
        double normalizedVerticalSpacingScale = NormalizeScale(
            settings.VerticalSpacingScale,
            DefaultVerticalSpacingScale,
            MinSpacingScale,
            MaxSpacingScale);
        double normalizedFileNameWidthScale = NormalizeScale(
            settings.FileNameWidthScale,
            DefaultFileNameWidthScale,
            MinSpacingScale,
            MaxSpacingScale);

        if (Math.Abs(settings.HorizontalSpacingScale - normalizedHorizontalSpacingScale) > 0.0001)
        {
            settings.HorizontalSpacingScale = normalizedHorizontalSpacingScale;
            changed = true;
        }

        if (Math.Abs(settings.VerticalSpacingScale - normalizedVerticalSpacingScale) > 0.0001)
        {
            settings.VerticalSpacingScale = normalizedVerticalSpacingScale;
            changed = true;
        }

        if (Math.Abs(settings.FileNameWidthScale - normalizedFileNameWidthScale) > 0.0001)
        {
            settings.FileNameWidthScale = normalizedFileNameWidthScale;
            changed = true;
        }

        string resolvedLayoutDensity = settings.LayoutDensity == LayoutDensityCustom
            ? LayoutDensityCustom
            : ResolveLayoutDensityPreset(settings);
        if (!string.Equals(settings.LayoutDensity, resolvedLayoutDensity, StringComparison.Ordinal))
        {
            settings.LayoutDensity = resolvedLayoutDensity;
            changed = true;
        }

        string normalizedMusicDisplayMode = NormalizeMusicDisplayMode(settings.MusicDisplayMode);
        if (!string.Equals(settings.MusicDisplayMode, normalizedMusicDisplayMode, StringComparison.Ordinal))
        {
            settings.MusicDisplayMode = normalizedMusicDisplayMode;
            changed = true;
        }

        double normalizedWidgetWidth = double.IsFinite(settings.DefaultWidgetWidth)
            ? Math.Clamp(settings.DefaultWidgetWidth, MinWidgetWidth, 1200)
            : DefaultWidgetWidth;
        if (Math.Abs(settings.DefaultWidgetWidth - normalizedWidgetWidth) > 0.0001)
        {
            settings.DefaultWidgetWidth = normalizedWidgetWidth;
            changed = true;
        }

        double normalizedWidgetHeight = double.IsFinite(settings.DefaultWidgetHeight)
            ? Math.Clamp(settings.DefaultWidgetHeight, MinWidgetHeight, 1200)
            : DefaultWidgetHeight;
        if (Math.Abs(settings.DefaultWidgetHeight - normalizedWidgetHeight) > 0.0001)
        {
            settings.DefaultWidgetHeight = normalizedWidgetHeight;
            changed = true;
        }

        return changed;
    }

    private static double NormalizeScale(double value, double defaultValue, double min, double max)
    {
        return double.IsFinite(value)
            ? Math.Clamp(value, min, max)
            : defaultValue;
    }

    public static double NormalizeIconSize(double value)
    {
        return double.IsFinite(value)
            ? Math.Clamp(value, MinIconSize, MaxIconSize)
            : DefaultIconSize;
    }

    public static double NormalizeTextSize(double value)
    {
        return double.IsFinite(value)
            ? Math.Clamp(value, MinTextSize, MaxTextSize)
            : DefaultTextSize;
    }

    public static string NormalizeMusicDisplayMode(string? mode)
    {
        return mode switch
        {
            MusicDisplayModeCover => MusicDisplayModeCover,
            MusicDisplayModeControls => MusicDisplayModeControls,
            MusicDisplayModeRecordVertical => MusicDisplayModeRecordVertical,
            MusicDisplayModeRecordHorizontal => MusicDisplayModeRecordHorizontal,
            _ => MusicDisplayModeAuto
        };
    }

    public static bool TryGetLayoutDensityPresetValues(
        string? preset,
        out LayoutDensityPresetValues values)
    {
        values = preset switch
        {
            LayoutDensityCompact => new LayoutDensityPresetValues(
                IconSize: 26,
                TextSize: 10.5,
                DensityScale: 0.20,
                HorizontalSpacingScale: 0.20,
                VerticalSpacingScale: 0.28,
                FileNameWidthScale: 0.30),
            LayoutDensityStandard => new LayoutDensityPresetValues(
                IconSize: DefaultIconSize,
                TextSize: DefaultTextSize,
                DensityScale: DefaultLayoutDensityScale,
                HorizontalSpacingScale: DefaultHorizontalSpacingScale,
                VerticalSpacingScale: DefaultVerticalSpacingScale,
                FileNameWidthScale: DefaultFileNameWidthScale),
            LayoutDensityRelaxed => new LayoutDensityPresetValues(
                IconSize: 36,
                TextSize: 13,
                DensityScale: 0.84,
                HorizontalSpacingScale: 0.68,
                VerticalSpacingScale: 0.82,
                FileNameWidthScale: 0.50),
            _ => default
        };

        return preset is LayoutDensityCompact or LayoutDensityStandard or LayoutDensityRelaxed;
    }

    public static void ApplyLayoutDensityPreset(AppSettings settings, string preset)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!TryGetLayoutDensityPresetValues(preset, out LayoutDensityPresetValues values))
        {
            return;
        }

        settings.IconSize = values.IconSize;
        settings.TextSize = values.TextSize;
        settings.LayoutDensityScale = values.DensityScale;
        settings.HorizontalSpacingScale = values.HorizontalSpacingScale;
        settings.VerticalSpacingScale = values.VerticalSpacingScale;
        settings.FileNameWidthScale = values.FileNameWidthScale;
        settings.LayoutDensity = preset;
    }

    public static string ResolveLayoutDensityPreset(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        foreach (string preset in new[] { LayoutDensityCompact, LayoutDensityStandard, LayoutDensityRelaxed })
        {
            TryGetLayoutDensityPresetValues(preset, out LayoutDensityPresetValues values);
            if (NearlyEqual(settings.IconSize, values.IconSize) &&
                NearlyEqual(settings.TextSize, values.TextSize) &&
                NearlyEqual(settings.LayoutDensityScale, values.DensityScale) &&
                NearlyEqual(settings.HorizontalSpacingScale, values.HorizontalSpacingScale) &&
                NearlyEqual(settings.VerticalSpacingScale, values.VerticalSpacingScale) &&
                NearlyEqual(settings.FileNameWidthScale, values.FileNameWidthScale))
            {
                return preset;
            }
        }

        return LayoutDensityCustom;
    }

    private static bool NearlyEqual(double left, double right) =>
        Math.Abs(left - right) <= 0.0001;

    public static string NormalizeWidgetCollapseBehavior(string? value)
    {
        return WidgetCollapseBehaviorNames.ToSettingValue(
            WidgetCollapseBehaviorNames.Normalize(value));
    }

    public static string NormalizeWidgetCompactContentMode(string? value)
    {
        if (string.Equals(value, WidgetCompactContentModeMinimal, StringComparison.OrdinalIgnoreCase))
        {
            return WidgetCompactContentModeMinimal;
        }

        return string.Equals(value, WidgetCompactContentModeSummary, StringComparison.OrdinalIgnoreCase)
            ? WidgetCompactContentModeSummary
            : WidgetCompactContentModeSmart;
    }

    public static string NormalizeWidgetCompactAnimationEffect(string? value)
    {
        if (string.Equals(value, WidgetCompactAnimationSlow, StringComparison.OrdinalIgnoreCase))
        {
            return WidgetCompactAnimationSlow;
        }

        if (string.Equals(value, WidgetCompactAnimationSnappy, StringComparison.OrdinalIgnoreCase))
        {
            return WidgetCompactAnimationSnappy;
        }

        if (string.Equals(value, WidgetCompactAnimationCustom, StringComparison.OrdinalIgnoreCase))
        {
            return WidgetCompactAnimationCustom;
        }

        return string.Equals(value, WidgetCompactAnimationNone, StringComparison.OrdinalIgnoreCase)
            ? WidgetCompactAnimationNone
            : WidgetCompactAnimationSmooth;
    }

    public static int NormalizeWidgetCompactAnimationDurationMs(int value) =>
        Math.Clamp(value, MinWidgetCompactAnimationDurationMs, MaxWidgetCompactAnimationDurationMs);

    public static int NormalizeWidgetCompactExpandDelayMs(int value) =>
        Math.Clamp(value, MinWidgetCompactExpandDelayMs, MaxWidgetCompactExpandDelayMs);

    public static int NormalizeWidgetCompactCollapseDelayMs(int value) =>
        Math.Clamp(value, MinWidgetCompactCollapseDelayMs, MaxWidgetCompactCollapseDelayMs);

    public static string NormalizeWidgetCompactHoverResponse(string? value) => value switch
    {
        WidgetCompactHoverResponseSensitive => WidgetCompactHoverResponseSensitive,
        WidgetCompactHoverResponsePreventAccidental => WidgetCompactHoverResponsePreventAccidental,
        WidgetCompactHoverResponseCustom => WidgetCompactHoverResponseCustom,
        _ => WidgetCompactHoverResponseBalanced
    };

    public static string ResolveWidgetCompactHoverResponse(int expandDelayMs, int collapseDelayMs)
    {
        int expand = NormalizeWidgetCompactExpandDelayMs(expandDelayMs);
        int collapse = NormalizeWidgetCompactCollapseDelayMs(collapseDelayMs);
        return (expand, collapse) switch
        {
            (SensitiveWidgetCompactExpandDelayMs, SensitiveWidgetCompactCollapseDelayMs) =>
                WidgetCompactHoverResponseSensitive,
            (DefaultWidgetCompactExpandDelayMs, DefaultWidgetCompactCollapseDelayMs) =>
                WidgetCompactHoverResponseBalanced,
            (PreventAccidentalWidgetCompactExpandDelayMs, PreventAccidentalWidgetCompactCollapseDelayMs) =>
                WidgetCompactHoverResponsePreventAccidental,
            _ => WidgetCompactHoverResponseCustom
        };
    }

    public static string NormalizeWidgetCompactMediaCornerMode(string? value)
    {
        if (string.Equals(value, WidgetCompactMediaCornerSquare, StringComparison.OrdinalIgnoreCase))
        {
            return WidgetCompactMediaCornerSquare;
        }

        if (string.Equals(value, WidgetCompactMediaCornerSmall, StringComparison.OrdinalIgnoreCase))
        {
            return WidgetCompactMediaCornerSmall;
        }

        return string.Equals(value, WidgetCompactMediaCornerRound, StringComparison.OrdinalIgnoreCase)
            ? WidgetCompactMediaCornerRound
            : WidgetCompactMediaCornerFollowWidget;
    }

    public static string NormalizeWidgetTitleIconModeSetting(string? value)
    {
        return WidgetTitleIconModeNames.NormalizeSettingValue(value);
    }

    private static bool NormalizeAppearanceSettings(AppSettings settings)
    {
        bool changed = false;

        if (settings.Theme is not ("System" or "Light" or "Dark"))
        {
            settings.Theme = "System";
            changed = true;
        }

        if (settings.AccentColorMode is not ("System" or "Custom"))
        {
            settings.AccentColorMode = "System";
            changed = true;
        }

        if (!AccentColorHelper.TryParseHex(settings.CustomAccentColor, out _))
        {
            settings.CustomAccentColor = AccentColorHelper.DefaultAccentColorHex;
            changed = true;
        }

        return changed;
    }

    private static bool NormalizeWidgetContentSettings(AppSettings settings)
    {
        bool changed = false;

        foreach (var widget in settings.Widgets)
        {
            if (!WidgetRegistry.Default.IsKnown(widget.WidgetKind))
            {
                widget.WidgetKind = WidgetKind.File;
                changed = true;
            }

            widget.Metadata ??= [];

            if (widget.CompactWidth is { } compactWidth)
            {
                double normalizedCompactWidth = WidgetCompactBoundsCalculator.ClampLogicalWidth(compactWidth);
                if (Math.Abs(compactWidth - normalizedCompactWidth) > 0.0001)
                {
                    widget.CompactWidth = normalizedCompactWidth;
                    changed = true;
                }
            }

            if (widget.Metadata.Remove(WidgetChromeModeNames.MetadataKey))
            {
                changed = true;
            }

            if (widget.Metadata.TryGetValue(WidgetCollapseBehaviorNames.MetadataKey, out string? collapseBehaviorValue))
            {
                WidgetCollapseBehavior normalizedBehavior = WidgetCollapseBehaviorNames.Normalize(
                    collapseBehaviorValue,
                    WidgetCollapseBehavior.System,
                    allowSystem: true);
                if (normalizedBehavior == WidgetCollapseBehavior.System)
                {
                    widget.Metadata.Remove(WidgetCollapseBehaviorNames.MetadataKey);
                    changed = true;
                }
                else
                {
                    string normalizedValue = WidgetCollapseBehaviorNames.ToSettingValue(normalizedBehavior);
                    if (!string.Equals(collapseBehaviorValue, normalizedValue, StringComparison.Ordinal))
                    {
                        widget.Metadata[WidgetCollapseBehaviorNames.MetadataKey] = normalizedValue;
                        changed = true;
                    }
                }
            }

            if (WidgetFileStackSettings.NormalizeOverrides(widget))
            {
                changed = true;
            }

            if (widget.IsDisabled)
            {
                widget.IsDisabled = false;
                changed = true;
            }
        }

        return changed;
    }

    internal static bool NormalizeFeatureWidgetSettings(AppSettings settings)
    {
        return FeatureWidgetSettings.Normalize(settings);
    }

    private static bool NormalizeOrganizerSettings(AppSettings settings)
    {
        bool changed = false;

        string normalizedFileStackGroupBy = NormalizeFileStackGroupBy(settings.FileStackGroupBy);
        if (!string.Equals(settings.FileStackGroupBy, normalizedFileStackGroupBy, StringComparison.Ordinal))
        {
            settings.FileStackGroupBy = normalizedFileStackGroupBy;
            changed = true;
        }

        int normalizedFileStackThreshold = NormalizeFileStackThreshold(settings.FileStackThreshold);
        if (settings.FileStackThreshold != normalizedFileStackThreshold)
        {
            settings.FileStackThreshold = normalizedFileStackThreshold;
            changed = true;
        }

        string normalizedFileStackOrderBy = NormalizeFileStackOrderBy(settings.FileStackOrderBy);
        if (!string.Equals(settings.FileStackOrderBy, normalizedFileStackOrderBy, StringComparison.Ordinal))
        {
            settings.FileStackOrderBy = normalizedFileStackOrderBy;
            changed = true;
        }

        string normalizedUnmatchedBehavior = NormalizeFileStackUnmatchedBehavior(
            settings.FileStackUnmatchedBehavior);
        if (!string.Equals(
                settings.FileStackUnmatchedBehavior,
                normalizedUnmatchedBehavior,
                StringComparison.Ordinal))
        {
            settings.FileStackUnmatchedBehavior = normalizedUnmatchedBehavior;
            changed = true;
        }

        settings.FileStackCustomRules ??= [];
        var normalizedRules = new List<FileStackCustomRule>();
        var usedRuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in settings.FileStackCustomRules.Where(rule => rule is not null).Take(32))
        {
            string id = rule.Id?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id) || !usedRuleIds.Add(id))
            {
                do
                {
                    id = Guid.NewGuid().ToString("N");
                }
                while (!usedRuleIds.Add(id));
            }

            string name = (rule.Name ?? string.Empty).Trim();
            if (name.Length > 80)
            {
                name = name[..80];
            }

            var extensions = NormalizeFileStackExtensions(rule.Extensions).Take(64).ToList();
            normalizedRules.Add(new FileStackCustomRule
            {
                Id = id,
                Name = name,
                Extensions = extensions
            });
        }

        if (!FileStackCustomRulesEqual(settings.FileStackCustomRules, normalizedRules))
        {
            settings.FileStackCustomRules = normalizedRules;
            changed = true;
        }

        if (!string.Equals(settings.ManagedDropAction, ManagedDropActionMove, StringComparison.Ordinal) &&
            !string.Equals(settings.ManagedDropAction, ManagedDropActionCopy, StringComparison.Ordinal))
        {
            settings.ManagedDropAction = ManagedDropActionMove;
            changed = true;
        }

        settings.RecentOrganizationHistory ??= [];
        int originalHistoryCount = settings.RecentOrganizationHistory.Count;
        settings.RecentOrganizationHistory = settings.RecentOrganizationHistory
            .Where(entry => entry is not null)
            .OrderByDescending(entry => entry.TimestampUtc)
            .Take(MaxRecentOrganizationHistoryCount)
            .ToList();
        if (settings.RecentOrganizationHistory.Count != originalHistoryCount)
        {
            changed = true;
        }

        foreach (var entry in settings.RecentOrganizationHistory)
        {
            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                entry.Id = Guid.NewGuid().ToString();
                changed = true;
            }

            entry.WidgetId ??= string.Empty;
            entry.WidgetName ??= string.Empty;
            entry.ActionType = string.IsNullOrWhiteSpace(entry.ActionType)
                ? OrganizationActionType.ManagedDrop
                : entry.ActionType;
            entry.TransferMode = entry.TransferMode is "Move" or "Copy"
                ? entry.TransferMode
                : ManagedDropActionMove;
            entry.Items ??= [];
        }

        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        foreach (var widget in settings.Widgets)
        {
            if (!widget.FollowsDefaultStoragePath || widget.WidgetKind != WidgetKind.File)
            {
                continue;
            }

            // 桌面归属收纳盒：不再拥有托管根下的私有文件夹。
            if (!string.IsNullOrWhiteSpace(widget.ManagedFolderName))
            {
                widget.ManagedFolderName = null;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(desktopPath) &&
                !string.Equals(widget.MappedFolderPath, desktopPath, StringComparison.OrdinalIgnoreCase))
            {
                widget.MappedFolderPath = desktopPath;
                changed = true;
            }
        }

        return changed;
    }

    public static string NormalizeFileStackGroupBy(string? groupBy)
    {
        if (string.Equals(groupBy, FileStackGroupByDateModified, StringComparison.OrdinalIgnoreCase))
        {
            return FileStackGroupByDateModified;
        }

        return string.Equals(groupBy, FileStackGroupByCustom, StringComparison.OrdinalIgnoreCase)
            ? FileStackGroupByCustom
            : FileStackGroupByKind;
    }

    public static int NormalizeFileStackThreshold(int threshold) => threshold switch
    {
        2 or 3 or 5 => threshold,
        _ => DefaultFileStackThreshold
    };

    public static string NormalizeFileStackOrderBy(string? orderBy)
    {
        if (string.Equals(orderBy, FileStackOrderByName, StringComparison.OrdinalIgnoreCase))
        {
            return FileStackOrderByName;
        }

        if (string.Equals(orderBy, FileStackOrderByDateAdded, StringComparison.OrdinalIgnoreCase))
        {
            return FileStackOrderByDateAdded;
        }

        return string.Equals(orderBy, FileStackOrderByDateModified, StringComparison.OrdinalIgnoreCase)
            ? FileStackOrderByDateModified
            : FileStackOrderByWidget;
    }

    public static string NormalizeFileStackUnmatchedBehavior(string? behavior) =>
        string.Equals(behavior, FileStackUnmatchedOther, StringComparison.OrdinalIgnoreCase)
            ? FileStackUnmatchedOther
            : FileStackUnmatchedKeepLoose;

    public static IReadOnlyList<string> NormalizeFileStackExtensions(
        IEnumerable<string>? extensions)
    {
        if (extensions is null)
        {
            return [];
        }

        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string? value in extensions)
        {
            string extension = (value ?? string.Empty).Trim();
            if (extension.StartsWith("*.", StringComparison.Ordinal))
            {
                extension = extension[1..];
            }
            else if (extension.StartsWith('*'))
            {
                extension = extension[1..];
            }

            if (extension.Length == 0)
            {
                continue;
            }

            if (!extension.StartsWith('.'))
            {
                extension = $".{extension}";
            }

            extension = extension.ToLowerInvariant();
            if (extension.Length > 24 ||
                extension.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                extension.Contains(Path.DirectorySeparatorChar) ||
                extension.Contains(Path.AltDirectorySeparatorChar) ||
                !seen.Add(extension))
            {
                continue;
            }

            normalized.Add(extension);
        }

        return normalized;
    }

    private static bool FileStackCustomRulesEqual(
        IReadOnlyList<FileStackCustomRule> left,
        IReadOnlyList<FileStackCustomRule> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Count; index++)
        {
            FileStackCustomRule leftRule = left[index];
            FileStackCustomRule rightRule = right[index];
            if (!string.Equals(leftRule.Id, rightRule.Id, StringComparison.Ordinal) ||
                !string.Equals(leftRule.Name, rightRule.Name, StringComparison.Ordinal) ||
                !leftRule.Extensions.SequenceEqual(
                    rightRule.Extensions,
                    StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool NormalizeHotkeySettings(AppSettings settings)
    {
        bool changed = false;
        int normalizedModifiers = (int)((Models.HotkeyModifierKeys)settings.GlobalHotkeyModifiers &
            (Models.HotkeyModifierKeys.Alt | Models.HotkeyModifierKeys.Control | Models.HotkeyModifierKeys.Shift));

        if (settings.GlobalHotkeyModifiers != normalizedModifiers)
        {
            settings.GlobalHotkeyModifiers = normalizedModifiers;
            changed = true;
        }

        var gesture = GlobalHotkeyService.NormalizeGesture(settings.GlobalHotkeyModifiers, settings.GlobalHotkeyKey);
        if (!GlobalHotkeyService.IsValidGesture(gesture))
        {
            settings.GlobalHotkeyModifiers = DefaultGlobalHotkeyModifiers;
            settings.GlobalHotkeyKey = DefaultGlobalHotkeyKey;
            changed = true;
        }

        return changed;
    }

    public static string NormalizeWidgetTabStyle(string? style)
    {
        return style == WidgetTabStylePivot
            ? WidgetTabStylePivot
            : WidgetTabStyleButton;
    }

    private static bool NormalizeDeletionSettings(AppSettings settings)
    {
        int beforeIds = settings.DeletedWidgetIds.Count;
        settings.DeletedWidgetIds = settings.DeletedWidgetIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        bool changed = settings.DeletedWidgetIds.Count != beforeIds;

        int removed = settings.Widgets.RemoveAll(widget => settings.DeletedWidgetIds.Contains(widget.Id));
        if (removed > 0)
        {
            changed = true;
        }

        int staleRemoved = settings.Widgets.RemoveAll(widget => IsStaleHiddenWidget(settings, widget));
        if (staleRemoved > 0)
        {
            changed = true;
        }

        return changed;
    }

    private static bool IsStaleHiddenWidget(AppSettings settings, WidgetConfig widget)
    {
        if (widget.WidgetKind != WidgetKind.File ||
            widget.IsVisible ||
            widget.IsDisabled ||
            !string.IsNullOrEmpty(widget.MappedFolderPath))
        {
            return false;
        }

        bool hasGenericName =
            string.Equals(widget.Name, "New Widget", StringComparison.Ordinal) ||
            string.Equals(widget.Name, "BentoDesk", StringComparison.Ordinal) ||
            string.Equals(widget.Name, "\u65B0\u5EFA\u7EC4\u4EF6", StringComparison.Ordinal) ||
            string.Equals(widget.Name, "\u65B0\u5EFA\u5C0F\u7EC4\u4EF6", StringComparison.Ordinal);

        if (!hasGenericName)
        {
            return false;
        }

        return Math.Abs(widget.X - 100) < 0.01 &&
               Math.Abs(widget.Y - 100) < 0.01 &&
               Math.Abs(widget.Width - settings.DefaultWidgetWidth) < 0.01 &&
               Math.Abs(widget.Height - settings.DefaultWidgetHeight) < 0.01;
    }
}
