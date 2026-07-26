using BentoDesk.Models;
using BentoDesk.Services;

namespace BentoDesk.Tests;

public sealed class OrganizerServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _desktopRoot;
    private readonly SettingsService _settingsService;
    private readonly FileService _fileService;
    private readonly OrganizerService _organizerService;

    public OrganizerServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "BentoDesk.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _desktopRoot = Directory.CreateDirectory(Path.Combine(_tempRoot, "desktop")).FullName;

        _settingsService = new SettingsService(Path.Combine(_tempRoot, "settings"));
        _fileService = new FileService();
        _organizerService = new OrganizerService(_settingsService, _fileService, () => _desktopRoot);
    }

    [Fact]
    public async Task OrganizeDropAsync_Move_RecordsUndoableHistoryAndMovesFile()
    {
        string sourceDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "source")).FullName;
        string targetDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "widget")).FullName;
        string sourcePath = Path.Combine(sourceDirectory, "note.txt");
        File.WriteAllText(sourcePath, "content");
        var widget = CreateMappedWidget(targetDirectory);

        var history = await _organizerService.OrganizeDropAsync(widget, "Widget", [sourcePath], move: true);

        string destinationPath = Path.Combine(targetDirectory, "note.txt");
        Assert.False(File.Exists(sourcePath));
        Assert.True(File.Exists(destinationPath));
        Assert.True(history.CanUndo);
        Assert.False(history.IsFailed);
        Assert.Equal(OrganizationActionType.ManagedDrop, history.ActionType);
        Assert.Equal("Move", history.TransferMode);
        var item = Assert.Single(history.Items);
        Assert.Equal(sourcePath, item.SourcePath);
        Assert.Equal(destinationPath, item.DestinationPath);
        Assert.Same(history, Assert.Single(_settingsService.Settings.RecentOrganizationHistory));
    }

    [Fact]
    public async Task OrganizeDropAsync_Copy_RecordsNonUndoableHistoryAndKeepsSourceFile()
    {
        string sourceDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "source")).FullName;
        string targetDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "widget")).FullName;
        string sourcePath = Path.Combine(sourceDirectory, "note.txt");
        File.WriteAllText(sourcePath, "content");
        var widget = CreateMappedWidget(targetDirectory);

        var history = await _organizerService.OrganizeDropAsync(widget, "Widget", [sourcePath], move: false);

        Assert.True(File.Exists(sourcePath));
        Assert.True(File.Exists(Path.Combine(targetDirectory, "note.txt")));
        Assert.False(history.CanUndo);
        Assert.Equal("Copy", history.TransferMode);
    }

    [Fact]
    public async Task OrganizeDropAsync_DoesNotBroadcastGlobalSettingsChanged()
    {
        string sourceDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "source-notify")).FullName;
        string targetDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "widget-notify")).FullName;
        string sourcePath = Path.Combine(sourceDirectory, "note.txt");
        File.WriteAllText(sourcePath, "content");
        var widget = CreateMappedWidget(targetDirectory);
        int settingsChangedCount = 0;
        _settingsService.SettingsChanged += () => settingsChangedCount++;

        await _organizerService.OrganizeDropAsync(widget, "Widget", [sourcePath], move: false);

        Assert.Equal(0, settingsChangedCount);
        Assert.Single(_settingsService.Settings.RecentOrganizationHistory);
    }

    [Fact]
    public async Task UndoLatestAsync_RestoresMovedFileAndMarksHistoryUndone()
    {
        string sourceDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "source")).FullName;
        string targetDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "widget")).FullName;
        string sourcePath = Path.Combine(sourceDirectory, "note.txt");
        File.WriteAllText(sourcePath, "content");
        var widget = CreateMappedWidget(targetDirectory);
        var history = await _organizerService.OrganizeDropAsync(widget, "Widget", [sourcePath], move: true);

        bool undone = await _organizerService.UndoLatestAsync();

        Assert.True(undone);
        Assert.True(File.Exists(sourcePath));
        Assert.False(File.Exists(Path.Combine(targetDirectory, "note.txt")));
        Assert.True(history.IsUndone);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public async Task OrganizeDropAsync_ManagedDesktop_KeepsFileOnDesktopAndRecordsMembership()
    {
        string sourcePath = Path.Combine(_desktopRoot, "desk-note.txt");
        File.WriteAllText(sourcePath, "content");
        var widget = CreateManagedDesktopWidget();
        _settingsService.Settings.Widgets.Add(widget);

        var history = await _organizerService.OrganizeDropAsync(widget, "Widget", [sourcePath], move: true);

        Assert.True(File.Exists(sourcePath));
        Assert.Contains(widget.Items, item => item.Path.Equals(sourcePath, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(sourcePath, Assert.Single(history.Items).DestinationPath);
    }

    [Fact]
    public async Task OrganizeDropAsync_ManagedDesktop_MovesOffDesktopSourceThenClaims()
    {
        string sourceDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "downloads")).FullName;
        string sourcePath = Path.Combine(sourceDirectory, "dl-note.txt");
        File.WriteAllText(sourcePath, "content");
        var widget = CreateManagedDesktopWidget();
        _settingsService.Settings.Widgets.Add(widget);

        var history = await _organizerService.OrganizeDropAsync(widget, "Widget", [sourcePath], move: true);

        string destinationPath = Assert.Single(history.Items).DestinationPath;
        Assert.False(File.Exists(sourcePath));
        Assert.True(File.Exists(destinationPath));
        Assert.Equal(_desktopRoot, Path.GetDirectoryName(destinationPath));
        Assert.Contains(widget.Items, item => item.Path.Equals(destinationPath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MoveItemsBackToDesktopAsync_ManagedDesktop_RemovesMembershipOnly()
    {
        string sourcePath = Path.Combine(_desktopRoot, "keep.txt");
        File.WriteAllText(sourcePath, "content");
        var widget = CreateManagedDesktopWidget();
        widget.Items.Add(new WidgetItemConfig { Path = sourcePath, SortOrder = 0 });
        _settingsService.Settings.Widgets.Add(widget);

        var history = await _organizerService.MoveItemsBackToDesktopAsync(widget, "Widget", [sourcePath]);

        Assert.True(File.Exists(sourcePath));
        Assert.Empty(widget.Items);
        Assert.Equal(sourcePath, Assert.Single(history.Items).DestinationPath);
    }

    [Fact]
    public async Task OrganizeDropAsync_UncategorizedDefault_RemovesOtherMembershipWithoutClaiming()
    {
        string sourcePath = Path.Combine(_desktopRoot, "inbox.txt");
        File.WriteAllText(sourcePath, "content");
        var category = CreateManagedDesktopWidget();
        category.Items.Add(new WidgetItemConfig { Path = sourcePath, SortOrder = 0 });
        var inbox = CreateManagedDesktopWidget();
        inbox.IsUncategorizedDefault = true;
        _settingsService.Settings.Widgets.Add(category);
        _settingsService.Settings.Widgets.Add(inbox);

        await _organizerService.OrganizeDropAsync(inbox, "未分类", [sourcePath], move: true);

        Assert.True(File.Exists(sourcePath));
        Assert.Empty(category.Items);
        Assert.Empty(inbox.Items);
        Assert.DoesNotContain(
            sourcePath,
            ManagedDesktopMembership.CollectClaimedPaths(_settingsService.Settings.Widgets));
    }

    [Fact]
    public async Task MoveItemBackToDesktopAsync_RejectsWidgetsWithoutMappedFolder()
    {
        var widget = CreateMappedWidget(string.Empty);
        var item = new WidgetItem
        {
            Path = Path.Combine(_tempRoot, "missing.txt"),
            Name = "missing"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _organizerService.MoveItemBackToDesktopAsync(widget, "Widget", item));
    }

    [Fact]
    public async Task MoveItemBackToDesktopAsync_AllowsMappedFolderWidgets()
    {
        string widgetFolder = Directory.CreateDirectory(Path.Combine(_tempRoot, "mapped")).FullName;
        string sourcePath = Path.Combine(widgetFolder, "mapped-note.txt");
        File.WriteAllText(sourcePath, "content");
        var widget = CreateMappedWidget(widgetFolder);
        var item = new WidgetItem
        {
            Path = sourcePath,
            Name = "mapped-note"
        };

        var history = await _organizerService.MoveItemBackToDesktopAsync(widget, "Widget", item);

        Assert.False(File.Exists(sourcePath));
        Assert.True(File.Exists(history.Items.Single().DestinationPath));
        Assert.Equal(_desktopRoot, Path.GetDirectoryName(history.Items.Single().DestinationPath));
    }

    private WidgetConfig CreateManagedDesktopWidget()
    {
        return new WidgetConfig
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Managed",
            MappedFolderPath = _desktopRoot,
            FollowsDefaultStoragePath = true
        };
    }

    private static WidgetConfig CreateMappedWidget(string folderPath)
    {
        return new WidgetConfig
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Widget",
            MappedFolderPath = folderPath,
            FollowsDefaultStoragePath = false,
            ManagedFolderName = Path.GetFileName(folderPath)
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
