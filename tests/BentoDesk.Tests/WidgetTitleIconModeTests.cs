using BentoDesk.Models;
using BentoDesk.Services;

namespace BentoDesk.Tests;

public sealed class WidgetTitleIconModeTests
{
    [Fact]
    public void MusicWidget_UsesDedicatedMusicIconFamily()
    {
        Assert.Equal(WidgetTitleIconKindNames.Music, WidgetTitleIconKindNames.FromWidgetKind(WidgetKind.Music));
        Assert.Equal("music", WidgetTitleIconKindNames.GetColorAssetName(WidgetTitleIconKind.Music));
        Assert.Equal("WidgetTitleIcon.Label.Music", WidgetTitleIconKindNames.GetLocalizationKey(WidgetTitleIconKind.Music));
    }
}
