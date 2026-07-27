using BentoDesk.Models;
using BentoDesk.Services;

namespace BentoDesk.Tests;

public sealed class MusicWidgetSettingsMigrationTests
{
    [Fact]
    public void NormalizeMusicWidgetSettings_MigratesLegacyFeatureStateAndClearsDictionary()
    {
        var settings = new AppSettings
        {
            MusicWidgetEnabled = false,
            FeatureWidgetEnabledStates = new Dictionary<string, bool>
            {
                [WidgetKind.Music.ToString()] = true
            }
        };

        bool changed = SettingsService.NormalizeMusicWidgetSettings(settings);

        Assert.True(changed);
        Assert.True(settings.MusicWidgetEnabled);
        Assert.Empty(settings.FeatureWidgetEnabledStates);
    }

    [Fact]
    public void NormalizeMusicWidgetSettings_DefaultMusicDisabled()
    {
        var settings = new AppSettings();

        Assert.False(settings.MusicWidgetEnabled);
        Assert.False(SettingsService.NormalizeMusicWidgetSettings(settings));
    }
}
