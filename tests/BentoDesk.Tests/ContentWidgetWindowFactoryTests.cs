using BentoDesk.Controls.WidgetContents;
using BentoDesk.Models;
using BentoDesk.Services;

namespace BentoDesk.Tests;

public sealed class ContentWidgetWindowFactoryTests : IDisposable
{
    private readonly string _tempRoot;

    public ContentWidgetWindowFactoryTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "BentoDesk.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "widgets"));
    }

    [Fact]
    public void CreateContentWindowPlan_ReturnsMusicAdapterForCreatableMusicKind()
    {
        var config = CreateConfig("music-window", WidgetKind.Music);
        var factory = CreateFactory();

        var plan = factory.CreateContentWindowPlan(config);

        Assert.Equal(config, plan.Config);
        Assert.Equal(WidgetKind.Music, plan.Descriptor.WidgetKind);
        Assert.IsType<MusicWidgetContentAdapter>(plan.Content);
        Assert.True(factory.CanCreateContentWindow(WidgetKind.Music));
        Assert.True(WidgetRegistry.Default.CanCreateWindow(WidgetKind.Music));
    }

    [Fact]
    public void CreateContentWindowPlan_RejectsWindowOwnedKinds()
    {
        var config = CreateConfig("unsupported-window", WidgetKind.File);
        var factory = CreateFactory();

        Assert.False(factory.CanCreateContentWindow(WidgetKind.File));
        Assert.Throws<NotSupportedException>(() => factory.CreateContentWindowPlan(config));
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

    private ContentWidgetWindowFactory CreateFactory()
    {
        return new ContentWidgetWindowFactory(
            TestServices.CreateWidgetContentFactory(),
            new SettingsService());
    }

    private static WidgetConfig CreateConfig(string id, WidgetKind widgetKind)
    {
        return new WidgetConfig
        {
            Id = id,
            Name = widgetKind.ToString(),
            WidgetKind = widgetKind
        };
    }
}
