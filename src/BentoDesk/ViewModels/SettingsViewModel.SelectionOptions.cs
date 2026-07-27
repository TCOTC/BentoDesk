using BentoDesk.Models;

namespace BentoDesk.ViewModels;

public partial class SettingsViewModel
{
    public IReadOnlyList<SettingsOption> AvailableThemeOptions =>
        CreateSelectionOptions(AvailableThemes, AvailableThemeDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableTrayIconStyleOptions =>
        CreateSelectionOptions(AvailableTrayIconStyles, AvailableTrayIconStyleDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableWidgetCornerPreferenceOptions =>
        CreateSelectionOptions(AvailableWidgetCornerPreferences, AvailableWidgetCornerPreferenceDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableWidgetMaterialTypeOptions =>
        CreateSelectionOptions(AvailableWidgetMaterialTypes, AvailableWidgetMaterialTypeDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableWidgetBorderColorModeOptions =>
        CreateSelectionOptions(AvailableWidgetBorderColorModes, AvailableWidgetBorderColorModeDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableWidgetBorderStyleOptions =>
        CreateSelectionOptions(AvailableWidgetBorderStyles, AvailableWidgetBorderStyleDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableWidgetCollapseBehaviorOptions =>
        CreateSelectionOptions(AvailableWidgetCollapseBehaviors, AvailableWidgetCollapseBehaviorDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableWidgetCompactWidthModeOptions =>
        CreateSelectionOptions(
            AvailableWidgetCompactWidthModes,
            AvailableWidgetCompactWidthModeDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableWidgetCapsuleArrangementOptions =>
        CreateSelectionOptions(
            AvailableWidgetCapsuleArrangementModes,
            AvailableWidgetCapsuleArrangementDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableWidgetCapsuleBarPlacementOptions =>
        CreateSelectionOptions(
            AvailableWidgetCapsuleBarPlacements,
            AvailableWidgetCapsuleBarPlacementDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableWidgetCapsuleBarDirectionOptions =>
        CreateSelectionOptions(
            AvailableWidgetCapsuleBarDirections,
            AvailableWidgetCapsuleBarDirectionDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableWidgetCompactContentModeOptions =>
        CreateSelectionOptions(AvailableWidgetCompactContentModes, AvailableWidgetCompactContentModeDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableWidgetCompactAnimationEffectOptions =>
        CreateSelectionOptions(AvailableWidgetCompactAnimationEffects, AvailableWidgetCompactAnimationEffectDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableWidgetCompactHoverResponseOptions =>
        CreateSelectionOptions(AvailableWidgetCompactHoverResponses, AvailableWidgetCompactHoverResponseDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableWidgetCompactMediaCornerOptions =>
        CreateSelectionOptions(AvailableWidgetCompactMediaCornerModes, AvailableWidgetCompactMediaCornerDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableLayoutDensityOptions =>
        CreateSelectionOptions(AvailableLayoutDensities, AvailableLayoutDensityDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableAnimationPresetOptions =>
        CreateSelectionOptions(AvailableAnimationPresets, AvailableAnimationPresetDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableDisplayWidgetChromeModeOptions =>
        CreateSelectionOptions(AvailableDisplayWidgetChromeModes, AvailableDisplayWidgetChromeModeDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableInteractiveWidgetChromeModeOptions =>
        CreateSelectionOptions(AvailableInteractiveWidgetChromeModes, AvailableInteractiveWidgetChromeModeDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableWidgetTitleIconModeOptions =>
        CreateSelectionOptions(AvailableWidgetTitleIconModes, AvailableWidgetTitleIconModeDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableManagedDropActionOptions =>
        CreateSelectionOptions(AvailableManagedDropActions, AvailableManagedDropActionDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableMusicDisplayModeOptions =>
        CreateSelectionOptions(AvailableMusicDisplayModes, AvailableMusicDisplayModeDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableFileStackGroupByOptions =>
        CreateSelectionOptions(AvailableFileStackGroupBys, AvailableFileStackGroupByDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableFileStackThresholdOptions =>
        CreateSelectionOptions(AvailableFileStackThresholds, AvailableFileStackThresholdDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableFileStackOrderByOptions =>
        CreateSelectionOptions(AvailableFileStackOrderBys, AvailableFileStackOrderByDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableFileStackUnmatchedBehaviorOptions =>
        CreateSelectionOptions(AvailableFileStackUnmatchedBehaviors, AvailableFileStackUnmatchedBehaviorDisplayNames);

    internal static IReadOnlyList<SettingsOption> CreateSelectionOptions<T>(
        IReadOnlyList<T> values,
        IReadOnlyList<string> displayNames)
    {
        if (values.Count != displayNames.Count)
        {
            throw new InvalidOperationException("Setting option values and display names must have the same length.");
        }

        var options = new SettingsOption[values.Count];
        for (int index = 0; index < values.Count; index++)
        {
            options[index] = new SettingsOption(values[index]!, displayNames[index]);
        }

        return options;
    }

    private void NotifySelectionOptionsChanged()
    {
        OnPropertyChanged(nameof(AvailableThemeOptions));
        OnPropertyChanged(nameof(AvailableTrayIconStyleOptions));
        OnPropertyChanged(nameof(AvailableWidgetCornerPreferenceOptions));
        OnPropertyChanged(nameof(AvailableWidgetMaterialTypeOptions));
        OnPropertyChanged(nameof(AvailableWidgetBorderColorModeOptions));
        OnPropertyChanged(nameof(AvailableWidgetBorderStyleOptions));
        OnPropertyChanged(nameof(AvailableWidgetCollapseBehaviorOptions));
        OnPropertyChanged(nameof(AvailableWidgetCompactWidthModeOptions));
        OnPropertyChanged(nameof(AvailableWidgetCapsuleArrangementOptions));
        OnPropertyChanged(nameof(AvailableWidgetCapsuleBarPlacementOptions));
        OnPropertyChanged(nameof(AvailableWidgetCapsuleBarDirectionOptions));
        OnPropertyChanged(nameof(AvailableWidgetCompactContentModeOptions));
        OnPropertyChanged(nameof(AvailableWidgetCompactAnimationEffectOptions));
        OnPropertyChanged(nameof(AvailableWidgetCompactHoverResponseOptions));
        OnPropertyChanged(nameof(AvailableWidgetCompactMediaCornerOptions));
        OnPropertyChanged(nameof(AvailableLayoutDensityOptions));
        OnPropertyChanged(nameof(AvailableAnimationPresetOptions));
        OnPropertyChanged(nameof(AvailableDisplayWidgetChromeModeOptions));
        OnPropertyChanged(nameof(AvailableInteractiveWidgetChromeModeOptions));
        OnPropertyChanged(nameof(AvailableWidgetTitleIconModeOptions));
        OnPropertyChanged(nameof(AvailableManagedDropActionOptions));
        OnPropertyChanged(nameof(AvailableMusicDisplayModeOptions));
        OnPropertyChanged(nameof(AvailableFileStackGroupByOptions));
        OnPropertyChanged(nameof(AvailableFileStackThresholdOptions));
        OnPropertyChanged(nameof(AvailableFileStackOrderByOptions));
        OnPropertyChanged(nameof(AvailableFileStackUnmatchedBehaviorOptions));
    }
}
