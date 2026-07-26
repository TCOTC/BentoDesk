// Copyright (c) BentoDesk. All rights reserved.

using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Windows.Graphics;

namespace BentoDesk.Helpers;

/// <summary>
/// Drives capsule fold/expand size illusion via Composition Scale around a
/// fixed pivot, so the host HWND geometry can stay fixed during the animation.
/// </summary>
internal static class WidgetCompositionBoundsTransition
{
    public static void Apply(
        UIElement root,
        RectInt32 from,
        RectInt32 to,
        bool collapsing,
        double progress,
        PointInt32 pivot,
        double dpiScale)
    {
        ArgumentNullException.ThrowIfNull(root);

        Vector2 scaleFactors = ResolveScale(from, to, collapsing, progress);
        RectInt32 host = collapsing ? from : to;
        double scale = Math.Max(0.01, dpiScale);
        float centerX = (float)((pivot.X - host.X) / scale);
        float centerY = (float)((pivot.Y - host.Y) / scale);

        var visual = ElementCompositionPreview.GetElementVisual(root);
        visual.CenterPoint = new Vector3(centerX, centerY, 0f);
        visual.Scale = new Vector3(scaleFactors.X, scaleFactors.Y, 1f);
    }

    public static Vector2 ResolveScale(
        RectInt32 from,
        RectInt32 to,
        bool collapsing,
        double progress)
    {
        if (collapsing)
        {
            float endX = (float)to.Width / Math.Max(1, from.Width);
            float endY = (float)to.Height / Math.Max(1, from.Height);
            return new Vector2(
                Lerp(1f, endX, (float)progress),
                Lerp(1f, endY, (float)progress));
        }

        float startX = (float)from.Width / Math.Max(1, to.Width);
        float startY = (float)from.Height / Math.Max(1, to.Height);
        return new Vector2(
            Lerp(startX, 1f, (float)progress),
            Lerp(startY, 1f, (float)progress));
    }

    public static void Reset(UIElement root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var visual = ElementCompositionPreview.GetElementVisual(root);
        visual.StopAnimation("Scale");
        visual.CenterPoint = Vector3.Zero;
        visual.Scale = Vector3.One;
    }

    private static float Lerp(float from, float to, float progress) =>
        from + ((to - from) * Math.Clamp(progress, 0f, 1f));
}
