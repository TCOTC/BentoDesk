using BentoDesk.Helpers;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace BentoDesk.Services;

/// <summary>
/// 桌面层盒子上的 WinUI Flyout 收不到落在 DefView / 桌面图标上的点击，
/// 因此用与托盘抬升相同的鼠标采样，在点到非本进程窗口时主动 Hide。
/// </summary>
internal static class WidgetFlyoutDesktopDismiss
{
    private static readonly object Gate = new();
    private static readonly HashSet<FlyoutBase> OpenFlyouts = [];
    private static DispatcherQueueTimer? _timer;
    private static bool _lastMouseButtonsDown;

    public static void Track(FlyoutBase flyout)
    {
        DispatcherQueue? dispatcher = App.UiDispatcherQueue;
        if (dispatcher is null)
        {
            return;
        }

        lock (Gate)
        {
            if (!OpenFlyouts.Add(flyout))
            {
                return;
            }

            flyout.Closed += OnFlyoutClosed;
            // 预充当前按键态，避免打开菜单时仍按住的鼠标被当成新的外侧点击。
            _lastMouseButtonsDown = Win32Helper.IsAnyMouseButtonDown();
            EnsureTimerLocked(dispatcher);
        }
    }

    private static void OnFlyoutClosed(object? sender, object e)
    {
        if (sender is not FlyoutBase flyout)
        {
            return;
        }

        flyout.Closed -= OnFlyoutClosed;
        Untrack(flyout);
    }

    private static void Untrack(FlyoutBase flyout)
    {
        lock (Gate)
        {
            OpenFlyouts.Remove(flyout);
            if (OpenFlyouts.Count == 0)
            {
                StopTimerLocked();
            }
        }
    }

    private static void EnsureTimerLocked(DispatcherQueue dispatcher)
    {
        _timer ??= dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(50);
        _timer.Tick -= Timer_Tick;
        _timer.Tick += Timer_Tick;
        if (!_timer.IsRunning)
        {
            _timer.Start();
        }
    }

    private static void StopTimerLocked()
    {
        if (_timer is null)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= Timer_Tick;
    }

    private static void Timer_Tick(DispatcherQueueTimer sender, object args)
    {
        FlyoutBase[]? toHide = null;
        lock (Gate)
        {
            if (OpenFlyouts.Count == 0)
            {
                StopTimerLocked();
                _lastMouseButtonsDown = false;
                return;
            }

            bool isDown = Win32Helper.IsAnyMouseButtonDown();
            if (isDown && !_lastMouseButtonsDown && ShouldDismissForCursor())
            {
                toHide = OpenFlyouts.ToArray();
            }

            _lastMouseButtonsDown = isDown;
        }

        if (toHide is null)
        {
            return;
        }

        foreach (FlyoutBase flyout in toHide)
        {
            try
            {
                flyout.Hide();
            }
            catch (Exception ex)
            {
                App.LogVerbose($"[FlyoutDismiss] Hide failed: {ex.Message}");
            }
        }
    }

    private static bool ShouldDismissForCursor()
    {
        if (!Win32Helper.GetCursorPos(out Win32Helper.POINT cursor))
        {
            return false;
        }

        IntPtr pointerWindow = Win32Helper.WindowFromPoint(cursor);
        // 点在本进程窗口（含 MenuFlyout 弹出层）上时交给 WinUI 自己处理。
        return !App.Current.IsBentoDeskWindow(pointerWindow);
    }
}
