// Copyright (c) BentoDesk. All rights reserved.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace BentoDesk.Helpers;

/// <summary>
/// Applies high-frequency item-surface opacity through Composition visuals so
/// selection/cut updates avoid XAML Opacity dependency-property churn.
/// Layout metrics still live on XAML Borders; SizeChanged no longer rebuilds them.
/// </summary>
internal static class WidgetItemSurfaceCompositionHelper
{
    public static void ApplyOpacity(UIElement element, double opacity)
    {
        ArgumentNullException.ThrowIfNull(element);

        float value = (float)Math.Clamp(opacity, 0, 1);
        var visual = ElementCompositionPreview.GetElementVisual(element);
        if (Math.Abs(visual.Opacity - value) < 0.001f)
        {
            return;
        }

        visual.StopAnimation("Opacity");
        // Prefer Composition opacity so high-frequency state changes do not
        // cascade through the XAML Opacity DP. Keep XAML Opacity at 1.
        if (Math.Abs(element.Opacity - 1.0) > 0.001)
        {
            element.Opacity = 1.0;
        }

        visual.Opacity = value;
    }
}
