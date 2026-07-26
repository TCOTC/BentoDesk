using BentoDesk.Models;
using BentoDesk.Services;

namespace BentoDesk.Tests;

public sealed class WidgetRegistryTests
{
    [Fact]
    public void Default_KnowsImplementedWidgetKindsAndCreatesWindows()
    {
        var registry = WidgetRegistry.Default;

        Assert.True(registry.CanCreateWindow(WidgetKind.File));
        Assert.True(registry.CanCreateWindow(WidgetKind.Music));
    }

    [Fact]
    public void IsAvailableForSession_RespectsMusicFeatureWidgetState()
    {
        var registry = WidgetRegistry.Default;
        var musicWidget = new WidgetConfig
        {
            WidgetKind = WidgetKind.Music
        };
        var settings = new AppSettings();

        Assert.False(registry.IsAvailableForSession(musicWidget, settings));

        FeatureWidgetSettings.SetEnabled(settings, WidgetKind.Music, true);

        Assert.True(registry.IsAvailableForSession(musicWidget, settings));
    }
}
