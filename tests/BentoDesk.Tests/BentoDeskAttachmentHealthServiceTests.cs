using BentoDesk.Models;
using BentoDesk.Services;

namespace BentoDesk.Tests;

public sealed class BentoDeskAttachmentHealthServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _dataRoot;

    public BentoDeskAttachmentHealthServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "BentoDesk.Tests", Guid.NewGuid().ToString("N"));
        _dataRoot = Directory.CreateDirectory(Path.Combine(_tempRoot, "data")).FullName;
    }

    [Fact]
    public async Task ScanAsync_ReportsMissingAndOrphanedAttachments()
    {
        string widgetsRoot = Directory.CreateDirectory(Path.Combine(_dataRoot, "widgets")).FullName;
        string managedRoot = Directory.CreateDirectory(
            Path.Combine(widgetsRoot, "todo-widget", "attachments", "task")).FullName;
        string managedExisting = Path.Combine(managedRoot, "existing.txt");
        string orphan = Path.Combine(managedRoot, "orphan.txt");
        string missingManaged = Path.Combine(managedRoot, "missing.txt");
        string missingLinked = Path.Combine(_tempRoot, "external-missing.txt");
        await File.WriteAllTextAsync(managedExisting, "referenced");
        await File.WriteAllTextAsync(orphan, "orphaned");
        var store = new TodoWidgetStore(widgetsRoot, "todo-widget");
        await store.SaveAsync(new TodoWidgetData
        {
            Items =
            [
                CreateItem(
                    new TodoAttachment
                    {
                        FilePath = managedExisting,
                        StorageMode = TodoAttachment.ManagedStorageMode
                    },
                    new TodoAttachment
                    {
                        FilePath = missingManaged,
                        StorageMode = TodoAttachment.ManagedStorageMode
                    },
                    new TodoAttachment
                    {
                        FilePath = missingLinked,
                        StorageMode = TodoAttachment.LinkedStorageMode
                    })
            ]
        });
        var service = new BentoDeskAttachmentHealthService(_dataRoot);

        BentoDeskAttachmentHealthReport report = await service.ScanAsync();

        Assert.Equal(3, report.ReferencedFileCount);
        Assert.Equal(missingLinked, Assert.Single(report.MissingLinkedFiles), ignoreCase: true);
        Assert.Equal(missingManaged, Assert.Single(report.MissingManagedFiles), ignoreCase: true);
        Assert.Equal(orphan, Assert.Single(report.OrphanManagedFiles), ignoreCase: true);
        Assert.Equal(0, report.UnreadableStoreCount);
        Assert.False(report.IsHealthy);
    }

    [Fact]
    public async Task ScanAsync_ReportsUnreadableTodoStoreWithoutStoppingOtherChecks()
    {
        string todoRoot = Directory.CreateDirectory(
            Path.Combine(_dataRoot, "widgets", "todo-widget")).FullName;
        await File.WriteAllTextAsync(Path.Combine(todoRoot, "todo.json"), "{ invalid json");
        var service = new BentoDeskAttachmentHealthService(_dataRoot);

        BentoDeskAttachmentHealthReport report = await service.ScanAsync();

        Assert.Equal(1, report.UnreadableStoreCount);
        Assert.False(report.IsHealthy);
    }

    private static TodoItem CreateItem(params TodoAttachment[] attachments)
    {
        return new TodoItem
        {
            Id = "task",
            Text = "Task with attachments",
            Attachments = attachments.ToList()
        };
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
        }
    }
}
