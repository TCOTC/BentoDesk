using BentoDesk.Contracts;
using BentoDesk.Controls.WidgetContents;
using BentoDesk.Models;

namespace BentoDesk.Services;

internal sealed class WeatherWidgetContentProvider : IWidgetContentProvider
{
    public WidgetKind WidgetKind => WidgetKind.Weather;

    public bool CanCreateDetachedContent => true;

    public IWidgetContent CreateDetachedContent(WidgetConfig config, WidgetContentProviderContext context)
    {
        if (config.WidgetKind != WidgetKind)
        {
            throw new ArgumentException("Weather content requires a Weather widget config.", nameof(config));
        }

        return new WeatherWidgetContentAdapter(
            config,
            context.LocalizationService,
            context.SettingsService);
    }
}
