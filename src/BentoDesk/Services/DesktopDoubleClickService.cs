using System.Runtime.InteropServices;
using System.Text;
using BentoDesk.Helpers;

namespace BentoDesk.Services;

/// <summary>
/// Listens for double-clicks on empty desktop space and toggles hiding
/// all desktop icons together with all BentoDesk widgets.
/// </summary>
public sealed class DesktopDoubleClickService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly Func<bool, Task> _setAllWidgetsVisibleAsync;
    private readonly Win32Helper.LowLevelMouseProc _mouseHookProc;
    private IntPtr _mouseHookHandle;
    private IntPtr _hiddenListViewHwnd;
    private bool _isCleared;
    private bool _iconsHiddenByUs;
    private bool _isInvoking;
    private bool _hasPendingClick;
    private int _pendingClickX;
    private int _pendingClickY;
    private uint _pendingClickTime;

    public DesktopDoubleClickService(
        SettingsService settingsService,
        Func<bool, Task> setAllWidgetsVisibleAsync)
    {
        _settingsService = settingsService;
        _setAllWidgetsVisibleAsync = setAllWidgetsVisibleAsync;
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
        SyncShellIconState();

        if (!_isCleared && !_iconsHiddenByUs)
        {
            return;
        }

        try
        {
            if (_iconsHiddenByUs)
            {
                WidgetLayerService.SetDesktopIconsVisible(true);
                _iconsHiddenByUs = false;
                _hiddenListViewHwnd = IntPtr.Zero;
            }

            if (_isCleared)
            {
                await _setAllWidgetsVisibleAsync(true);
                _isCleared = false;
            }
        }
        catch (Exception ex)
        {
            App.Log($"[DesktopDoubleClick] RestoreIfNeeded failed: {ex}");
        }
    }

    public void Dispose()
    {
        UninstallMouseHook();
        if (_iconsHiddenByUs)
        {
            WidgetLayerService.SetDesktopIconsVisible(true);
            _iconsHiddenByUs = false;
            _hiddenListViewHwnd = IntPtr.Zero;
        }

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
            SyncShellIconState();

            if (_isCleared)
            {
                App.Log("[DesktopDoubleClick] Restoring desktop icons and widgets");
                WidgetLayerService.SetDesktopIconsVisible(true);
                _iconsHiddenByUs = false;
                _hiddenListViewHwnd = IntPtr.Zero;
                await _setAllWidgetsVisibleAsync(true);
                _isCleared = false;
                return;
            }

            App.Log("[DesktopDoubleClick] Hiding desktop icons and widgets");
            if (WidgetLayerService.SetDesktopIconsVisible(false))
            {
                _iconsHiddenByUs = true;
                _hiddenListViewHwnd = WidgetLayerService.FindDesktopIconListView();
            }

            await _setAllWidgetsVisibleAsync(false);
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

    /// <summary>
    /// Explorer 重启后桌面图标会自行恢复，但我们内存中的隐藏标记可能仍在。
    /// 这里只清除“由我们隐藏图标”的标记，保留 <see cref="_isCleared"/>，
    /// 以便下一次双击继续走恢复格子的路径。
    /// </summary>
    private void SyncShellIconState()
    {
        if (!_iconsHiddenByUs)
        {
            return;
        }

        bool hiddenWindowGone =
            _hiddenListViewHwnd != IntPtr.Zero &&
            !Win32Helper.IsWindow(_hiddenListViewHwnd);
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
            $"[DesktopDoubleClick] Shell icon state changed externally " +
            $"hiddenWindowGone={hiddenWindowGone} iconsVisible={iconsVisible}; clearing iconsHiddenByUs");
        _iconsHiddenByUs = false;
        _hiddenListViewHwnd = IntPtr.Zero;
    }

    private static bool IsModifierPressed()
    {
        return Win32Helper.IsKeyPressed(Windows.System.VirtualKey.Control) ||
               Win32Helper.IsKeyPressed(Windows.System.VirtualKey.Menu) ||
               Win32Helper.IsKeyPressed(Windows.System.VirtualKey.Shift);
    }

    private static bool IsEmptyDesktopPoint(Win32Helper.POINT point)
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
            // 命中测试失败时宁可不触发，避免把图标双击误判成空白双击。
            return false;
        }

        return !isOnItem;
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
        // 直接把本进程栈上的 LVHITTESTINFO 指针发给 Explorer 会让 COMCTL32 解引用无效地址并崩溃。
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

    private static bool IsDesktopShellWindow(IntPtr hWnd)
    {
        IntPtr current = hWnd;
        while (current != IntPtr.Zero)
        {
            if (WindowHasClass(current, "Progman") ||
                WindowHasClass(current, "WorkerW") ||
                WindowHasClass(current, "SHELLDLL_DefView") ||
                WindowHasClass(current, "SysListView32"))
            {
                return true;
            }

            current = Win32Helper.GetParent(current);
        }

        IntPtr root = Win32Helper.GetAncestor(hWnd, Win32Helper.GA_ROOT);
        return WindowHasClass(root, "Progman") || WindowHasClass(root, "WorkerW");
    }

    private static bool IsSameOrDescendant(IntPtr ancestor, IntPtr hWnd)
    {
        IntPtr current = hWnd;
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

    private static bool WindowHasClass(IntPtr hWnd, string className)
    {
        if (hWnd == IntPtr.Zero)
        {
            return false;
        }

        var buffer = new StringBuilder(256);
        int length = Win32Helper.GetClassName(hWnd, buffer, buffer.Capacity);
        return length > 0 &&
               string.Equals(buffer.ToString(), className, StringComparison.Ordinal);
    }
}
