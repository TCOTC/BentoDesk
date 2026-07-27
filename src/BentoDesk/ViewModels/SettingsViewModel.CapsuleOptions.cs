using CommunityToolkit.Mvvm.Input;
using BentoDesk.Models;
using BentoDesk.Services;
using Microsoft.UI.Xaml;

namespace BentoDesk.ViewModels;

public partial class SettingsViewModel
{
    private string _selectedWidgetCompactAnimationEffect = SettingsService.WidgetCompactAnimationSmooth;
    private string _selectedWidgetCompactMediaCornerMode = SettingsService.WidgetCompactMediaCornerFollowWidget;
    private double _widgetCompactAnimationDurationMs = SettingsService.DefaultWidgetCompactAnimationDurationMs;
    private double _widgetCompactExpandDelayMs = SettingsService.DefaultWidgetCompactExpandDelayMs;
    private double _widgetCompactCollapseDelayMs = SettingsService.DefaultWidgetCompactCollapseDelayMs;
    private string _selectedWidgetCompactHoverResponse = SettingsService.WidgetCompactHoverResponseBalanced;
    private bool _isApplyingWidgetCompactHoverResponse;
    private string[]? _cachedWidgetCompactAnimationEffectDisplayNames;
    private string[]? _cachedWidgetCompactHoverResponseDisplayNames;
    private string[]? _cachedWidgetCompactMediaCornerDisplayNames;

    public bool IsSmartWidgetCollapseBehavior =>
        SelectedWidgetCollapseBehavior == SettingsService.WidgetCollapseBehaviorSmart;

    public bool IsSmartWidgetCollapseBehaviorSelected =>
        SelectedWidgetCollapseBehavior == SettingsService.WidgetCollapseBehaviorSmart;

    public Visibility CapsuleHoverResponseEntryVisibility =>
        IsSmartWidgetCollapseBehaviorSelected ? Visibility.Visible : Visibility.Collapsed;

    public string[] AvailableWidgetCompactAnimationEffects { get; } =
    [
        SettingsService.WidgetCompactAnimationSnappy,
        SettingsService.WidgetCompactAnimationSmooth,
        SettingsService.WidgetCompactAnimationSlow,
        SettingsService.WidgetCompactAnimationNone,
        SettingsService.WidgetCompactAnimationCustom
    ];

    public string[] AvailableWidgetCompactAnimationEffectDisplayNames =>
        _cachedWidgetCompactAnimationEffectDisplayNames ??=
            AvailableWidgetCompactAnimationEffects.Select(GetWidgetCompactAnimationEffectDisplayName).ToArray();

    public string SelectedWidgetCompactAnimationEffect
    {
        get => _selectedWidgetCompactAnimationEffect;
        set
        {
            string normalized = SettingsService.NormalizeWidgetCompactAnimationEffect(value);
            if (!SetProperty(ref _selectedWidgetCompactAnimationEffect, normalized))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedWidgetCompactAnimationEffectText));
            OnPropertyChanged(nameof(IsWidgetCompactAnimationEnabled));
            OnPropertyChanged(nameof(IsWidgetCompactAnimationCustom));
            OnPropertyChanged(nameof(WidgetCompactAnimationCustomVisibility));
            OnPropertyChanged(nameof(CanOpenWidgetCompactAnimationDetails));
            if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
            {
                return;
            }

            _settingsService.Settings.WidgetCompactAnimationEffect = normalized;
            int? presetDuration = normalized switch
            {
                SettingsService.WidgetCompactAnimationSmooth =>
                    SettingsService.DefaultWidgetCompactAnimationDurationMs,
                SettingsService.WidgetCompactAnimationSlow =>
                    SettingsService.SlowWidgetCompactAnimationDurationMs,
                SettingsService.WidgetCompactAnimationSnappy =>
                    SettingsService.SnappyWidgetCompactAnimationDurationMs,
                _ => null
            };
            if (presetDuration is { } duration &&
                SetProperty(
                    ref _widgetCompactAnimationDurationMs,
                    duration,
                    nameof(WidgetCompactAnimationDurationMs)))
            {
                _settingsService.Settings.WidgetCompactAnimationDurationMs = duration;
                OnPropertyChanged(nameof(WidgetCompactAnimationDurationText));
            }
            _settingsService.SaveDebounced();
        }
    }

    public string SelectedWidgetCompactAnimationEffectText =>
        GetWidgetCompactAnimationEffectDisplayName(SelectedWidgetCompactAnimationEffect);


    public bool IsWidgetCompactAnimationEnabled =>
        SelectedWidgetCompactAnimationEffect != SettingsService.WidgetCompactAnimationNone;

    public bool IsWidgetCompactAnimationCustom =>
        SelectedWidgetCompactAnimationEffect == SettingsService.WidgetCompactAnimationCustom;

    public Visibility WidgetCompactAnimationCustomVisibility =>
        IsWidgetCompactAnimationCustom ? Visibility.Visible : Visibility.Collapsed;

    public bool CanOpenWidgetCompactAnimationDetails =>
        IsWidgetCompactAnimationCustom;

    public double WidgetCompactAnimationDurationMs
    {
        get => _widgetCompactAnimationDurationMs;
        set
        {
            int normalized = SettingsService.NormalizeWidgetCompactAnimationDurationMs((int)Math.Round(value));
            if (!SetProperty(ref _widgetCompactAnimationDurationMs, normalized))
            {
                return;
            }

            OnPropertyChanged(nameof(WidgetCompactAnimationDurationText));
            if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
            {
                return;
            }

            if (SelectedWidgetCompactAnimationEffect is not
                (SettingsService.WidgetCompactAnimationCustom or
                 SettingsService.WidgetCompactAnimationNone))
            {
                _selectedWidgetCompactAnimationEffect = SettingsService.WidgetCompactAnimationCustom;
                _settingsService.Settings.WidgetCompactAnimationEffect =
                    SettingsService.WidgetCompactAnimationCustom;
                OnPropertyChanged(nameof(SelectedWidgetCompactAnimationEffect));
                OnPropertyChanged(nameof(SelectedWidgetCompactAnimationEffectText));
                OnPropertyChanged(nameof(IsWidgetCompactAnimationEnabled));
                OnPropertyChanged(nameof(IsWidgetCompactAnimationCustom));
                OnPropertyChanged(nameof(WidgetCompactAnimationCustomVisibility));
                OnPropertyChanged(nameof(CanOpenWidgetCompactAnimationDetails));
            }

            _settingsService.Settings.WidgetCompactAnimationDurationMs = normalized;
            _settingsService.SaveDebounced();
        }
    }

    public string WidgetCompactAnimationDurationText => $"{Math.Round(WidgetCompactAnimationDurationMs):0} ms";

    public string[] AvailableWidgetCompactHoverResponses { get; } =
    [
        SettingsService.WidgetCompactHoverResponseSensitive,
        SettingsService.WidgetCompactHoverResponseBalanced,
        SettingsService.WidgetCompactHoverResponsePreventAccidental,
        SettingsService.WidgetCompactHoverResponseCustom
    ];

    public string[] AvailableWidgetCompactHoverResponseDisplayNames =>
        _cachedWidgetCompactHoverResponseDisplayNames ??=
            AvailableWidgetCompactHoverResponses
                .Select(GetWidgetCompactHoverResponseDisplayName)
                .ToArray();

    public string SelectedWidgetCompactHoverResponse
    {
        get => _selectedWidgetCompactHoverResponse;
        set
        {
            string normalized = SettingsService.NormalizeWidgetCompactHoverResponse(value);
            if (!SetProperty(ref _selectedWidgetCompactHoverResponse, normalized))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedWidgetCompactHoverResponseText));
            OnPropertyChanged(nameof(IsWidgetCompactHoverResponseCustom));
            OnPropertyChanged(nameof(WidgetCompactHoverResponseCustomVisibility));
            OnPropertyChanged(nameof(CanOpenWidgetCompactHoverResponseDetails));
            if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
            {
                return;
            }

            (int Expand, int Collapse)? delays = normalized switch
            {
                SettingsService.WidgetCompactHoverResponseSensitive =>
                    (SettingsService.SensitiveWidgetCompactExpandDelayMs,
                     SettingsService.SensitiveWidgetCompactCollapseDelayMs),
                SettingsService.WidgetCompactHoverResponseBalanced =>
                    (SettingsService.DefaultWidgetCompactExpandDelayMs,
                     SettingsService.DefaultWidgetCompactCollapseDelayMs),
                SettingsService.WidgetCompactHoverResponsePreventAccidental =>
                    (SettingsService.PreventAccidentalWidgetCompactExpandDelayMs,
                     SettingsService.PreventAccidentalWidgetCompactCollapseDelayMs),
                _ => null
            };
            if (delays is not { } preset)
            {
                return;
            }

            _isApplyingWidgetCompactHoverResponse = true;
            try
            {
                WidgetCompactExpandDelayMs = preset.Expand;
                WidgetCompactCollapseDelayMs = preset.Collapse;
            }
            finally
            {
                _isApplyingWidgetCompactHoverResponse = false;
            }
        }
    }

    public string SelectedWidgetCompactHoverResponseText =>
        GetWidgetCompactHoverResponseDisplayName(SelectedWidgetCompactHoverResponse);

    public bool IsWidgetCompactHoverResponseCustom =>
        SelectedWidgetCompactHoverResponse == SettingsService.WidgetCompactHoverResponseCustom;

    public Visibility WidgetCompactHoverResponseCustomVisibility =>
        IsWidgetCompactHoverResponseCustom ? Visibility.Visible : Visibility.Collapsed;

    public bool CanOpenWidgetCompactHoverResponseDetails =>
        IsWidgetCompactHoverResponseCustom;

    public double WidgetCompactExpandDelayMs
    {
        get => _widgetCompactExpandDelayMs;
        set
        {
            int normalized = SettingsService.NormalizeWidgetCompactExpandDelayMs((int)Math.Round(value));
            if (!SetProperty(ref _widgetCompactExpandDelayMs, normalized))
            {
                return;
            }

            OnPropertyChanged(nameof(WidgetCompactExpandDelayText));
            if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
            {
                return;
            }

            MarkWidgetCompactHoverResponseCustom();

            _settingsService.Settings.WidgetCompactExpandDelayMs = normalized;
            _settingsService.SaveDebounced();
        }
    }

    public string WidgetCompactExpandDelayText => $"{Math.Round(WidgetCompactExpandDelayMs):0} ms";

    public double WidgetCompactCollapseDelayMs
    {
        get => _widgetCompactCollapseDelayMs;
        set
        {
            int normalized = SettingsService.NormalizeWidgetCompactCollapseDelayMs((int)Math.Round(value));
            if (!SetProperty(ref _widgetCompactCollapseDelayMs, normalized))
            {
                return;
            }

            OnPropertyChanged(nameof(WidgetCompactCollapseDelayText));
            if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
            {
                return;
            }

            MarkWidgetCompactHoverResponseCustom();

            _settingsService.Settings.WidgetCompactCollapseDelayMs = normalized;
            _settingsService.SaveDebounced();
        }
    }

    public string WidgetCompactCollapseDelayText => $"{Math.Round(WidgetCompactCollapseDelayMs):0} ms";

    private void MarkWidgetCompactHoverResponseCustom()
    {
        if (_isApplyingWidgetCompactHoverResponse || IsWidgetCompactHoverResponseCustom)
        {
            return;
        }

        _selectedWidgetCompactHoverResponse = SettingsService.WidgetCompactHoverResponseCustom;
        OnPropertyChanged(nameof(SelectedWidgetCompactHoverResponse));
        OnPropertyChanged(nameof(SelectedWidgetCompactHoverResponseText));
        OnPropertyChanged(nameof(IsWidgetCompactHoverResponseCustom));
        OnPropertyChanged(nameof(WidgetCompactHoverResponseCustomVisibility));
        OnPropertyChanged(nameof(CanOpenWidgetCompactHoverResponseDetails));
    }

    public string[] AvailableWidgetCompactMediaCornerModes { get; } =
    [
        SettingsService.WidgetCompactMediaCornerFollowWidget,
        SettingsService.WidgetCompactMediaCornerSquare,
        SettingsService.WidgetCompactMediaCornerSmall,
        SettingsService.WidgetCompactMediaCornerRound
    ];

    public string[] AvailableWidgetCompactMediaCornerDisplayNames =>
        _cachedWidgetCompactMediaCornerDisplayNames ??=
            AvailableWidgetCompactMediaCornerModes.Select(GetWidgetCompactMediaCornerDisplayName).ToArray();

    public string SelectedWidgetCompactMediaCornerMode
    {
        get => _selectedWidgetCompactMediaCornerMode;
        set
        {
            string normalized = SettingsService.NormalizeWidgetCompactMediaCornerMode(value);
            if (!SetProperty(ref _selectedWidgetCompactMediaCornerMode, normalized))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedWidgetCompactMediaCornerText));
            if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
            {
                return;
            }

            _settingsService.Settings.WidgetCompactMediaCornerMode = normalized;
            _settingsService.SaveDebounced();
        }
    }

    public string SelectedWidgetCompactMediaCornerText =>
        GetWidgetCompactMediaCornerDisplayName(SelectedWidgetCompactMediaCornerMode);


    public int CapsuleCustomRuleCount => _settingsService.Settings.Widgets.Count(widget =>
        widget.Metadata?.ContainsKey(WidgetCollapseBehaviorNames.MetadataKey) == true);

    public int CapsuleCustomWidthCount =>
        _settingsService.Settings.Widgets.Count(widget => widget.CompactWidth is not null);

    public int CapsuleSavedPlacementCount =>
        _settingsService.Settings.Widgets.Count(widget => widget.CompactPlacement is not null);

    public bool HasCapsuleBehaviorOverrides => CapsuleCustomRuleCount > 0;

    public bool HasCapsuleGeometryOverrides =>
        CapsuleCustomWidthCount > 0 || CapsuleSavedPlacementCount > 0;

    public int CapsuleOverrideWidgetCount => _settingsService.Settings.Widgets.Count(HasCapsuleOverride);

    public bool HasCapsuleOverrides => CapsuleOverrideWidgetCount > 0;

    public Visibility CapsuleOverridesEntryVisibility =>
        HasCapsuleOverrides ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CapsuleOverridesListVisibility =>
        HasCapsuleOverrides ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CapsuleOverridesEmptyVisibility =>
        HasCapsuleOverrides ? Visibility.Collapsed : Visibility.Visible;

    public IReadOnlyList<CapsuleOverrideSettingsItem> CapsuleOverrideItems =>
        _settingsService.Settings.Widgets
            .Where(HasCapsuleOverride)
            .Select(CreateCapsuleOverrideSettingsItem)
            .ToArray();

    public string CapsuleOverrideSummaryText => _localizationService.Format(
        "Settings.Capsule.Overrides.Summary",
        CapsuleOverrideWidgetCount);

    public string CapsuleBehaviorOverrideSummaryText => _localizationService.Format(
        "Settings.Capsule.Overrides.Behavior.Summary",
        CapsuleCustomRuleCount);

    public string CapsuleGeometryOverrideSummaryText => _localizationService.Format(
        "Settings.Capsule.Overrides.Geometry.Summary",
        CapsuleCustomWidthCount,
        CapsuleSavedPlacementCount);

    [RelayCommand]
    private void ResetCapsuleBehaviorOverrides()
    {
        int changed = 0;
        foreach (var widget in _settingsService.Settings.Widgets)
        {
            if (widget.Metadata?.Remove(WidgetCollapseBehaviorNames.MetadataKey) == true)
            {
                changed++;
            }
        }

        if (changed > 0)
        {
            _settingsService.SaveDebounced();
            NotifyCapsuleOverridePropertiesChanged();
        }
    }

    [RelayCommand]
    private void ResetCapsuleGeometryOverrides()
    {
        int changed = 0;
        foreach (var widget in _settingsService.Settings.Widgets)
        {
            if (widget.CompactWidth is not null)
            {
                widget.CompactWidth = null;
                changed++;
            }

            if (widget.CompactPlacement is not null)
            {
                widget.CompactPlacement = null;
                changed++;
            }
        }

        if (changed > 0)
        {
            _settingsService.SaveDebounced();
            NotifyCapsuleOverridePropertiesChanged();
        }
    }

    public void ResetCapsuleOverridesForWidget(string widgetId)
    {
        var widget = _settingsService.Settings.Widgets.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, widgetId, StringComparison.Ordinal));
        if (widget is null || !ClearCapsuleOverrides(widget))
        {
            return;
        }

        _settingsService.SaveDebounced();
        NotifyCapsuleOverridePropertiesChanged();
    }

    [RelayCommand]
    private void ResetAllCapsuleOverrides()
    {
        bool changed = false;
        foreach (var widget in _settingsService.Settings.Widgets)
        {
            changed |= ClearCapsuleOverrides(widget);
        }

        if (!changed)
        {
            return;
        }

        _settingsService.SaveDebounced();
        NotifyCapsuleOverridePropertiesChanged();
    }

    private static bool HasCapsuleOverride(WidgetConfig widget) =>
        widget.Metadata?.ContainsKey(WidgetCollapseBehaviorNames.MetadataKey) == true ||
        widget.CompactWidth is not null ||
        widget.CompactPlacement is not null;

    private static bool ClearCapsuleOverrides(WidgetConfig widget)
    {
        bool changed = widget.Metadata?.Remove(WidgetCollapseBehaviorNames.MetadataKey) == true;
        if (widget.CompactWidth is not null)
        {
            widget.CompactWidth = null;
            changed = true;
        }

        if (widget.CompactPlacement is not null)
        {
            widget.CompactPlacement = null;
            changed = true;
        }

        return changed;
    }

    private CapsuleOverrideSettingsItem CreateCapsuleOverrideSettingsItem(WidgetConfig widget)
    {
        var details = new List<string>(3);
        if (widget.Metadata is not null &&
            widget.Metadata.TryGetValue(WidgetCollapseBehaviorNames.MetadataKey, out string? behavior))
        {
            details.Add(_localizationService.Format(
                "Settings.Capsule.Overrides.Item.Behavior",
                GetWidgetCollapseBehaviorDisplayName(behavior)));
        }

        if (widget.CompactWidth is { } width)
        {
            details.Add(_localizationService.Format(
                "Settings.Capsule.Overrides.Item.Width",
                Math.Round(width)));
        }

        if (widget.CompactPlacement is not null)
        {
            details.Add(_localizationService.T("Settings.Capsule.Overrides.Item.Position"));
        }

        string displayName = string.IsNullOrWhiteSpace(widget.Name)
            ? GetWidgetKindDisplayName(widget.WidgetKind)
            : widget.Name.Trim();
        return new CapsuleOverrideSettingsItem(
            widget.Id,
            displayName,
            string.Join(" · ", details),
            GetWidgetKindGlyph(widget.WidgetKind));
    }

    private string GetWidgetKindDisplayName(WidgetKind kind) => kind switch
    {
        WidgetKind.Music => _localizationService.T("WidgetTitleIcon.Label.Music"),
        _ => _localizationService.T("WidgetTitleIcon.Label.Default")
    };

    private static string GetWidgetKindGlyph(WidgetKind kind) => kind switch
    {
        WidgetKind.Music => "\uE8D6",
        _ => "\uE8A5"
    };

    private void NotifyCapsuleOverridePropertiesChanged()
    {
        OnPropertyChanged(nameof(CapsuleCustomRuleCount));
        OnPropertyChanged(nameof(CapsuleCustomWidthCount));
        OnPropertyChanged(nameof(CapsuleSavedPlacementCount));
        OnPropertyChanged(nameof(HasCapsuleBehaviorOverrides));
        OnPropertyChanged(nameof(HasCapsuleGeometryOverrides));
        OnPropertyChanged(nameof(CapsuleOverrideWidgetCount));
        OnPropertyChanged(nameof(HasCapsuleOverrides));
        OnPropertyChanged(nameof(CapsuleOverridesEntryVisibility));
        OnPropertyChanged(nameof(CapsuleOverridesListVisibility));
        OnPropertyChanged(nameof(CapsuleOverridesEmptyVisibility));
        OnPropertyChanged(nameof(CapsuleOverrideItems));
        OnPropertyChanged(nameof(CapsuleOverrideSummaryText));
        OnPropertyChanged(nameof(CapsuleBehaviorOverrideSummaryText));
        OnPropertyChanged(nameof(CapsuleGeometryOverrideSummaryText));
        ResetCapsuleBehaviorOverridesCommand.NotifyCanExecuteChanged();
        ResetCapsuleGeometryOverridesCommand.NotifyCanExecuteChanged();
        ResetAllCapsuleOverridesCommand.NotifyCanExecuteChanged();
    }

    private string GetWidgetCompactAnimationEffectDisplayName(string effect) =>
        SettingsService.NormalizeWidgetCompactAnimationEffect(effect) switch
        {
            SettingsService.WidgetCompactAnimationSnappy => _localizationService.T("Settings.Capsule.Animation.Snappy"),
            SettingsService.WidgetCompactAnimationSlow => _localizationService.T("Settings.Capsule.Animation.Slow"),
            SettingsService.WidgetCompactAnimationCustom => _localizationService.T("Settings.Capsule.Animation.Custom"),
            SettingsService.WidgetCompactAnimationNone => _localizationService.T("Settings.Capsule.Animation.None"),
            _ => _localizationService.T("Settings.Capsule.Animation.Smooth")
        };

    private string GetWidgetCompactHoverResponseDisplayName(string response) =>
        SettingsService.NormalizeWidgetCompactHoverResponse(response) switch
        {
            SettingsService.WidgetCompactHoverResponseSensitive =>
                _localizationService.T("Settings.Capsule.HoverResponse.Sensitive"),
            SettingsService.WidgetCompactHoverResponsePreventAccidental =>
                _localizationService.T("Settings.Capsule.HoverResponse.PreventAccidental"),
            SettingsService.WidgetCompactHoverResponseCustom =>
                _localizationService.T("Settings.Capsule.HoverResponse.Custom"),
            _ => _localizationService.T("Settings.Capsule.HoverResponse.Balanced")
        };

    private string GetWidgetCompactMediaCornerDisplayName(string mode) =>
        SettingsService.NormalizeWidgetCompactMediaCornerMode(mode) switch
        {
            SettingsService.WidgetCompactMediaCornerSquare => _localizationService.T("Settings.Capsule.MediaCorner.Square"),
            SettingsService.WidgetCompactMediaCornerSmall => _localizationService.T("Settings.Capsule.MediaCorner.Small"),
            SettingsService.WidgetCompactMediaCornerRound => _localizationService.T("Settings.Capsule.MediaCorner.Round"),
            _ => _localizationService.T("Settings.Capsule.MediaCorner.FollowWidget")
        };
}

public sealed record CapsuleOverrideSettingsItem(
    string WidgetId,
    string DisplayName,
    string Summary,
    string Glyph);
