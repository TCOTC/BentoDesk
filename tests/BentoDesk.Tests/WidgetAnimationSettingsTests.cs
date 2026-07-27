using BentoDesk.Services;

namespace BentoDesk.Tests;

public sealed class WidgetAnimationSettingsTests
{
    [Fact]
    public void GetDirectionalOffset_NoneFallsBackToRightwardMotion()
    {
        var offset = WidgetAnimationSettings.GetDirectionalOffset(
            SettingsService.WidgetAnimationSlideDirectionNone,
            (Left: 320, Right: 480, Up: 240, Down: 360));

        Assert.Equal((480d, 0d), offset);
    }

    [Fact]
    public void From_FadeKeepsConfiguredSpeedAndEasing()
    {
        var settings = new BentoDesk.Models.AppSettings
        {
            WidgetAnimationEffect = SettingsService.WidgetAnimationEffectFade,
            WidgetAnimationSlideDirection = SettingsService.WidgetAnimationSlideDirectionRight,
            WidgetAnimationSpeed = SettingsService.WidgetAnimationSpeedRelaxed,
            WidgetAnimationEasingIntensity = SettingsService.WidgetAnimationEasingLight
        };

        var options = WidgetAnimationSettings.From(settings);

        Assert.Equal(SettingsService.WidgetAnimationEffectFade, options.Effect);
        Assert.Equal(SettingsService.WidgetAnimationSpeedRelaxed, options.Speed);
        Assert.Equal(SettingsService.WidgetAnimationSlideDirectionNone, options.SlideDirection);
        Assert.Equal(SettingsService.WidgetAnimationEasingLight, options.EasingIntensity);
    }

    [Fact]
    public void From_NoneKeepsAnimationDisabled()
    {
        var settings = new BentoDesk.Models.AppSettings
        {
            WidgetAnimationEffect = SettingsService.WidgetAnimationEffectNone
        };

        var options = WidgetAnimationSettings.From(settings);

        Assert.Equal(SettingsService.WidgetAnimationEffectNone, options.Effect);
        Assert.Equal(SettingsService.WidgetAnimationEasingNone, options.EasingIntensity);
        Assert.False(options.UsesGroupOffset);
    }

    [Fact]
    public void UsesGroupOffset_IsAlwaysFalse()
    {
        Assert.False(WidgetAnimationSettings.UsesGroupOffset(
            SettingsService.WidgetAnimationEffectFade));
        Assert.False(WidgetAnimationSettings.UsesGroupOffset(
            SettingsService.WidgetAnimationEffectSlideFade));
        Assert.False(WidgetAnimationSettings.UsesGroupOffset("SlideRight"));
    }

    [Theory]
    [InlineData(SettingsService.WidgetAnimationSpeedVeryFast, 120)]
    [InlineData(SettingsService.WidgetAnimationSpeedFast, 220)]
    [InlineData(SettingsService.WidgetAnimationSpeedStandard, 240)]
    [InlineData(SettingsService.WidgetAnimationSpeedRelaxed, 520)]
    [InlineData(SettingsService.WidgetAnimationSpeedSlow, 680)]
    public void GetDurationMs_ReturnsCalibratedDuration(string speed, int expectedDurationMs)
    {
        Assert.Equal(expectedDurationMs, WidgetAnimationSettings.GetDurationMs(speed));
    }
}
