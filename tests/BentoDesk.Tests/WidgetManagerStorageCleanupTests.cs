using BentoDesk.Models;
using BentoDesk.Services;

namespace BentoDesk.Tests;

public sealed class WidgetManagerStorageCleanupTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _desktopRoot;
    private readonly SettingsService _settingsService;
    private readonly WidgetManager _widgetManager;

    public WidgetManagerStorageCleanupTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "BentoDesk.Tests", Guid.NewGuid().ToString("N"));
        _desktopRoot = Directory.CreateDirectory(Path.Combine(_tempRoot, "desktop")).FullName;

        _settingsService = new SettingsService(Path.Combine(_tempRoot, "settings"));

        var fileService = new FileService();
        var organizerService = new OrganizerService(_settingsService, fileService);
        var themeService = new ThemeService(_settingsService);
        _widgetManager = new WidgetManager(
            _settingsService,
            fileService,
            organizerService,
            themeService,
            () => _desktopRoot);
    }

    [Fact]
    public async Task RenameWidgetAsync_DesktopMembershipWidgetRenamesWithoutMovingDesktopFolder()
    {
        var widget = CreateDesktopMembershipWidget("待处理");
        _settingsService.Settings.Widgets.Add(widget);
        File.WriteAllText(Path.Combine(_desktopRoot, "keep.txt"), "desktop");

        await _widgetManager.RenameWidgetAsync(widget.Id, "本周项目");

        Assert.Equal("本周项目", widget.Name);
        Assert.Null(widget.ManagedFolderName);
        Assert.Equal(_desktopRoot, widget.MappedFolderPath);
        Assert.True(Directory.Exists(_desktopRoot));
        Assert.Equal("desktop", File.ReadAllText(Path.Combine(_desktopRoot, "keep.txt")));
    }

    [Fact]
    public async Task RenameWidgetAsync_DesktopMembershipWidgetRejectsDuplicateName()
    {
        var existing = CreateDesktopMembershipWidget("AI");
        var target = CreateDesktopMembershipWidget("Work");
        _settingsService.Settings.Widgets.Add(existing);
        _settingsService.Settings.Widgets.Add(target);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _widgetManager.RenameWidgetAsync(target.Id, "AI"));

        Assert.Equal("Work", target.Name);
        Assert.Equal(_desktopRoot, target.MappedFolderPath);
    }

    [Fact]
    public async Task RenameWidgetAsync_SameNameCommitDoesNotThrow()
    {
        var widget = CreateDesktopMembershipWidget("待处理");
        _settingsService.Settings.Widgets.Add(widget);

        await _widgetManager.RenameWidgetAsync(widget.Id, "待处理");

        Assert.Equal("待处理", widget.Name);
        Assert.Null(widget.ManagedFolderName);
        Assert.Equal(_desktopRoot, widget.MappedFolderPath);
    }

    [Fact]
    public async Task RemoveWidgetAsync_DesktopMembershipKeepsDesktopFiles()
    {
        var widget = CreateDesktopMembershipWidget("Work");
        _settingsService.Settings.Widgets.Add(widget);
        string desktopFile = Path.Combine(_desktopRoot, "note.txt");
        File.WriteAllText(desktopFile, "keep");

        await _widgetManager.RemoveWidgetAsync(widget.Id);

        Assert.DoesNotContain(_settingsService.Settings.Widgets, item => item.Id == widget.Id);
        Assert.Contains(widget.Id, _settingsService.Settings.DeletedWidgetIds);
        Assert.True(File.Exists(desktopFile));
        Assert.Equal("keep", File.ReadAllText(desktopFile));
    }

    private WidgetConfig CreateDesktopMembershipWidget(string name)
    {
        return new WidgetConfig
        {
            Name = name,
            WidgetKind = WidgetKind.File,
            MappedFolderPath = _desktopRoot,
            FollowsDefaultStoragePath = true,
            ManagedFolderName = null
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
