using BentoDesk.Models;
using BentoDesk.Services;

namespace BentoDesk.Tests;

public sealed class WidgetManagerStorageCleanupTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _storageRoot;
    private readonly string _desktopRoot;
    private readonly SettingsService _settingsService;
    private readonly WidgetManager _widgetManager;

    public WidgetManagerStorageCleanupTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "BentoDesk.Tests", Guid.NewGuid().ToString("N"));
        _storageRoot = Directory.CreateDirectory(Path.Combine(_tempRoot, "storage")).FullName;
        _desktopRoot = Directory.CreateDirectory(Path.Combine(_tempRoot, "desktop")).FullName;

        _settingsService = new SettingsService(Path.Combine(_tempRoot, "settings"));
        _settingsService.Settings.DefaultManagedStorageRootPath = _storageRoot;

        var fileService = new FileService();
        var organizerService = new OrganizerService(_settingsService, fileService);
        var themeService = new ThemeService(_settingsService);
        _widgetManager = new WidgetManager(
            _settingsService,
            fileService,
            organizerService,
            themeService,
            () => _desktopRoot,
            recycleManagedFolderDeletes: false);
    }

    [Fact]
    public void GetOrphanManagedStorageFolders_ReturnsOnlyUntrackedRootChildren()
    {
        string activeFolder = Directory.CreateDirectory(Path.Combine(_storageRoot, "Active")).FullName;
        string orphanFolder = Directory.CreateDirectory(Path.Combine(_storageRoot, "Orphan")).FullName;
        string mappedFolderOutsideRoot = Directory.CreateDirectory(Path.Combine(_tempRoot, "mapped")).FullName;
        File.WriteAllText(Path.Combine(orphanFolder, "note.txt"), "orphan");

        _settingsService.Settings.Widgets.Add(CreateManagedWidget("Active", activeFolder));
        _settingsService.Settings.Widgets.Add(new WidgetConfig
        {
            Name = "Mapped",
            MappedFolderPath = mappedFolderOutsideRoot,
            FollowsDefaultStoragePath = false
        });

        var candidates = _widgetManager.GetOrphanManagedStorageFolders();

        var candidate = Assert.Single(candidates);
        Assert.Equal("Orphan", candidate.Name);
        Assert.Equal(orphanFolder, candidate.Path);
        Assert.Equal(1, candidate.ItemCount);
    }

    [Fact]
    public async Task MoveOrphanManagedStorageFolderContentsToDesktopAsync_MovesContentsAndDeletesEmptyFolder()
    {
        string orphanFolder = Directory.CreateDirectory(Path.Combine(_storageRoot, "Orphan")).FullName;
        string sourcePath = Path.Combine(orphanFolder, "note.txt");
        string existingDesktopPath = Path.Combine(_desktopRoot, "note.txt");
        File.WriteAllText(sourcePath, "orphan");
        File.WriteAllText(existingDesktopPath, "existing");

        await _widgetManager.MoveOrphanManagedStorageFolderContentsToDesktopAsync(orphanFolder);

        Assert.False(Directory.Exists(orphanFolder));
        Assert.Equal("existing", File.ReadAllText(existingDesktopPath));
        Assert.Equal("orphan", File.ReadAllText(Path.Combine(_desktopRoot, "note (2).txt")));
    }

    [Fact]
    public async Task MoveOrphanManagedStorageFolderContentsToDesktopAsync_RejectsActiveManagedFolder()
    {
        string activeFolder = Directory.CreateDirectory(Path.Combine(_storageRoot, "Active")).FullName;
        string activeFile = Path.Combine(activeFolder, "note.txt");
        File.WriteAllText(activeFile, "active");
        _settingsService.Settings.Widgets.Add(CreateManagedWidget("Active", activeFolder));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _widgetManager.MoveOrphanManagedStorageFolderContentsToDesktopAsync(activeFolder));

        Assert.True(Directory.Exists(activeFolder));
        Assert.Equal("active", File.ReadAllText(activeFile));
    }

    [Fact]
    public async Task DeleteOrphanManagedStorageFolderAsync_DeletesValidatedOrphanFolder()
    {
        string orphanFolder = Directory.CreateDirectory(Path.Combine(_storageRoot, "Orphan")).FullName;
        File.WriteAllText(Path.Combine(orphanFolder, "note.txt"), "orphan");

        await _widgetManager.DeleteOrphanManagedStorageFolderAsync(orphanFolder);

        Assert.False(Directory.Exists(orphanFolder));
    }

    [Fact]
    public async Task RestoreOrphanManagedStorageFoldersAsync_CreatesManagedWidgetsForExistingFolders()
    {
        string orphanFolder = Directory.CreateDirectory(Path.Combine(_storageRoot, "Orphan")).FullName;
        File.WriteAllText(Path.Combine(orphanFolder, "note.txt"), "orphan");

        int restored = await _widgetManager.RestoreOrphanManagedStorageFoldersAsync([orphanFolder]);

        Assert.Equal(1, restored);
        var widget = Assert.Single(_settingsService.Settings.Widgets);
        Assert.Equal("Orphan", widget.Name);
        Assert.Equal(WidgetKind.File, widget.WidgetKind);
        Assert.True(widget.FollowsDefaultStoragePath);
        Assert.Equal("Orphan", widget.ManagedFolderName);
        Assert.Equal(orphanFolder, widget.MappedFolderPath);
        Assert.True(widget.IsVisible);
        Assert.False(widget.IsDisabled);
        Assert.True(Directory.Exists(orphanFolder));
        Assert.Empty(_widgetManager.GetOrphanManagedStorageFolders());
    }

    [Fact]
    public void RepairLegacyContentFeatureFileShells_RemovesOnlyEmptyMusicFileShells()
    {
        var musicConfig = new WidgetConfig
        {
            Id = "music-real",
            Name = "Music",
            WidgetKind = WidgetKind.Music,
            IsVisible = true
        };
        var legacyShell = new WidgetConfig
        {
            Id = "music-shell",
            Name = "\u97F3\u4E50",
            WidgetKind = WidgetKind.File,
            IsVisible = true
        };
        var userFileWidget = new WidgetConfig
        {
            Id = "music-user-file",
            Name = "Music",
            WidgetKind = WidgetKind.File,
            MappedFolderPath = _desktopRoot,
            IsVisible = true
        };
        FeatureWidgetSettings.SetEnabled(_settingsService.Settings, WidgetKind.Music, true);
        _settingsService.Settings.Widgets.Add(musicConfig);
        _settingsService.Settings.Widgets.Add(legacyShell);
        _settingsService.Settings.Widgets.Add(userFileWidget);

        int repaired = _widgetManager.RepairLegacyContentFeatureFileShells();

        Assert.Equal(1, repaired);
        Assert.DoesNotContain(_settingsService.Settings.Widgets, widget => widget.Id == legacyShell.Id);
        Assert.Contains(_settingsService.Settings.Widgets, widget => widget.Id == musicConfig.Id);
        Assert.Contains(_settingsService.Settings.Widgets, widget => widget.Id == userFileWidget.Id);
        Assert.Contains(legacyShell.Id, _settingsService.Settings.DeletedWidgetIds);
    }

    [Fact]
    public async Task CreateWidgetFromConfigAsync_RejectsContentFeatureConfigBeforeMutatingKind()
    {
        var musicConfig = new WidgetConfig
        {
            Id = "music-window",
            Name = "Music",
            WidgetKind = WidgetKind.Music
        };
        var method = typeof(WidgetManager).GetMethod(
            "CreateWidgetFromConfigAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            _widgetManager,
            [musicConfig, false, false, false]));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await task);
        Assert.Contains("File config", exception.Message);
        Assert.Equal(WidgetKind.Music, musicConfig.WidgetKind);
    }

    [Fact]
    public async Task RemoveWidgetAsync_MoveManagedFolderContentsToDesktop_RemovesConfigAndMovesFiles()
    {
        string managedFolder = Directory.CreateDirectory(Path.Combine(_storageRoot, "Managed")).FullName;
        string sourcePath = Path.Combine(managedFolder, "note.txt");
        File.WriteAllText(sourcePath, "managed");
        var widget = CreateManagedWidget("Managed", managedFolder);
        _settingsService.Settings.Widgets.Add(widget);

        await _widgetManager.RemoveWidgetAsync(widget.Id, WidgetRemovalAction.MoveManagedFolderContentsToDesktop);

        Assert.DoesNotContain(_settingsService.Settings.Widgets, item => item.Id == widget.Id);
        Assert.Contains(widget.Id, _settingsService.Settings.DeletedWidgetIds);
        Assert.False(Directory.Exists(managedFolder));
        Assert.Equal("managed", File.ReadAllText(Path.Combine(_desktopRoot, "note.txt")));
    }

    [Fact]
    public async Task RemoveWidgetAsync_MissingManagedFolder_RemovesConfigWithoutCreatingFolder()
    {
        string missingFolder = Path.Combine(_storageRoot, "Missing");
        var widget = CreateManagedWidget("Missing", missingFolder);
        _settingsService.Settings.Widgets.Add(widget);

        Assert.False(_widgetManager.CanCleanupManagedStorageForWidget(widget.Id));

        await _widgetManager.RemoveWidgetAsync(widget.Id, WidgetRemovalAction.DeleteManagedFolder);

        Assert.DoesNotContain(_settingsService.Settings.Widgets, item => item.Id == widget.Id);
        Assert.Contains(widget.Id, _settingsService.Settings.DeletedWidgetIds);
        Assert.False(Directory.Exists(missingFolder));
    }

    [Fact]
    public async Task RemoveWidgetAsync_InvalidManagedCleanup_RemovesConfigAndKeepsFolder()
    {
        string mappedFolder = Directory.CreateDirectory(Path.Combine(_tempRoot, "mapped")).FullName;
        var widget = new WidgetConfig
        {
            Name = "Mapped",
            WidgetKind = WidgetKind.File,
            MappedFolderPath = mappedFolder,
            FollowsDefaultStoragePath = false
        };
        _settingsService.Settings.Widgets.Add(widget);

        await _widgetManager.RemoveWidgetAsync(widget.Id, WidgetRemovalAction.DeleteManagedFolder);

        Assert.DoesNotContain(_settingsService.Settings.Widgets, item => item.Id == widget.Id);
        Assert.Contains(widget.Id, _settingsService.Settings.DeletedWidgetIds);
        Assert.True(Directory.Exists(mappedFolder));
    }

    [Fact]
    public async Task RemoveWidgetAsync_MappedFolderCleanupRequest_RemovesConfigAndKeepsFolder()
    {
        string mappedFolder = Directory.CreateDirectory(Path.Combine(_tempRoot, "mapped")).FullName;
        string mappedFile = Path.Combine(mappedFolder, "note.txt");
        File.WriteAllText(mappedFile, "mapped");
        var widget = new WidgetConfig
        {
            Name = "Mapped",
            MappedFolderPath = mappedFolder,
            FollowsDefaultStoragePath = false
        };
        _settingsService.Settings.Widgets.Add(widget);

        await _widgetManager.RemoveWidgetAsync(widget.Id, WidgetRemovalAction.DeleteManagedFolder);

        Assert.DoesNotContain(_settingsService.Settings.Widgets, item => item.Id == widget.Id);
        Assert.Contains(widget.Id, _settingsService.Settings.DeletedWidgetIds);
        Assert.True(Directory.Exists(mappedFolder));
        Assert.Equal("mapped", File.ReadAllText(mappedFile));
    }

    [Fact]
    public async Task RenameWidgetAsync_ManagedWidgetRejectsDuplicateNameWithoutCreatingFolder()
    {
        string existingFolder = Directory.CreateDirectory(Path.Combine(_storageRoot, "AI")).FullName;
        string targetFolder = Directory.CreateDirectory(Path.Combine(_storageRoot, "Work")).FullName;
        var existingWidget = CreateManagedWidget("AI", existingFolder);
        var targetWidget = CreateManagedWidget("Work", targetFolder);
        _settingsService.Settings.Widgets.Add(existingWidget);
        _settingsService.Settings.Widgets.Add(targetWidget);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _widgetManager.RenameWidgetAsync(targetWidget.Id, "AI"));

        Assert.Equal("Work", targetWidget.Name);
        Assert.Equal("Work", targetWidget.ManagedFolderName);
        Assert.Equal(targetFolder, targetWidget.MappedFolderPath);
        Assert.True(Directory.Exists(targetFolder));
        Assert.False(Directory.Exists(Path.Combine(_storageRoot, "AI (2)")));
    }

    [Fact]
    public async Task RenameWidgetAsync_ManagedWidgetMovesFolderAfterValidation()
    {
        string sourceFolder = Directory.CreateDirectory(Path.Combine(_storageRoot, "Work")).FullName;
        File.WriteAllText(Path.Combine(sourceFolder, "note.txt"), "content");
        var widget = CreateManagedWidget("Work", sourceFolder);
        _settingsService.Settings.Widgets.Add(widget);

        await _widgetManager.RenameWidgetAsync(widget.Id, "AI");

        string destinationFolder = Path.Combine(_storageRoot, "AI");
        Assert.Equal("AI", widget.Name);
        Assert.Equal("AI", widget.ManagedFolderName);
        Assert.Equal(destinationFolder, widget.MappedFolderPath);
        Assert.False(Directory.Exists(sourceFolder));
        Assert.Equal("content", File.ReadAllText(Path.Combine(destinationFolder, "note.txt")));
    }

    [Fact]
    public async Task RenameWidgetAsync_ConcurrentManagedRenamesDoNotCreateDuplicateFolders()
    {
        string sourceFolder = Directory.CreateDirectory(Path.Combine(_storageRoot, "Work")).FullName;
        var widget = CreateManagedWidget("Work", sourceFolder);
        _settingsService.Settings.Widgets.Add(widget);

        var firstRename = _widgetManager.RenameWidgetAsync(widget.Id, "AI");
        var secondRename = _widgetManager.RenameWidgetAsync(widget.Id, "AI");
        await Task.WhenAll(firstRename, secondRename);

        string destinationFolder = Path.Combine(_storageRoot, "AI");
        Assert.Equal("AI", widget.Name);
        Assert.Equal("AI", widget.ManagedFolderName);
        Assert.Equal(destinationFolder, widget.MappedFolderPath);
        Assert.True(Directory.Exists(destinationFolder));
        Assert.False(Directory.Exists(Path.Combine(_storageRoot, "AI (2)")));
    }

    [Fact]
    public async Task RenameWidgetAsync_ConcurrentManagedRenamesRespectDuplicateNameGuard()
    {
        string existingFolder = Directory.CreateDirectory(Path.Combine(_storageRoot, "AI")).FullName;
        string targetFolder = Directory.CreateDirectory(Path.Combine(_storageRoot, "Work")).FullName;
        var existingWidget = CreateManagedWidget("AI", existingFolder);
        var targetWidget = CreateManagedWidget("Work", targetFolder);
        _settingsService.Settings.Widgets.Add(existingWidget);
        _settingsService.Settings.Widgets.Add(targetWidget);

        var firstRename = _widgetManager.RenameWidgetAsync(targetWidget.Id, "AI");
        var secondRename = _widgetManager.RenameWidgetAsync(targetWidget.Id, "AI");
        await Assert.ThrowsAnyAsync<InvalidOperationException>(async () =>
            await Task.WhenAll(firstRename, secondRename));

        Assert.Equal("Work", targetWidget.Name);
        Assert.Equal("Work", targetWidget.ManagedFolderName);
        Assert.Equal(targetFolder, targetWidget.MappedFolderPath);
        Assert.False(Directory.Exists(Path.Combine(_storageRoot, "AI (2)")));
    }

    [Fact]
    public async Task RenameWidgetAsync_ManagedWidgetMissingSourceDoesNotCreateTargetFolder()
    {
        string missingFolder = Path.Combine(_storageRoot, "Missing");
        var widget = CreateManagedWidget("Missing", missingFolder);
        _settingsService.Settings.Widgets.Add(widget);

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            _widgetManager.RenameWidgetAsync(widget.Id, "AI"));

        Assert.Equal("Missing", widget.Name);
        Assert.Equal("Missing", widget.ManagedFolderName);
        Assert.Equal(missingFolder, widget.MappedFolderPath);
        Assert.False(Directory.Exists(Path.Combine(_storageRoot, "AI")));
    }

    private static WidgetConfig CreateManagedWidget(string name, string folderPath)
    {
        return new WidgetConfig
        {
            Name = name,
            WidgetKind = WidgetKind.File,
            MappedFolderPath = folderPath,
            FollowsDefaultStoragePath = true,
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
