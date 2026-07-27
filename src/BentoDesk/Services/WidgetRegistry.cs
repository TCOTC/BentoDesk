using BentoDesk.Models;
using BentoDesk.Services.WidgetKinds;

namespace BentoDesk.Services;

public sealed record WidgetKindRegistration(
    WidgetKind WidgetKind,
    bool CanCreateWindow,
    bool IsImplemented);

/// <summary>
/// Central registry for widget kinds known to BentoDesk.
/// </summary>
public sealed class WidgetRegistry
{
    private readonly IReadOnlyDictionary<WidgetKind, WidgetKindRegistration> _registrations;

    public static WidgetRegistry Default { get; } = CreateDefault();

    public WidgetRegistry(IEnumerable<WidgetKindRegistration> registrations)
    {
        _registrations = registrations.ToDictionary(registration => registration.WidgetKind);
    }

    public bool IsKnown(WidgetKind widgetKind)
    {
        return _registrations.ContainsKey(widgetKind);
    }

    public bool CanCreateWindow(WidgetKind widgetKind)
    {
        return _registrations.TryGetValue(widgetKind, out var registration) &&
               registration.CanCreateWindow;
    }

    public bool IsImplemented(WidgetKind widgetKind)
    {
        return _registrations.TryGetValue(widgetKind, out var registration) &&
               registration.IsImplemented;
    }

    public bool IsAvailableForSession(WidgetConfig widget, AppSettings settings)
    {
        if (!CanCreateWindow(widget.WidgetKind))
        {
            return false;
        }

        if (WidgetKindHandlerRegistry.Default.TryGet(widget.WidgetKind, out var handler))
        {
            return handler.IsAvailableForSession(settings);
        }

        return true;
    }

    private static WidgetRegistry CreateDefault()
    {
        return new WidgetRegistry(
        [
            new(WidgetKind.File, CanCreateWindow: true, IsImplemented: true),
            new(WidgetKind.Music, CanCreateWindow: true, IsImplemented: true)
        ]);
    }
}
