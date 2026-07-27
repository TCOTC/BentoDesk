using BentoDesk.Services;

namespace BentoDesk.ViewModels;

public partial class SettingsViewModel
{
    private static string NormalizeWidgetAnimationEffect(string? effect)
    {
        return effect == SettingsService.WidgetAnimationEffectNone
            ? SettingsService.WidgetAnimationEffectNone
            : SettingsService.WidgetAnimationEffectFade;
    }

    private static string NormalizeWidgetAnimationSpeed(string? speed)
    {
        return speed is
            SettingsService.WidgetAnimationSpeedVeryFast or
            SettingsService.WidgetAnimationSpeedFast or
            SettingsService.WidgetAnimationSpeedStandard or
            SettingsService.WidgetAnimationSpeedRelaxed or
            SettingsService.WidgetAnimationSpeedSlow
            ? speed
            : SettingsService.WidgetAnimationSpeedStandard;
    }

    private static string NormalizeWidgetAnimationSlideDirection(string? direction)
    {
        return direction is
            SettingsService.WidgetAnimationSlideDirectionNone or
            SettingsService.WidgetAnimationSlideDirectionLeft or
            SettingsService.WidgetAnimationSlideDirectionRight or
            SettingsService.WidgetAnimationSlideDirectionUp or
            SettingsService.WidgetAnimationSlideDirectionDown
            ? direction
            : SettingsService.WidgetAnimationSlideDirectionRight;
    }

    private static string NormalizeWidgetAnimationEasingIntensity(string? intensity)
    {
        return intensity is
            SettingsService.WidgetAnimationEasingNone or
            SettingsService.WidgetAnimationEasingLight or
            SettingsService.WidgetAnimationEasingStandard or
            SettingsService.WidgetAnimationEasingStrong
            ? intensity
            : SettingsService.WidgetAnimationEasingStandard;
    }

    private static string NormalizeWidgetTitleIconModeSetting(string? mode)
    {
        return SettingsService.NormalizeWidgetTitleIconModeSetting(mode);
    }
}
