using BentoDesk.Contracts;
using BentoDesk.Controls.WidgetContents;
using BentoDesk.Models;

namespace BentoDesk.Services;

internal sealed class MusicWidgetContentProvider : IWidgetContentProvider
{
    public WidgetKind WidgetKind => WidgetKind.Music;

    public bool CanCreateDetachedContent => true;

    public IWidgetContent CreateDetachedContent(WidgetConfig config, WidgetContentProviderContext context)
    {
        if (config.WidgetKind != WidgetKind)
        {
            throw new ArgumentException("Music content requires a Music widget config.", nameof(config));
        }

        return new MusicWidgetContentAdapter(
            config,
            context.LocalizationService,
            context.SettingsService);
    }
}
