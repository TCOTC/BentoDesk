using BentoDesk.Helpers;
using Windows.Graphics;

namespace BentoDesk.Tests;

public sealed class WidgetCompositionBoundsTransitionTests
{
    [Theory]
    [InlineData(0, 80, 42, 400, 300, false, 80f / 400f, 42f / 300f)]
    [InlineData(1, 80, 42, 400, 300, false, 1f, 1f)]
    [InlineData(0, 400, 300, 80, 42, true, 1f, 1f)]
    [InlineData(1, 400, 300, 80, 42, true, 80f / 400f, 42f / 300f)]
    public void ResolveScale_MatchesHostAndProgress(
        double progress,
        int fromWidth,
        int fromHeight,
        int toWidth,
        int toHeight,
        bool collapsing,
        float expectedScaleX,
        float expectedScaleY)
    {
        var scale = WidgetCompositionBoundsTransition.ResolveScale(
            new RectInt32(0, 0, fromWidth, fromHeight),
            new RectInt32(0, 0, toWidth, toHeight),
            collapsing,
            progress);

        Assert.Equal(expectedScaleX, scale.X, precision: 3);
        Assert.Equal(expectedScaleY, scale.Y, precision: 3);
    }
}
