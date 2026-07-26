using System.Runtime.InteropServices;
using System.Text;
using BentoDesk.Helpers;

namespace BentoDesk.Services;

/// <summary>
/// Listens for double-clicks on empty desktop space and toggles all BentoDesk widgets.
/// When painted-desktop mode is active (SysListView32 already hidden), this service
/// never shows the native icon list — only widget visibility changes.
/// </summary>
public sealed class DesktopDoubleClickService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly DesktopShellIconService _shellIcons;
    private readonly Func<bool, Task> _setPaintedUiVisibleAsync;
    private readonly Win32Helper.LowLevelMouseProc _mouseHookProc;
    private IntPtr _mouseHookHandle;
    private bool _isCleared;
    private bool _isInvoking;
    private bool _hasPendingClick;
    private int _pendingClickX;
    private int _pendingClickY;
    private uint _pendingClickTime;

    public DesktopDoubleClickService(
        SettingsService settingsService,
        DesktopShellIconService shellIcons,
        Func<bool, Task> setPaintedUiVisibleAsync)
    {
        _settingsService = settingsService;
        _shellIcons = shellIcons;
        _setPaintedUiVisibleAsync = setPaintedUiVisibleAsync;
        _mouseHookProc = MouseHookProc;
    }

    public bool IsCleared => _isCleared;

    public void SetEnabled(bool enabled)
    {
        if (_settingsService.Settings.DoubleClickDesktopToHideAll != enabled)
        {
            _settingsService.Settings.DoubleClickDesktopToHideAll = enabled;
            _settingsService.SaveDebounced();
        }

        RefreshRegistration();
    }

    public void RefreshRegistration()
    {
        if (_settingsService.Settings.DoubleClickDesktopToHideAll)
        {
            InstallMouseHook();
            return;
        }

        UninstallMouseHook();
        _ = RestoreIfNeededAsync();
    }

    public async Task RestoreIfNeededAsync()
    {
        if (!_isCleared)
        {
            _shellIcons.ReleaseHidden(DesktopShellIconService.HideReason.DoubleClickClear);
            return;
        }

        try
        {
            _shellIcons.ReleaseHidden(DesktopShellIconService.HideReason.DoubleClickClear);
            await _setPaintedUiVisibleAsync(true);
            _isCleared = false;
        }
        catch (Exception ex)
        {
            App.Log($"[DesktopDoubleClick] RestoreIfNeeded failed: {ex}");
        }
    }

    public void Dispose()
    {
        UninstallMouseHook();
        _shellIcons.ReleaseHidden(DesktopShellIconService.HideReason.DoubleClickClear);
        _isCleared = false;
    }

    private void InstallMouseHook()
    {
        if (_mouseHookHandle != IntPtr.Zero)
        {
            return;
        }

        _mouseHookHandle = Win32Helper.SetWindowsMouseHookEx(
            Win32Helper.WH_MOUSE_LL,
            _mouseHookProc,
            Win32Helper.GetModuleHandle(null),
            0);
        if (_mouseHookHandle == IntPtr.Zero)
        {
            App.Log($"[DesktopDoubleClick] Failed to install mouse hook error={Marshal.GetLastWin32Error()}");
            return;
        }

        App.Log("[DesktopDoubleClick] Low-level mouse hook installed");
    }

    private void UninstallMouseHook()
    {
        if (_mouseHookHandle == IntPtr.Zero)
        {
            return;
        }

        Win32Helper.UnhookWindowsHookEx(_mouseHookHandle);
        _mouseHookHandle = IntPtr.Zero;
        _hasPendingClick = false;
        App.Log("[DesktopDoubleClick] Low-level mouse hook removed");
    }

    private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 ||
            !_settingsService.Settings.DoubleClickDesktopToHideAll ||
            wParam != Win32Helper.WM_LBUTTONUP)
        {
            return Win32Helper.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        try
        {
            var data = Marshal.PtrToStructure<Win32Helper.MSLLHOOKSTRUCT>(lParam);
            if (IsModifierPressed() || !IsEmptyDesktopPoint(data.pt))
            {
                _hasPendingClick = false;
                return Win32Helper.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
            }

            if (IsDoubleClick(data.pt, data.time))
            {
                _hasPendingClick = false;
                App.UiDispatcherQueue?.TryEnqueue(() =>
                {
                    _ = ToggleDesktopClearAsync();
                });
            }
            else
            {
                _hasPendingClick = true;
                _pendingClickX = data.pt.X;
                _pendingClickY = data.pt.Y;
                _pendingClickTime = data.time;
            }
        }
        catch (Exception ex)
        {
            App.Log($"[DesktopDoubleClick] MouseHookProc failed: {ex}");
            _hasPendingClick = false;
        }

        return Win32Helper.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
    }

    private bool IsDoubleClick(Win32Helper.POINT point, uint time)
    {
        if (!_hasPendingClick)
        {
            return false;
        }

        uint doubleClickTime = Win32Helper.GetDoubleClickTime();
        if (doubleClickTime == 0)
        {
            doubleClickTime = 500;
        }

        uint elapsed = unchecked(time - _pendingClickTime);
        if (elapsed > doubleClickTime)
        {
            return false;
        }

        int dxThreshold = Math.Max(1, Win32Helper.GetSystemMetrics(Win32Helper.SM_CXDOUBLECLK) / 2);
        int dyThreshold = Math.Max(1, Win32Helper.GetSystemMetrics(Win32Helper.SM_CYDOUBLECLK) / 2);
        return Math.Abs(point.X - _pendingClickX) <= dxThreshold &&
               Math.Abs(point.Y - _pendingClickY) <= dyThreshold;
    }

    private async Task ToggleDesktopClearAsync()
    {
        if (_isInvoking)
        {
            return;
        }

        _isInvoking = true;
        try
        {
            _shellIcons.SyncShellIconState();
            bool paintedMode = _shellIcons.IsPaintedDesktopActive;

            if (_isCleared)
            {
                App.Log("[DesktopDoubleClick] Restoring widgets");
                // Painted mode owns ListView hide; do not Show native icons on restore.
                if (!paintedMode)
                {
                    _shellIcons.ReleaseHidden(DesktopShellIconService.HideReason.DoubleClickClear);
                }

                await _setPaintedUiVisibleAsync(true);
                _isCleared = false;
                return;
            }

            App.Log("[DesktopDoubleClick] Hiding widgets");
            if (!paintedMode)
            {
                _shellIcons.RequestHidden(DesktopShellIconService.HideReason.DoubleClickClear);
            }

            await _setPaintedUiVisibleAsync(false);
            _isCleared = true;
        }
        catch (Exception ex)
        {
            App.Log($"[DesktopDoubleClick] Toggle failed: {ex}");
        }
        finally
        {
            _isInvoking = false;
        }
    }

    private static bool IsModifierPressed()
    {
        return Win32Helper.IsKeyPressed(Windows.System.VirtualKey.Control) ||
               Win32Helper.IsKeyPressed(Windows.System.VirtualKey.Menu) ||
               Win32Helper.IsKeyPressed(Windows.System.VirtualKey.Shift);
    }

    private bool IsEmptyDesktopPoint(Win32Helper.POINT point)
    {
        IntPtr hwnd = Win32Helper.WindowFromPoint(point);
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        IntPtr root = Win32Helper.GetAncestor(hwnd, Win32Helper.GA_ROOT);
        if (App.Current.IsBentoDeskWindow(hwnd) || App.Current.IsBentoDeskWindow(root))
        {
            return false;
        }

        if (!IsDesktopShellWindow(hwnd))
        {
            return false;
        }

        IntPtr listView = WidgetLayerService.FindDesktopIconListView();
        if (listView == IntPtr.Zero || !Win32Helper.IsWindowVisible(listView))
        {
            // Icons already hidden: any desktop-shell hit counts as empty desktop.
            return true;
        }

        if (!IsSameOrDescendant(listView, hwnd) && !WindowHasClass(hwnd, "SHELLDLL_DefView"))
        {
            // Click landed on WorkerW/Progman wallpaper chrome rather than the icon list.
            return true;
        }

        if (!TryIsDesktopIconItemAtPoint(listView, point, out bool isOnItem))
        {
            return false;
        }

        return !isOnItem;
    }

    private static bool IsDesktopShellWindow(IntPtr hwnd)
    {
        // 仅认 Progman / WorkerW：资源管理器窗口同样含子级 SHELLDLL_DefView / SysListView32，
        // 不能靠这两类类名判断，否则文件夹内双击会误触发清桌面。
        IntPtr current = hwnd;
        for (int depth = 0; depth < 8 && current != IntPtr.Zero; depth++)
        {
            if (WindowHasClass(current, "Progman") ||
                WindowHasClass(current, "WorkerW"))
            {
                return true;
            }

            current = Win32Helper.GetParent(current);
        }

        return false;
    }

    private static bool IsSameOrDescendant(IntPtr ancestor, IntPtr hwnd)
    {
        IntPtr current = hwnd;
        while (current != IntPtr.Zero)
        {
            if (current == ancestor)
            {
                return true;
            }

            current = Win32Helper.GetParent(current);
        }

        return false;
    }

    private static bool WindowHasClass(IntPtr hwnd, string className)
    {
        var buffer = new StringBuilder(256);
        return Win32Helper.GetClassName(hwnd, buffer, buffer.Capacity) > 0 &&
               buffer.ToString().Equals(className, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryIsDesktopIconItemAtPoint(
        IntPtr listView,
        Win32Helper.POINT screenPoint,
        out bool isOnItem)
    {
        isOnItem = false;

        var clientPoint = screenPoint;
        if (!Win32Helper.ScreenToClient(listView, ref clientPoint))
        {
            return false;
        }

        // LVM_HITTEST 的 lParam 必须指向目标进程地址空间。
        _ = Win32Helper.GetWindowThreadProcessId(listView, out uint processId);
        if (processId == 0)
        {
            return false;
        }

        IntPtr processHandle = Win32Helper.OpenProcess(
            Win32Helper.ProcessVmOperation |
            Win32Helper.ProcessVmRead |
            Win32Helper.ProcessVmWrite |
            Win32Helper.ProcessQueryInformation,
            false,
            processId);
        if (processHandle == IntPtr.Zero)
        {
            App.Log($"[DesktopDoubleClick] OpenProcess for hit-test failed error={Marshal.GetLastWin32Error()}");
            return false;
        }

        IntPtr remoteBuffer = IntPtr.Zero;
        IntPtr localBuffer = IntPtr.Zero;
        try
        {
            int size = Marshal.SizeOf<Win32Helper.LVHITTESTINFO>();
            remoteBuffer = Win32Helper.VirtualAllocEx(
                processHandle,
                IntPtr.Zero,
                (nuint)size,
                Win32Helper.MemCommit | Win32Helper.MemReserve,
                Win32Helper.PageReadWrite);
            if (remoteBuffer == IntPtr.Zero)
            {
                App.Log($"[DesktopDoubleClick] VirtualAllocEx for hit-test failed error={Marshal.GetLastWin32Error()}");
                return false;
            }

            localBuffer = Marshal.AllocHGlobal(size);
            var hitTest = new Win32Helper.LVHITTESTINFO
            {
                pt = clientPoint
            };
            Marshal.StructureToPtr(hitTest, localBuffer, false);

            if (!Win32Helper.WriteProcessMemory(
                    processHandle,
                    remoteBuffer,
                    localBuffer,
                    (nuint)size,
                    out _))
            {
                App.Log($"[DesktopDoubleClick] WriteProcessMemory for hit-test failed error={Marshal.GetLastWin32Error()}");
                return false;
            }

            _ = Win32Helper.SendMessage(
                listView,
                Win32Helper.LVM_HITTEST,
                IntPtr.Zero,
                remoteBuffer);

            if (!Win32Helper.ReadProcessMemory(
                    processHandle,
                    remoteBuffer,
                    localBuffer,
                    (nuint)size,
                    out _))
            {
                App.Log($"[DesktopDoubleClick] ReadProcessMemory for hit-test failed error={Marshal.GetLastWin32Error()}");
                return false;
            }

            hitTest = Marshal.PtrToStructure<Win32Helper.LVHITTESTINFO>(localBuffer);
            isOnItem = hitTest.iItem >= 0 && (hitTest.flags & Win32Helper.LVHT_ONITEM) != 0;
            return true;
        }
        finally
        {
            if (localBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(localBuffer);
            }

            if (remoteBuffer != IntPtr.Zero)
            {
                _ = Win32Helper.VirtualFreeEx(processHandle, remoteBuffer, 0, Win32Helper.MemRelease);
            }

            _ = Win32Helper.CloseHandle(processHandle);
        }
    }
}
