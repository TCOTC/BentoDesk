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

        settingsService.Settings.WidgetCapsuleModeEnabled = true;
        settingsService.Settings.WidgetCompactWidthMode =
            SettingsService.WidgetCompactWidthModeIndependent;
        settingsService.Settings.WidgetCapsuleArrangementMode =
            SettingsService.WidgetCapsuleArrangementBar;
        settingsService.Settings.WidgetCapsuleBarPlacement =
            SettingsService.WidgetCapsuleBarPlacementRight;
        settingsService.Settings.WidgetCapsuleBarDirection =
            SettingsService.WidgetCapsuleBarDirectionVertical;
        settingsService.Settings.WidgetCapsuleBarSpacing = 12;
        settingsService.Settings.WidgetCapsuleBarOrder = ["music", "tags"];
        settingsService.Settings.WidgetCapsuleFreePlacements["music"] = new BentoDesk.Models.WidgetCompactPlacement
        {
            X = 120,
            Y = 80,
            PositionAnchor = WidgetPositionAnchors.LeftTop
        };
        settingsService.Settings.MusicDisplayMode = SettingsService.MusicDisplayModeCover;

        await settingsService.SaveAsync();

        Assert.True(settingsChanged);

        var reloadedService = new SettingsService(scope.RootPath);
        await reloadedService.LoadAsync();

        Assert.True(reloadedService.Settings.WidgetCapsuleModeEnabled);
        Assert.Equal(
            SettingsService.WidgetCompactWidthModeIndependent,
            reloadedService.Settings.WidgetCompactWidthMode);
        Assert.Equal(
            SettingsService.WidgetCapsuleArrangementBar,
            reloadedService.Settings.WidgetCapsuleArrangementMode);
        Assert.Equal(
            SettingsService.WidgetCapsuleBarPlacementRight,
            reloadedService.Settings.WidgetCapsuleBarPlacement);
        Assert.Equal(
            SettingsService.WidgetCapsuleBarDirectionVertical,
            reloadedService.Settings.WidgetCapsuleBarDirection);
        Assert.Equal(12d, reloadedService.Settings.WidgetCapsuleBarSpacing);
        Assert.Equal(new[] { "music", "tags" }, reloadedService.Settings.WidgetCapsuleBarOrder);
        Assert.Equal(120d, reloadedService.Settings.WidgetCapsuleFreePlacements["music"].X);
        Assert.Equal(SettingsService.MusicDisplayModeCover, reloadedService.Settings.MusicDisplayMode);
    }

    [Fact]
    public async Task LegacyCapsuleArrangementMigratesToWidgetBarDirection()
    {
        using var scope = new TempSettingsScope();
        var settingsService = new SettingsService(scope.RootPath);
        settingsService.Settings.WidgetCapsuleArrangementMode =
            SettingsService.WidgetCapsuleArrangementVertical;
        settingsService.Settings.WidgetCapsuleBarOrder = ["legacy-one", "legacy-two"];
        await settingsService.SaveAsync(notifySubscribers: false);

        var reloadedService = new SettingsService(scope.RootPath);
        await reloadedService.LoadAsync();

        Assert.Equal(
            SettingsService.WidgetCapsuleArrangementBar,
            reloadedService.Settings.WidgetCapsuleArrangementMode);
        Assert.Equal(
            SettingsService.WidgetCapsuleBarDirectionVertical,
            reloadedService.Settings.WidgetCapsuleBarDirection);
        Assert.Empty(reloadedService.Settings.WidgetCapsuleBarOrder);
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
            }
        }
    }
}
