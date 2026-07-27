using BentoDesk.Contracts;
using BentoDesk.Controls.WidgetContents;
using BentoDesk.Models;
using Microsoft.UI.Xaml;

namespace BentoDesk.Services;

/// <summary>
/// Creates widget content views without owning host windows or z-order behavior.
/// </summary>
public sealed class WidgetContentFactory
{
    private readonly LocalizationService _localizationService;
    private readonly IReadOnlyDictionary<WidgetKind, IWidgetContentProvider> _contentProviders;

    public WidgetContentFactory(LocalizationService localizationService)
    {
        _localizationService = localizationService;
        _contentProviders = CreateContentProviders();
    }

    private static readonly IReadOnlyList<WidgetContentDescriptor> DescriptorList =
    [
        new(
            WidgetKind.File,
            "BentoDesk",
            "\uE8A5",
            WidgetContentStage.Implemented,
            CanShowInCreateEntry: true,
            WidgetContentAvailability.Available,
            "WidgetContent.File.StatusLabel",
            "WidgetContent.File.StatusDescription",
            "Common.NewWidget"),
        new(
            WidgetKind.Music,
            "Music",
            "\uEC4F",
            WidgetContentStage.Implemented,
            CanShowInCreateEntry: false,
            WidgetContentAvailability.Available,
            "WidgetContent.Music.StatusLabel",
            "WidgetContent.Music.StatusDescription",
            HasSettingsPage: true,
            SettingsSectionTag: "MusicSettings",
            DefaultChromeMode: WidgetChromeMode.Overlay)
    ];

    private static readonly IReadOnlyDictionary<WidgetKind, WidgetContentDescriptor> Descriptors =
        DescriptorList.ToDictionary(descriptor => descriptor.WidgetKind);

    public IWidgetContent CreateExistingContent(WidgetConfig config, FrameworkElement view)
    {
        return new ExistingWidgetContent(config, view);
    }

    /// <summary>
    /// Creates content that is not yet attached to a production widget window.
    /// </summary>
    internal IWidgetContent CreateDetachedContent(
        WidgetConfig config,
        SettingsService? settingsService = null)
    {
        if (!_contentProviders.TryGetValue(config.WidgetKind, out var provider) ||
            !provider.CanCreateDetachedContent)
        {
            throw new NotSupportedException(
                $"Widget kind '{config.WidgetKind}' does not have detached content.");
        }

        var context = new WidgetContentProviderContext(
            _localizationService,
            settingsService,
            GetDescriptor);
        return provider.CreateDetachedContent(config, context);
    }

    internal bool CanCreateDetachedContent(WidgetKind widgetKind)
    {
        return _contentProviders.TryGetValue(widgetKind, out var provider) &&
               provider.CanCreateDetachedContent;
    }

    public IReadOnlyList<WidgetContentDescriptor> GetDescriptors()
    {
        return DescriptorList;
    }

    public WidgetContentDescriptor GetDescriptor(WidgetKind widgetKind)
    {
        if (Descriptors.TryGetValue(widgetKind, out var descriptor))
        {
            return descriptor;
        }

        throw new NotSupportedException($"Widget kind '{widgetKind}' does not have a content descriptor.");
    }

    public IReadOnlyList<WidgetContentDescriptor> GetCreateEntryDescriptors()
    {
        return DescriptorList
            .Where(descriptor => descriptor.CanShowInCreateEntry)
            .ToArray();
    }

    public IReadOnlyList<WidgetContentDescriptor> GetFeatureWidgetEntryDescriptors()
    {
        return DescriptorList
            .Where(descriptor =>
                descriptor.WidgetKind != WidgetKind.File &&
                descriptor.HasImplementedContent &&
                descriptor.IsAvailable)
            .ToArray();
    }

    public bool HasImplementedContent(WidgetKind widgetKind)
    {
        return Descriptors.TryGetValue(widgetKind, out var descriptor) &&
               descriptor.HasImplementedContent;
    }

    public bool IsPlaceholderOnly(WidgetKind widgetKind)
    {
        return Descriptors.TryGetValue(widgetKind, out var descriptor) &&
               descriptor.IsPlaceholderOnly;
    }

    public bool CanShowInCreateEntry(WidgetKind widgetKind)
    {
        return Descriptors.TryGetValue(widgetKind, out var descriptor) &&
               descriptor.CanShowInCreateEntry;
    }

    public bool IsAvailable(WidgetKind widgetKind)
    {
        return Descriptors.TryGetValue(widgetKind, out var descriptor) &&
               descriptor.IsAvailable;
    }

    public bool IsPlanned(WidgetKind widgetKind)
    {
        return Descriptors.TryGetValue(widgetKind, out var descriptor) &&
               descriptor.IsPlanned;
    }

    private static IReadOnlyDictionary<WidgetKind, IWidgetContentProvider> CreateContentProviders()
    {
        IWidgetContentProvider[] providers =
        [
            new MusicWidgetContentProvider()
        ];

        return providers.ToDictionary(provider => provider.WidgetKind);
    }
}
