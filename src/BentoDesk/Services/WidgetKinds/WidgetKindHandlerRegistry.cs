using BentoDesk.Models;

namespace BentoDesk.Services.WidgetKinds;

internal sealed class WidgetKindHandlerRegistry
{
    private readonly IReadOnlyDictionary<WidgetKind, IWidgetKindHandler> _handlers;

    public static WidgetKindHandlerRegistry Default { get; } = CreateDefault();

    public WidgetKindHandlerRegistry(IEnumerable<IWidgetKindHandler> handlers)
    {
        _handlers = handlers.ToDictionary(handler => handler.WidgetKind);
    }

    public IReadOnlyList<IWidgetKindHandler> All => _handlers.Values.ToArray();

    public IWidgetKindHandler Get(WidgetKind kind)
    {
        if (_handlers.TryGetValue(kind, out var handler))
        {
            return handler;
        }

        throw new NotSupportedException($"Widget kind '{kind}' does not have a kind handler.");
    }

    public bool TryGet(WidgetKind kind, out IWidgetKindHandler handler)
    {
        return _handlers.TryGetValue(kind, out handler!);
    }

    private static WidgetKindHandlerRegistry CreateDefault()
    {
        return new WidgetKindHandlerRegistry(
        [
            FileWidgetKindHandler.Instance,
            MusicWidgetKindHandler.Instance
        ]);
    }
}
