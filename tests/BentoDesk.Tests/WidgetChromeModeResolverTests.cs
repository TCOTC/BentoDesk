using BentoDesk.Models;
using BentoDesk.Services;

namespace BentoDesk.Tests;

public sealed class WidgetChromeModeResolverTests
{
    [Fact]
    public void Resolve_UsesFileDescriptorStandardChrome()
    {
        var factory = TestServices.CreateWidgetContentFactory();
        var descriptor = factory.GetDescriptor(WidgetKind.File);
        var config = new WidgetConfig { WidgetKind = WidgetKind.File };

        var mode = new WidgetChromeModeResolver().Resolve(config, descriptor);

        Assert.Equal(WidgetChromeMode.Standard, mode);
    }

    [Fact]
    public void Resolve_UsesMusicDescriptorOverlayChrome()
    {
        var factory = TestServices.CreateWidgetContentFactory();
        var descriptor = factory.GetDescriptor(WidgetKind.Music);
        var config = new WidgetConfig { WidgetKind = WidgetKind.Music };

        var mode = new WidgetChromeModeResolver().Resolve(config, descriptor);

        Assert.Equal(WidgetChromeMode.Overlay, mode);
    }

    [Fact]
    public void Resolve_IgnoresLegacyChromeModeMetadata()
    {
        var factory = TestServices.CreateWidgetContentFactory();
        var descriptor = factory.GetDescriptor(WidgetKind.Music);
        var config = new WidgetConfig
        {
            WidgetKind = WidgetKind.Music,
            Metadata = new Dictionary<string, string>
            {
                [WidgetChromeModeNames.MetadataKey] = "Standard"
            }
        };

        var mode = new WidgetChromeModeResolver().Resolve(config, descriptor);

        Assert.Equal(WidgetChromeMode.Overlay, mode);
    }
}
