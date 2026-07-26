using BentoDesk.Services;

namespace BentoDesk.Tests;

internal static class TestServices
{
    public static LocalizationService CreateLocalizationService()
    {
        return new LocalizationService();
    }

    public static WidgetContentFactory CreateWidgetContentFactory()
    {
        return new WidgetContentFactory(CreateLocalizationService());
    }
}
