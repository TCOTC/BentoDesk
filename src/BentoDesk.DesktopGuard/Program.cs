using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BentoDesk.DesktopGuard;

/// <summary>
/// Session watchdog aligned with Xiaozhi XZGuarder: wait for the main process,
/// then restore Explorer's native desktop SysListView32 visibility.
/// </summary>
internal static class Program
{
    private const string MutexName = "Local\\BentoDesk_DesktopGuard_Mutex_8BF33840";
    private const int OpenProcessSync = 0x00100000; // SYNCHRONIZE
    private const uint WaitObject0 = 0;
    private const uint WaitFailed = 0xFFFFFFFF;
    private const int SwShowNoActivate = 4;

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BentoDesk",
        "BentoDesk.DesktopGuard.log");

    private static int Main(string[] args)
    {
        using var mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            Log("Another DesktopGuard instance is already running.");
            return 0;
        }

        try
        {
            int parentPid = ParsePid(args);
            if (parentPid <= 0)
            {
                Log("Missing or invalid --pid argument.");
                return 2;
            }

            Log($"Guarding BentoDesk pid={parentPid}");
            WaitForProcessExit(parentPid);
            bool restored = RestoreDesktopIcons();
            Log($"Parent exited; restore desktop icons ok={restored}");
            return restored ? 0 : 3;
        }
        catch (Exception ex)
        {
            Log($"Fatal: {ex}");
            return 1;
        }
    }

    private static int ParsePid(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--pid", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(args[i + 1], out int pid))
            {
                return pid;
            }
        }

        return 0;
    }

    private static void WaitForProcessExit(int processId)
    {
        IntPtr handle = OpenProcess(OpenProcessSync, false, processId);
        if (handle == IntPtr.Zero)
        {
            // Process already gone.
            Log($"OpenProcess failed for pid={processId}; treating as exited. error={Marshal.GetLastWin32Error()}");
            return;
        }

        try
        {
            uint result = WaitForSingleObject(handle, uint.MaxValue);
            if (result != WaitObject0 && result != WaitFailed)
            {
                Log($"WaitForSingleObject returned {result}");
            }
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    private static bool RestoreDesktopIcons()
    {
        IntPtr listView = FindDesktopIconListView();
        if (listView == IntPtr.Zero || !IsWindow(listView))
        {
            Log("Desktop SysListView32 not found.");
            return false;
        }

        if (IsWindowVisible(listView))
        {
            Log("Desktop icons already visible.");
            return true;
        }

        _ = ShowWindow(listView, SwShowNoActivate);
        bool visible = IsWindowVisible(listView);
        Log($"ShowWindow result visible={visible} hwnd=0x{listView.ToInt64():X}");
        return visible;
    }

    private static IntPtr FindDesktopIconListView()
    {
        IntPtr defView = FindExistingDesktopIconView();
        if (defView == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        IntPtr named = FindWindowEx(defView, IntPtr.Zero, "SysListView32", "FolderView");
        if (named != IntPtr.Zero)
        {
            return named;
        }

        return FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
    }

    private static IntPtr FindExistingDesktopIconView()
    {
        IntPtr existingDefView = IntPtr.Zero;
        EnumWindows((hWnd, _) =>
        {
            IntPtr defView = FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView != IntPtr.Zero)
            {
                existingDefView = defView;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return existingDefView;
    }

    private static void Log(string message)
    {
        try
        {
            string directory = Path.GetDirectoryName(LogPath)!;
            Directory.CreateDirectory(directory);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Best-effort logging only.
        }

        try
        {
            Debug.WriteLine($"[DesktopGuard] {message}");
        }
        catch
        {
        }
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
