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

    public Visibility CollapseHoverResponseEntryVisibility =>
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


    public int CollapseCustomRuleCount => _settingsService.Settings.Widgets.Count(widget =>
        widget.Metadata?.ContainsKey(WidgetCollapseBehaviorNames.MetadataKey) == true);

    public int CollapseCustomWidthCount =>
        _settingsService.Settings.Widgets.Count(widget => widget.CompactWidth is not null);

    public int CollapseSavedPlacementCount =>
        _settingsService.Settings.Widgets.Count(widget => widget.CompactPlacement is not null);

    public bool HasCollapseBehaviorOverrides => CollapseCustomRuleCount > 0;

    public bool HasCollapseGeometryOverrides =>
        CollapseCustomWidthCount > 0 || CollapseSavedPlacementCount > 0;

    public int CollapseOverrideWidgetCount => _settingsService.Settings.Widgets.Count(HasCollapseOverride);

    public bool HasCollapseOverrides => CollapseOverrideWidgetCount > 0;

    public Visibility CollapseOverridesEntryVisibility =>
        HasCollapseOverrides ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CollapseOverridesListVisibility =>
        HasCollapseOverrides ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CollapseOverridesEmptyVisibility =>
        HasCollapseOverrides ? Visibility.Collapsed : Visibility.Visible;

    public IReadOnlyList<CollapseOverrideSettingsItem> CollapseOverrideItems =>
        _settingsService.Settings.Widgets
            .Where(HasCollapseOverride)
            .Select(CreateCollapseOverrideSettingsItem)
            .ToArray();

    public string CollapseOverrideSummaryText => _localizationService.Format(
        "Settings.Collapse.Overrides.Summary",
        CollapseOverrideWidgetCount);

    public string CollapseBehaviorOverrideSummaryText => _localizationService.Format(
        "Settings.Collapse.Overrides.Behavior.Summary",
        CollapseCustomRuleCount);

    public string CollapseGeometryOverrideSummaryText => _localizationService.Format(
        "Settings.Collapse.Overrides.Geometry.Summary",
        CollapseCustomWidthCount,
        CollapseSavedPlacementCount);

    [RelayCommand]
    private void ResetCollapseBehaviorOverrides()
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
            NotifyCollapseOverridePropertiesChanged();
        }
    }

    [RelayCommand]
    private void ResetCollapseGeometryOverrides()
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
            NotifyCollapseOverridePropertiesChanged();
        }
    }

    public void ResetCollapseOverridesForWidget(string widgetId)
    {
        var widget = _settingsService.Settings.Widgets.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, widgetId, StringComparison.Ordinal));
        if (widget is null || !ClearCollapseOverrides(widget))
        {
            return;
        }

        _settingsService.SaveDebounced();
        NotifyCollapseOverridePropertiesChanged();
    }

    [RelayCommand]
    private void ResetAllCollapseOverrides()
    {
        bool changed = false;
        foreach (var widget in _settingsService.Settings.Widgets)
        {
            changed |= ClearCollapseOverrides(widget);
        }

        if (!changed)
        {
            return;
        }

        _settingsService.SaveDebounced();
        NotifyCollapseOverridePropertiesChanged();
    }

    private static bool HasCollapseOverride(WidgetConfig widget) =>
        widget.Metadata?.ContainsKey(WidgetCollapseBehaviorNames.MetadataKey) == true ||
        widget.CompactWidth is not null ||
        widget.CompactPlacement is not null;

    private static bool ClearCollapseOverrides(WidgetConfig widget)
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

    private CollapseOverrideSettingsItem CreateCollapseOverrideSettingsItem(WidgetConfig widget)
    {
        var details = new List<string>(3);
        if (widget.Metadata is not null &&
            widget.Metadata.TryGetValue(WidgetCollapseBehaviorNames.MetadataKey, out string? behavior))
        {
            details.Add(_localizationService.Format(
                "Settings.Collapse.Overrides.Item.Behavior",
                GetWidgetCollapseBehaviorDisplayName(behavior)));
        }

        if (widget.CompactWidth is { } width)
        {
            details.Add(_localizationService.Format(
                "Settings.Collapse.Overrides.Item.Width",
                Math.Round(width)));
        }

        if (widget.CompactPlacement is not null)
        {
            details.Add(_localizationService.T("Settings.Collapse.Overrides.Item.Position"));
        }

        string displayName = string.IsNullOrWhiteSpace(widget.Name)
            ? GetWidgetKindDisplayName(widget.WidgetKind)
            : widget.Name.Trim();
        return new CollapseOverrideSettingsItem(
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

    private void NotifyCollapseOverridePropertiesChanged()
    {
        OnPropertyChanged(nameof(CollapseCustomRuleCount));
        OnPropertyChanged(nameof(CollapseCustomWidthCount));
        OnPropertyChanged(nameof(CollapseSavedPlacementCount));
        OnPropertyChanged(nameof(HasCollapseBehaviorOverrides));
        OnPropertyChanged(nameof(HasCollapseGeometryOverrides));
        OnPropertyChanged(nameof(CollapseOverrideWidgetCount));
        OnPropertyChanged(nameof(HasCollapseOverrides));
        OnPropertyChanged(nameof(CollapseOverridesEntryVisibility));
        OnPropertyChanged(nameof(CollapseOverridesListVisibility));
        OnPropertyChanged(nameof(CollapseOverridesEmptyVisibility));
        OnPropertyChanged(nameof(CollapseOverrideItems));
        OnPropertyChanged(nameof(CollapseOverrideSummaryText));
        OnPropertyChanged(nameof(CollapseBehaviorOverrideSummaryText));
        OnPropertyChanged(nameof(CollapseGeometryOverrideSummaryText));
        ResetCollapseBehaviorOverridesCommand.NotifyCanExecuteChanged();
        ResetCollapseGeometryOverridesCommand.NotifyCanExecuteChanged();
        ResetAllCollapseOverridesCommand.NotifyCanExecuteChanged();
    }

    private string GetWidgetCompactAnimationEffectDisplayName(string effect) =>
        SettingsService.NormalizeWidgetCompactAnimationEffect(effect) switch
        {
            SettingsService.WidgetCompactAnimationSnappy => _localizationService.T("Settings.Collapse.Animation.Snappy"),
            SettingsService.WidgetCompactAnimationSlow => _localizationService.T("Settings.Collapse.Animation.Slow"),
            SettingsService.WidgetCompactAnimationCustom => _localizationService.T("Settings.Collapse.Animation.Custom"),
            SettingsService.WidgetCompactAnimationNone => _localizationService.T("Settings.Collapse.Animation.None"),
            _ => _localizationService.T("Settings.Collapse.Animation.Smooth")
        };

    private string GetWidgetCompactHoverResponseDisplayName(string response) =>
        SettingsService.NormalizeWidgetCompactHoverResponse(response) switch
        {
            SettingsService.WidgetCompactHoverResponseSensitive =>
                _localizationService.T("Settings.Collapse.HoverResponse.Sensitive"),
            SettingsService.WidgetCompactHoverResponsePreventAccidental =>
                _localizationService.T("Settings.Collapse.HoverResponse.PreventAccidental"),
            SettingsService.WidgetCompactHoverResponseCustom =>
                _localizationService.T("Settings.Collapse.HoverResponse.Custom"),
            _ => _localizationService.T("Settings.Collapse.HoverResponse.Balanced")
        };

    private string GetWidgetCompactMediaCornerDisplayName(string mode) =>
        SettingsService.NormalizeWidgetCompactMediaCornerMode(mode) switch
        {
            SettingsService.WidgetCompactMediaCornerSquare => _localizationService.T("Settings.Collapse.MediaCorner.Square"),
            SettingsService.WidgetCompactMediaCornerSmall => _localizationService.T("Settings.Collapse.MediaCorner.Small"),
            SettingsService.WidgetCompactMediaCornerRound => _localizationService.T("Settings.Collapse.MediaCorner.Round"),
            _ => _localizationService.T("Settings.Collapse.MediaCorner.FollowWidget")
        };
}

public sealed record CollapseOverrideSettingsItem(
    string WidgetId,
    string DisplayName,
    string Summary,
    string Glyph);
