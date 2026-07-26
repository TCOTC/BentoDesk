using BentoDesk.Contracts;
using BentoDesk.Models;
using BentoDesk.Views;

namespace BentoDesk.Services;

/// <summary>
/// Prepares lightweight content widget windows for non-file widget kinds.
/// </summary>
public sealed class ContentWidgetWindowFactory
{
    private readonly WidgetContentFactory _contentFactory;
    private readonly SettingsService _settingsService;
    private readonly Func<WidgetConfig, IWidgetContent, SettingsService, WidgetContentDescriptor, ContentWidgetWindow> _windowFactory;

    public ContentWidgetWindowFactory(
        WidgetContentFactory contentFactory,
        SettingsService settingsService,
        Func<WidgetConfig, IWidgetContent, SettingsService, WidgetContentDescriptor, ContentWidgetWindow>? windowFactory = null)
    {
        _contentFactory = contentFactory;
        _settingsService = settingsService;
        _windowFactory = windowFactory ?? ((config, content, settings, descriptor) =>
            new ContentWidgetWindow(config, content, settings, descriptor));
    }

    internal bool CanCreateContentWindow(WidgetKind widgetKind)
    {
        return _contentFactory.CanCreateDetachedContent(widgetKind);
    }

    internal ContentWidgetWindow CreateContentWindow(WidgetConfig config)
    {
        var plan = CreateContentWindowPlan(config);
        return _windowFactory(plan.Config, plan.Content, _settingsService, plan.Descriptor);
    }

    internal ContentWidgetWindowPlan CreateContentWindowPlan(WidgetConfig config)
    {
        if (!CanCreateContentWindow(config.WidgetKind))
        {
            throw new NotSupportedException(
                $"Widget kind '{config.WidgetKind}' does not support content windows.");
        }

        var descriptor = _contentFactory.GetDescriptor(config.WidgetKind);
        var content = _contentFactory.CreateDetachedContent(
            config,
            _settingsService);
        return new ContentWidgetWindowPlan(config, content, descriptor);
    }
}

internal sealed record ContentWidgetWindowPlan(
    WidgetConfig Config,
    IWidgetContent Content,
    WidgetContentDescriptor Descriptor);
