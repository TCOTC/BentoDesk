using Microsoft.UI.Xaml;

namespace BentoDesk.Services;

public static class WidgetActionIconHelper
{
    public static void ApplyLockState(
        FrameworkElement regularIcon,
        FrameworkElement filledIcon,
        bool isLocked)
    {
        ApplyPairState(regularIcon, filledIcon, isLocked);
    }

    public static void ApplyPairSize(FrameworkElement regularIcon, FrameworkElement filledIcon, WidgetTitleBarMetrics metrics)
    {
        WidgetTitleBarMetricsCalculator.ApplyActionIcon(regularIcon, metrics);
        WidgetTitleBarMetricsCalculator.ApplyActionIcon(filledIcon, metrics);
    }

    private static void ApplyPairState(FrameworkElement regularIcon, FrameworkElement filledIcon, bool isFilled)
    {
        regularIcon.Visibility = isFilled ? Visibility.Collapsed : Visibility.Visible;
        filledIcon.Visibility = isFilled ? Visibility.Visible : Visibility.Collapsed;
    }
}
