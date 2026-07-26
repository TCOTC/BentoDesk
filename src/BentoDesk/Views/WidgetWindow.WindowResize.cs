using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace BentoDesk.Views;

public sealed partial class WidgetWindow
{
    private bool _boundsTransitionLayoutSuspended;

    private void ResizeBorder_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.IsSizeLocked)
        {
            return;
        }

        ResizeBorder_PointerPressedCore(sender, e);
    }

    private void ResizeBorder_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        ResizeBorder_PointerMovedCore(sender, e);
    }

    private void ResizeBorder_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        ResizeBorder_PointerReleasedCore(sender, e);
    }

    private void ResizeBorder_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        ResizeBorder_PointerCaptureLostCore(sender, e);
    }

    private void ResizeBorder_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        var shape = element is FrameworkElement frameworkElement
            ? GetResizeCursorShapeForCurrentState(frameworkElement.Tag as string)
            : InputSystemCursorShape.Arrow;

        var property = typeof(UIElement).GetProperty(
            "ProtectedCursor",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        property?.SetValue(element, InputSystemCursor.Create(shape));
    }

    protected override void OnResizeStart()
    {
        BeginSuspendInteractiveSurfaceLayout();
        BeginInteractionLayer("file-resize-started");
    }

    protected override void OnResizeEnd()
    {
        ReleaseInteractionLayer("file-resize-ended");
        EndSuspendInteractiveSurfaceLayout();
    }

    protected override void OnBoundsTransitionStarted()
    {
        if (_boundsTransitionLayoutSuspended)
        {
            return;
        }

        _boundsTransitionLayoutSuspended = true;
        BeginSuspendInteractiveSurfaceLayout();
    }

    protected override void OnBoundsTransitionCompleted()
    {
        if (!_boundsTransitionLayoutSuspended)
        {
            return;
        }

        _boundsTransitionLayoutSuspended = false;
        EndSuspendInteractiveSurfaceLayout();
    }
}
