using BentoDesk.Models;
using BentoDesk.Services;

namespace BentoDesk.Tests;

public sealed class WidgetCompactPrivacyPolicyTests
{
    [Theory]
    [InlineData(WidgetKind.File)]
    public void ResolveContentMode_HidesSmartTextForTextBearingWidgets(WidgetKind widgetKind)
    {
        Assert.Equal(
            SettingsService.WidgetCompactContentModeSummary,
            WidgetCompactPrivacyPolicy.ResolveContentMode(
                SettingsService.WidgetCompactContentModeSmart,
                enabled: true,
                widgetKind: widgetKind));
    }

    [Fact]
    public void ResolveContentMode_PreservesSmartMusicControls()
    {
        Assert.Equal(
            SettingsService.WidgetCompactContentModeSmart,
            WidgetCompactPrivacyPolicy.ResolveContentMode(
                SettingsService.WidgetCompactContentModeSmart,
                enabled: true,
                widgetKind: WidgetKind.Music));
    }

    [Theory]
    [InlineData(WidgetKind.File, true)]
    [InlineData(WidgetKind.Music, true)]
    [InlineData(WidgetKind.Search, false)]
    public void HidesSensitiveContent_CoversExpectedWidgets(
        WidgetKind widgetKind,
        bool expected)
    {
        Assert.Equal(
            expected,
            WidgetCompactPrivacyPolicy.HidesSensitiveContent(
                enabled: true,
                widgetKind: widgetKind));
    }
}
