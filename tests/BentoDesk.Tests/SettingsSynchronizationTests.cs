using BentoDesk.Services;

namespace BentoDesk.Tests;

public sealed class SettingsSynchronizationTests
{
    [Fact]
    public void LocalizationServiceLoadsChineseStrings()
    {
        var localizationService = new LocalizationService();

        Assert.Equal("zh-CN", localizationService.CurrentCultureName);
        Assert.False(string.IsNullOrWhiteSpace(localizationService.T("Settings.Material.Mica")));
        Assert.False(string.IsNullOrWhiteSpace(localizationService.T("Music.Title")));
    }

    [Fact]
    public async Task SaveAsyncNotifiesSubscribersAndPersistsSettingsSnapshot()
    {
        using var scope = new TempSettingsScope();
        var settingsService = new SettingsService(scope.RootPath);
        bool settingsChanged = false;
        settingsService.SettingsChanged += () => settingsChanged = true;

        settingsService.Settings.MusicDisplayMode = SettingsService.MusicDisplayModeCover;

        await settingsService.SaveAsync();

        Assert.True(settingsChanged);

        var reloadedService = new SettingsService(scope.RootPath);
        await reloadedService.LoadAsync();

        Assert.Equal(SettingsService.MusicDisplayModeCover, reloadedService.Settings.MusicDisplayMode);
    }

    private sealed class TempSettingsScope : IDisposable
    {
        public TempSettingsScope()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "BentoDeskTests", Guid.NewGuid().ToString("N"));
        }

        public string RootPath { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(RootPath))
                {
                    Directory.Delete(RootPath, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup for temporary test settings.
            }
        }
    }
}
