using BentoDesk.Helpers;
using Microsoft.UI.Dispatching;
using Windows.Foundation;

namespace BentoDesk.Services;

/// <summary>
/// Single source of truth for whether Explorer's native desktop ListView should stay hidden.
/// Coordinates painted-desktop mode, double-click clear, Explorer restart re-hide, and Guard.
/// </summary>
public sealed class DesktopShellIconService : IDisposable
{
    [Flags]
    public enum HideReason
    {
        None = 0,
        PaintedDesktop = 1,
        DoubleClickClear = 2
    }

    private readonly object _lock = new();
    private readonly DispatcherQueueTimer? _syncTimer;
    private readonly TypedEventHandler<DispatcherQueueTimer, object> _syncTick;
    private HideReason _activeReasons;
    private IntPtr _hiddenListViewHwnd;
    private bool _disposed;

    public DesktopShellIconService(DispatcherQueue? dispatcherQueue = null)
    {
        _syncTick = (_, _) => SyncShellIconState();
        if (dispatcherQueue is not null)
        {
            _syncTimer = dispatcherQueue.CreateTimer();
            _syncTimer.Interval = TimeSpan.FromSeconds(2);
            _syncTimer.IsRepeating = true;
            _syncTimer.Tick += _syncTick;
        }
    }

    public bool IsPaintedDesktopActive
    {
        get
        {
            lock (_lock)
            {
                return (_activeReasons & HideReason.PaintedDesktop) != 0;
            }
        }
    }

    public bool ExpectedNativeIconsHidden
    {
        get
        {
            lock (_lock)
            {
                return _activeReasons != HideReason.None;
            }
        }
    }

    public event Action? PaintedDesktopChanged;

    public void RequestHidden(HideReason reason)
    {
        if (reason == HideReason.None || _disposed)
        {
            return;
        }

        bool becamePainted;
        lock (_lock)
        {
            bool wasPainted = (_activeReasons & HideReason.PaintedDesktop) != 0;
            _activeReasons |= reason;
            becamePainted = !wasPainted && (_activeReasons & HideReason.PaintedDesktop) != 0;
        }

        ApplyExpectedVisibility();
        EnsureSyncTimer();
        if (becamePainted)
        {
            PaintedDesktopChanged?.Invoke();
        }
    }

    public void ReleaseHidden(HideReason reason)
    {
        if (reason == HideReason.None || _disposed)
        {
            return;
        }

        bool leftPainted;
        lock (_lock)
        {
            bool wasPainted = (_activeReasons & HideReason.PaintedDesktop) != 0;
            _activeReasons &= ~reason;
            leftPainted = wasPainted && (_activeReasons & HideReason.PaintedDesktop) == 0;
            if (_activeReasons == HideReason.None)
            {
                _hiddenListViewHwnd = IntPtr.Zero;
            }
        }

        ApplyExpectedVisibility();
        EnsureSyncTimer();
        if (leftPainted)
        {
            PaintedDesktopChanged?.Invoke();
        }
    }

    public bool SetNativeIconsVisible(bool visible)
    {
        bool ok = WidgetLayerService.SetDesktopIconsVisible(visible);
        if (ok && !visible)
        {
            lock (_lock)
            {
                _hiddenListViewHwnd = WidgetLayerService.FindDesktopIconListView();
            }
        }

        return ok;
    }

    public bool AreNativeIconsVisible() => WidgetLayerService.AreDesktopIconsVisible();

    /// <summary>
    /// Explorer restart recreates a visible ListView; re-apply hide when we still expect it.
    /// </summary>
    public void SyncShellIconState()
    {
        if (_disposed)
        {
            return;
        }

        bool expectHidden;
        IntPtr previousHwnd;
        lock (_lock)
        {
            expectHidden = _activeReasons != HideReason.None;
            previousHwnd = _hiddenListViewHwnd;
        }

        if (!expectHidden)
        {
            return;
        }

        bool hiddenWindowGone =
            previousHwnd != IntPtr.Zero &&
            !Win32Helper.IsWindow(previousHwnd);
        if (hiddenWindowGone)
        {
            WidgetLayerService.InvalidateDesktopIconViewCache();
        }

        bool iconsVisible = WidgetLayerService.AreDesktopIconsVisible();
        if (!hiddenWindowGone && !iconsVisible)
        {
            return;
        }

        App.Log(
            $"[DesktopShellIcons] Re-hiding native icons after shell change " +
            $"hiddenWindowGone={hiddenWindowGone} iconsVisible={iconsVisible}");
        ApplyExpectedVisibility();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_syncTimer is not null)
        {
            _syncTimer.Stop();
            _syncTimer.Tick -= _syncTick;
        }

        lock (_lock)
        {
            _activeReasons = HideReason.None;
            _hiddenListViewHwnd = IntPtr.Zero;
        }

        try
        {
            WidgetLayerService.SetDesktopIconsVisible(true);
        }
        catch (Exception ex)
        {
            App.Log($"[DesktopShellIcons] Dispose restore failed: {ex}");
        }
    }

    private void ApplyExpectedVisibility()
    {
        bool expectHidden;
        lock (_lock)
        {
            expectHidden = _activeReasons != HideReason.None;
        }

        if (expectHidden)
        {
            if (WidgetLayerService.SetDesktopIconsVisible(false))
            {
                lock (_lock)
                {
                    _hiddenListViewHwnd = WidgetLayerService.FindDesktopIconListView();
                }
            }
        }
        else
        {
            WidgetLayerService.SetDesktopIconsVisible(true);
            lock (_lock)
            {
                _hiddenListViewHwnd = IntPtr.Zero;
            }
        }
    }

    private void EnsureSyncTimer()
    {
        if (_syncTimer is null)
        {
            return;
        }

        bool expectHidden;
        lock (_lock)
        {
            expectHidden = _activeReasons != HideReason.None;
        }

        if (expectHidden)
        {
            if (!_syncTimer.IsRunning)
            {
                _syncTimer.Start();
            }
        }
        else if (_syncTimer.IsRunning)
        {
            _syncTimer.Stop();
        }
    }
}
