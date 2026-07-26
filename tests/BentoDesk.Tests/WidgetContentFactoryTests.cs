using BentoDesk.Controls.WidgetContents;
using BentoDesk.Models;
using BentoDesk.Services;
namespace BentoDesk.Tests;

public sealed class WidgetContentFactoryTests
{
    [Theory]
    [InlineData(WidgetKind.File, "BentoDesk", WidgetContentStage.Implemented, true, WidgetContentAvailability.Available)]
    [InlineData(WidgetKind.Music, "Music", WidgetContentStage.Implemented, false, WidgetContentAvailability.Available)]
    public void GetDescriptor_ReturnsContentMetadata(
        WidgetKind widgetKind,
        string title,
        WidgetContentStage stage,
        bool canShowInCreateEntry,
        WidgetContentAvailability availability)
    {
        var factory = TestServices.CreateWidgetContentFactory();

        var descriptor = factory.GetDescriptor(widgetKind);

        Assert.Equal(widgetKind, descriptor.WidgetKind);
        Assert.Equal(title, descriptor.DefaultTitle);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.DefaultGlyph));
        Assert.Equal(stage, descriptor.ContentStage);
        Assert.Equal(canShowInCreateEntry, descriptor.CanShowInCreateEntry);
        Assert.Equal(availability, descriptor.Availability);
        Assert.StartsWith($"WidgetContent.{widgetKind}.", descriptor.StatusLabelKey);
        Assert.StartsWith($"WidgetContent.{widgetKind}.", descriptor.StatusDescriptionKey);
    }

    [Fact]
    public void GetDescriptors_ReturnsStableKnownContentKinds()
    {
        var factory = TestServices.CreateWidgetContentFactory();

        var descriptors = factory.GetDescriptors();

        Assert.Equal(
        [
            WidgetKind.File,
            WidgetKind.Music
        ], descriptors.Select(descriptor => descriptor.WidgetKind));
    }

    [Theory]
    [InlineData(WidgetKind.File, WidgetChromeCategory.Interactive, WidgetChromeMode.Standard)]
    [InlineData(WidgetKind.Music, WidgetChromeCategory.Display, WidgetChromeMode.Overlay)]
    public void GetDescriptor_ReturnsChromeDefaults(
        WidgetKind widgetKind,
        WidgetChromeCategory expectedCategory,
        WidgetChromeMode expectedDefaultMode)
    {
        var factory = TestServices.CreateWidgetContentFactory();

        var descriptor = factory.GetDescriptor(widgetKind);

        Assert.Equal(expectedCategory, descriptor.ChromeCategory);
        Assert.Equal(expectedDefaultMode, descriptor.DefaultChromeMode);
        Assert.True(descriptor.CanUseOverlayChrome);
        Assert.True(descriptor.CanHideChrome);
    }

    [Fact]
    public void GetCreateEntryDescriptors_OnlyReturnsCurrentlyCreatableContentEntries()
    {
        var factory = TestServices.CreateWidgetContentFactory();

        var descriptors = factory.GetCreateEntryDescriptors();

        Assert.Equal([WidgetKind.File], descriptors.Select(descriptor => descriptor.WidgetKind));
        Assert.All(descriptors, descriptor => Assert.True(WidgetRegistry.Default.CanCreateWindow(descriptor.WidgetKind)));
        Assert.All(descriptors, descriptor => Assert.False(string.IsNullOrWhiteSpace(descriptor.CreateEntryTextKey)));
        Assert.Equal("Common.NewWidget", descriptors.Single(descriptor => descriptor.WidgetKind == WidgetKind.File).CreateEntryTextKey);
    }

    [Fact]
    public void GetFeatureWidgetEntryDescriptors_OnlyReturnsAvailableImplementedWidgets()
    {
        var factory = TestServices.CreateWidgetContentFactory();

        var descriptors = factory.GetFeatureWidgetEntryDescriptors();

        Assert.Equal(
        [
            WidgetKind.Music
        ], descriptors.Select(descriptor => descriptor.WidgetKind));
        Assert.DoesNotContain(descriptors, descriptor => descriptor.WidgetKind == WidgetKind.File);
        Assert.DoesNotContain(descriptors, descriptor => descriptor.IsPlanned);
        Assert.Contains(descriptors, descriptor => descriptor.WidgetKind == WidgetKind.Music && descriptor.HasImplementedContent);
    }

    [Theory]
    [InlineData(WidgetKind.File, true, false, true, true, false)]
    [InlineData(WidgetKind.Music, true, false, false, true, false)]
    public void ContentCapabilityQueries_ReturnExpectedReadOnlyState(
        WidgetKind widgetKind,
        bool hasImplementedContent,
        bool isPlaceholderOnly,
        bool canShowInCreateEntry,
        bool isAvailable,
        bool isPlanned)
    {
        var factory = TestServices.CreateWidgetContentFactory();

        Assert.Equal(hasImplementedContent, factory.HasImplementedContent(widgetKind));
        Assert.Equal(isPlaceholderOnly, factory.IsPlaceholderOnly(widgetKind));
        Assert.Equal(canShowInCreateEntry, factory.CanShowInCreateEntry(widgetKind));
        Assert.Equal(isAvailable, factory.IsAvailable(widgetKind));
        Assert.Equal(isPlanned, factory.IsPlanned(widgetKind));
    }

    [Fact]
    public void StatusKeys_AreStableLocalizationKeys()
    {
        var factory = TestServices.CreateWidgetContentFactory();

        foreach (var descriptor in factory.GetDescriptors())
        {
            Assert.EndsWith(".StatusLabel", descriptor.StatusLabelKey);
            Assert.EndsWith(".StatusDescription", descriptor.StatusDescriptionKey);
            Assert.DoesNotContain(' ', descriptor.StatusLabelKey);
            Assert.DoesNotContain(' ', descriptor.StatusDescriptionKey);
        }
    }

    [Fact]
    public void CreateDetachedContent_ReturnsMusicAdapterForImplementedMusicKind()
    {
        var factory = TestServices.CreateWidgetContentFactory();
        var config = new WidgetConfig
        {
            Id = "music-detached",
            Name = "Music",
            WidgetKind = WidgetKind.Music
        };

        var content = factory.CreateDetachedContent(config);

        Assert.IsType<MusicWidgetContentAdapter>(content);
        Assert.Equal(WidgetKind.Music, content.WidgetKind);
        Assert.True(factory.CanCreateDetachedContent(WidgetKind.Music));
        Assert.False(factory.CanShowInCreateEntry(WidgetKind.Music));
        Assert.True(WidgetRegistry.Default.CanCreateWindow(WidgetKind.Music));
    }

    [Fact]
    public void CreateDetachedContent_RejectsWindowOwnedKinds()
    {
        var factory = TestServices.CreateWidgetContentFactory();
        var config = new WidgetConfig
        {
            WidgetKind = WidgetKind.File
        };

        Assert.False(factory.CanCreateDetachedContent(WidgetKind.File));
        Assert.Throws<NotSupportedException>(() => factory.CreateDetachedContent(config));
    }
}
