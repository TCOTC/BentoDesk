using BentoDesk.Helpers;
using System.Runtime.InteropServices;

namespace BentoDesk.Services;

/// <summary>
/// Centralizes desktop widget Z-order operations so future layer modes can be
/// implemented without duplicating Win32 calls across each widget window type.
/// </summary>
public static class WidgetLayerService
{
    private const uint SpawnWorkerWMessage = 0x052C;

    private static readonly object s_desktopLayerLock = new();
    private static readonly Dictionary<IntPtr, DesktopLayerAttachment> s_desktopLayerAttachments = [];
    private static IntPtr s_cachedDesktopIconView;

    public static void MoveToDesktopBottom(IntPtr windowHandle)
    {
        if (UsesDesktopPinnedMode() && TryAttachToDesktopIconLayer(windowHandle))
        {
            return;
        }

        DetachFromDesktopIconLayerIfNeeded(windowHandle);
        Win32Helper.ClearWindowTopMost(windowHandle);
        Win32Helper.SetWindowToBottom(windowHandle);
    }

    public static IntPtr ClearTopMostPreservingForeground(IntPtr windowHandle)
    {
        if (UsesDesktopPinnedMode())
        {
            if (!TryAttachToDesktopIconLayer(windowHandle))
            {
                MoveToDynamicDesktopBottom(windowHandle);
            }

            return Win32Helper.GetForegroundWindow();
        }

        IntPtr foreground = Win32Helper.GetForegroundWindow();
        DetachFromDesktopIconLayerIfNeeded(windowHandle);

        // Always clear TopMost and bring the foreground window to front.
        // Previously this was gated by `wasTopMost`, but raised widgets use
        // BringWindowTemporarilyToFront (TOPMOST→NOTOPMOST) so they are never
        // persistently TopMost by the time restore runs — the gate was always
        // false, causing a "silent restore" (state changed, visual didn't).
        Win32Helper.ClearWindowTopMost(windowHandle);

        if (foreground != IntPtr.Zero && foreground != windowHandle)
        {
            Win32Helper.BringWindowToFront(foreground);
        }

        return foreground;
    }

    public static void ClearTopMost(IntPtr windowHandle)
    {
        if (UsesDesktopPinnedMode())
        {
            if (!TryAttachToDesktopIconLayer(windowHandle))
            {
                MoveToDynamicDesktopBottom(windowHandle);
            }

            return;
        }

        DetachFromDesktopIconLayerIfNeeded(windowHandle);
        Win32Helper.ClearWindowTopMost(windowHandle);
    }

    public static void HoldTemporaryTopMost(IntPtr windowHandle)
    {
        if (UsesDesktopPinnedMode())
        {
            if (!TryAttachToDesktopIconLayer(windowHandle))
            {
                MoveToDynamicDesktopBottom(windowHandle);
            }

            return;
        }

        DetachFromDesktopIconLayerIfNeeded(windowHandle);
        Win32Helper.BringWindowTemporarilyToFront(windowHandle);
    }

    public static void BringToFront(IntPtr windowHandle)
    {
        if (UsesDesktopPinnedMode())
        {
            if (!TryAttachToDesktopIconLayer(windowHandle))
            {
                MoveToDynamicDesktopBottom(windowHandle);
            }

            return;
        }

        DetachFromDesktopIconLayerIfNeeded(windowHandle);
        Win32Helper.BringWindowToFront(windowHandle);
    }

    /// <summary>
    /// Raises one widget above its peers without activating it. In desktop-pinned
    /// mode the window remains attached to the desktop icon layer and only its
    /// sibling order changes.
    /// </summary>
    public static void BringAbovePeerWidgets(IntPtr windowHandle)
    {
        if (UsesDesktopPinnedMode())
        {
            if (TryAttachToDesktopIconLayer(windowHandle))
            {
                Win32Helper.SetWindowPos(
                    windowHandle,
                    Win32Helper.HWND_TOP,
                    0,
                    0,
                    0,
                    0,
                    Win32Helper.SWP_NOMOVE |
                        Win32Helper.SWP_NOSIZE |
                        Win32Helper.SWP_NOACTIVATE |
                        Win32Helper.SWP_SHOWWINDOW);
            }

            return;
        }

        DetachFromDesktopIconLayerIfNeeded(windowHandle);
        Win32Helper.BringWindowTemporarilyToFront(windowHandle);
    }

    public static void BringGroupTemporarilyToFront(
        IReadOnlyList<IntPtr> windowHandles,
        IntPtr activeWindowHandle)
    {
        if (UsesDesktopPinnedMode())
        {
            return;
        }

        var handles = windowHandles
            .Where(handle => handle != IntPtr.Zero && Win32Helper.IsWindow(handle))
            .Distinct()
            .ToList();
        if (handles.Count == 0)
        {
            return;
        }

        foreach (IntPtr handle in handles)
        {
            DetachFromDesktopIconLayerIfNeeded(handle);
            Win32Helper.SetWindowTopMost(handle);
        }

        foreach (IntPtr handle in handles.Where(handle => handle != activeWindowHandle))
        {
            Win32Helper.ClearWindowTopMost(handle);
        }

        IntPtr activeHandle = handles.Contains(activeWindowHandle)
            ? activeWindowHandle
            : handles[^1];
        Win32Helper.ClearWindowTopMost(activeHandle);
        Win32Helper.BringWindowToFront(activeHandle);
        Win32Helper.SetForegroundWindow(activeHandle);
    }

    public static void ReleaseWindow(IntPtr windowHandle)
    {
        DetachFromDesktopIconLayerIfNeeded(windowHandle);
    }

    public static void InvalidateDesktopIconViewCache()
    {
        lock (s_desktopLayerLock)
        {
            s_cachedDesktopIconView = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Finds the desktop icon ListView without sending Progman <c>0x052C</c>.
    /// Safe for hot paths such as mouse hooks.
    /// </summary>
    public static IntPtr FindDesktopIconListView()
    {
        IntPtr defView = FindExistingDesktopIconView();
        if (defView == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        return FindDesktopIconListViewChild(defView);
    }

    public static bool AreDesktopIconsVisible()
    {
        IntPtr listView = FindDesktopIconListView();
        return listView != IntPtr.Zero && Win32Helper.IsWindowVisible(listView);
    }

    public static bool SetDesktopIconsVisible(bool visible)
    {
        IntPtr listView = FindDesktopIconListView();
        if (listView == IntPtr.Zero || !Win32Helper.IsWindow(listView))
        {
            App.Log("[WidgetLayer] SetDesktopIconsVisible skipped: desktop list view not found");
            return false;
        }

        bool currentlyVisible = Win32Helper.IsWindowVisible(listView);
        if (currentlyVisible == visible)
        {
            return true;
        }

        // ShowWindow 的返回值表示“之前是否可见”，不能当作成功与否。
        _ = Win32Helper.ShowWindow(
            listView,
            visible ? Win32Helper.SW_SHOWNOACTIVATE : Win32Helper.SW_HIDE);
        bool nowVisible = Win32Helper.IsWindowVisible(listView);
        bool ok = nowVisible == visible;
        App.Log($"[WidgetLayer] SetDesktopIconsVisible visible={visible} ok={ok} hwnd=0x{listView.ToInt64():X}");
        return ok;
    }

    public static bool UsesDesktopPinnedMode()
    {
        var settings = App.Current?.SettingsService?.Settings;
        string mode = SettingsService.NormalizeWidgetLayerModeSetting(settings?.WidgetLayerMode);
        return string.Equals(mode, SettingsService.WidgetLayerModeDesktopPinned, StringComparison.Ordinal);
    }

    private static bool TryAttachToDesktopIconLayer(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero || !Win32Helper.IsWindow(windowHandle))
        {
            return false;
        }

        IntPtr desktopIconView = FindDesktopIconView();
        if (desktopIconView == IntPtr.Zero)
        {
            App.Log("[WidgetLayer] DesktopPinned attach skipped: desktop icon view not found");
            return false;
        }

        lock (s_desktopLayerLock)
        {
            if (!s_desktopLayerAttachments.ContainsKey(windowHandle))
            {
                s_desktopLayerAttachments[windowHandle] = new DesktopLayerAttachment(
                    Win32Helper.GetWindowLongPtr(windowHandle, Win32Helper.GWLP_HWNDPARENT));
            }

            if (Win32Helper.GetWindowLongPtr(windowHandle, Win32Helper.GWLP_HWNDPARENT) != desktopIconView)
            {
                Win32Helper.SetLastError(0);
                _ = Win32Helper.SetWindowLongPtr(
                    windowHandle,
                    Win32Helper.GWLP_HWNDPARENT,
                    desktopIconView);
            }

            IntPtr actualOwner = Win32Helper.GetWindowLongPtr(windowHandle, Win32Helper.GWLP_HWNDPARENT);
            if (actualOwner != desktopIconView)
            {
                int error = Marshal.GetLastWin32Error();
                App.Log($"[WidgetLayer] DesktopPinned owner attach failed hwnd=0x{windowHandle.ToInt64():X} defView=0x{desktopIconView.ToInt64():X} actual=0x{actualOwner.ToInt64():X} error={error}");
                RestoreOriginalOwner(windowHandle);
                s_cachedDesktopIconView = IntPtr.Zero;
                return false;
            }

            Win32Helper.ClearWindowTopMost(windowHandle);
            Win32Helper.SetWindowPos(
                windowHandle,
                Win32Helper.HWND_BOTTOM,
                0,
                0,
                0,
                0,
                Win32Helper.SWP_NOMOVE |
                Win32Helper.SWP_NOSIZE |
                Win32Helper.SWP_NOACTIVATE |
                Win32Helper.SWP_SHOWWINDOW);

            App.LogVerbose($"[WidgetLayer] DesktopPinned owner attached hwnd=0x{windowHandle.ToInt64():X} defView=0x{desktopIconView.ToInt64():X}");
            return true;
        }
    }

    private static void DetachFromDesktopIconLayerIfNeeded(IntPtr windowHandle)
    {
        lock (s_desktopLayerLock)
        {
            if (!s_desktopLayerAttachments.ContainsKey(windowHandle))
            {
                return;
            }

            RestoreOriginalOwner(windowHandle);
        }
    }

    private static void MoveToDynamicDesktopBottom(IntPtr windowHandle)
    {
        // Try to attach to desktop icon layer to prevent Win+D from hiding the window
        // while maintaining dynamic layer behavior (can be raised on interaction)
        if (TryAttachToDesktopIconLayer(windowHandle))
        {
            return;
        }

        // Fallback: detach and use NOTOPMOST
        DetachFromDesktopIconLayerIfNeeded(windowHandle);
        Win32Helper.ClearWindowTopMost(windowHandle);
        Win32Helper.SetWindowToBottom(windowHandle);
    }

    private static void RestoreOriginalOwner(IntPtr windowHandle)
    {
        if (!s_desktopLayerAttachments.TryGetValue(windowHandle, out var attachment))
        {
            return;
        }

        Win32Helper.SetLastError(0);
        _ = Win32Helper.SetWindowLongPtr(
            windowHandle,
            Win32Helper.GWLP_HWNDPARENT,
            attachment.OriginalOwner);
        Win32Helper.SetWindowPos(
            windowHandle,
            Win32Helper.HWND_NOTOPMOST,
            0,
            0,
            0,
            0,
            Win32Helper.SWP_NOMOVE |
                Win32Helper.SWP_NOSIZE |
                Win32Helper.SWP_NOACTIVATE);
        s_desktopLayerAttachments.Remove(windowHandle);
        App.LogVerbose($"[WidgetLayer] DesktopPinned owner detached hwnd=0x{windowHandle.ToInt64():X}");
    }

    private static IntPtr FindDesktopIconView()
    {
        IntPtr existingDefView = FindExistingDesktopIconView();
        if (existingDefView != IntPtr.Zero)
        {
            return existingDefView;
        }

        // No existing WorkerW found: send 0x052C to Progman to spawn one.
        // Only used by DesktopPinned attach paths — never from mouse-hook hot paths.
        IntPtr progman = Win32Helper.FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
        {
            _ = Win32Helper.SendMessageTimeout(
                progman,
                SpawnWorkerWMessage,
                UIntPtr.Zero,
                IntPtr.Zero,
                Win32Helper.SMTO_NORMAL,
                1000,
                out _);

            IntPtr progmanDefView = FindDesktopIconViewChild(progman);
            if (progmanDefView != IntPtr.Zero)
            {
                lock (s_desktopLayerLock)
                {
                    s_cachedDesktopIconView = progmanDefView;
                }

                return progmanDefView;
            }
        }

        // Last resort: enum again after spawning.
        IntPtr workerDefView = FindExistingDesktopIconView(forceRescan: true);
        return workerDefView;
    }

    /// <summary>
    /// Locates an already-existing <c>SHELLDLL_DefView</c> without sending <c>0x052C</c>.
    /// </summary>
    private static IntPtr FindExistingDesktopIconView(bool forceRescan = false)
    {
        if (!forceRescan &&
            s_cachedDesktopIconView != IntPtr.Zero &&
            Win32Helper.IsWindow(s_cachedDesktopIconView))
        {
            return s_cachedDesktopIconView;
        }

        // Prefer an existing WorkerW/Progman DefView. Avoid 0x052C, which can disrupt
        // DWM composition and cause the desktop wallpaper to disappear.
        IntPtr existingDefView = IntPtr.Zero;
        Win32Helper.EnumWindows((hWnd, _) =>
        {
            IntPtr defView = FindDesktopIconViewChild(hWnd);
            if (defView != IntPtr.Zero)
            {
                existingDefView = defView;
                return false; // stop enumeration
            }

            return true;
        }, IntPtr.Zero);

        lock (s_desktopLayerLock)
        {
            s_cachedDesktopIconView = existingDefView;
        }

        return existingDefView;
    }

    private static IntPtr FindDesktopIconViewChild(IntPtr windowHandle)
    {
        return Win32Helper.FindWindowEx(windowHandle, IntPtr.Zero, "SHELLDLL_DefView", null);
    }

    private static IntPtr FindDesktopIconListViewChild(IntPtr defView)
    {
        IntPtr listView = Win32Helper.FindWindowEx(defView, IntPtr.Zero, "SysListView32", "FolderView");
        if (listView != IntPtr.Zero)
        {
            return listView;
        }

        return Win32Helper.FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
    }

    private sealed record DesktopLayerAttachment(IntPtr OriginalOwner);
}
