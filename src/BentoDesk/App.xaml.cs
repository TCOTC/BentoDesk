// Copyright (c) BentoDesk. All rights reserved.

using CommunityToolkit.Mvvm.Input;
using BentoDesk.Helpers;
using BentoDesk.Models;
using BentoDesk.Services;
using BentoDesk.Views;
using System.Diagnostics;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Extensions.DependencyInjection;
using DrawingPoint = System.Drawing.Point;
using WinRT.Interop;

namespace BentoDesk;

/// <summary>
/// Application bootstrap, tray menu, and widget lifecycle.
/// </summary>
public partial class App : Application
{
    private const double TrayMenuItemWidth = 176;
    private const int TrayContextMenuFallbackOffsetPixels = 24;
    private const int TrayContextMenuEstimatedWidth = (int)TrayMenuItemWidth + 16;
    private const int MaxQueuedLogLines = 4096;
    private const long MaxLogFileSizeBytes = 5 * 1024 * 1024; // 5 MB before rotation
    private const string PendingNativeNotificationActivationFileName = "pending-notification-activation.txt";
    private const string PendingJumpListArgumentFileName = "pending-jumplist-arg.txt";
    private const string VerboseLoggingEnvironmentVariable = "BENTODESK_VERBOSE_LOG";
    private static readonly bool EnableVerboseLogging = IsEnabledEnvironmentValue(
        Environment.GetEnvironmentVariable(VerboseLoggingEnvironmentVariable));

    private static readonly string LogPath = BentoDeskDataPathService.Current.LogFilePath;
    private static readonly string PendingNativeNotificationActivationPath = Path.Combine(
        BentoDeskDataPathService.Current.RootPath,
        PendingNativeNotificationActivationFileName);
    private static readonly string PendingJumpListArgumentPath = Path.Combine(
        BentoDeskDataPathService.Current.RootPath,
        PendingJumpListArgumentFileName);
    private static readonly System.Collections.Concurrent.ConcurrentQueue<string> s_logQueue = new();
    private static readonly SemaphoreSlim s_logSignal = new(0);
    private static int s_logWorkerStarted;
    private static int s_pendingLogLineCount;
    private static int s_logDirectoryEnsured;

    private static Mutex? _singleInstanceMutex;
    private static EventWaitHandle? _activationEvent;
    private static RegisteredWaitHandle? _activationRegistration;

    private TaskbarIcon? _trayIcon;
    private Window? _trayWindow;
    private MenuFlyout? _trayContextMenu;
    private bool _traySecondWindowSyncLogged;
    private MenuFlyoutItem? _trayMapFolderItem;
    private readonly Dictionary<WidgetKind, MenuFlyoutItem> _trayCreateWidgetItems = [];
    private MenuFlyoutItem? _trayUpdateItem;
    private MenuFlyoutItem? _traySettingsItem;
    private MenuFlyoutItem? _trayExitItem;
    private SettingsWindow? _settingsWindow;
    private OnboardingWindow? _onboardingWindow;
    private NativeAppNotificationService? _nativeNotificationService;
    private DisplayAreaWatcherService? _displayAreaWatcher;
    private bool _widgetsRaisedFromTray;
    private bool _hasUpdateAvailable;
    private bool _updateNotificationShown;
    private string _availableUpdateVersion = string.Empty;

    public static new App Current => (App)Application.Current;

    public static Microsoft.UI.Dispatching.DispatcherQueue UiDispatcherQueue { get; private set; } = null!;

    public bool IsStartupMode { get; set; }

    public ServiceProvider Services { get; private set; } = null!;
    public SettingsService SettingsService { get; private set; } = null!;
    public BentoDeskDataBackupService DataBackupService { get; private set; } = null!;
    public FileService FileService { get; private set; } = null!;
    public OrganizerService OrganizerService { get; private set; } = null!;
    public IAppUpdateService AppUpdateService { get; private set; } = null!;
    public LocalizationService LocalizationService { get; private set; } = null!;
    public ThemeService ThemeService { get; private set; } = null!;
    public GlobalHotkeyService? GlobalHotkeyService { get; private set; }
    public DesktopShellIconService? DesktopShellIconService { get; private set; }
    public DesktopGuardHostService? DesktopGuardHostService { get; private set; }
    public FreeDesktopService? FreeDesktopService { get; private set; }
    public DesktopDoubleClickService? DesktopDoubleClickService { get; private set; }
    public WidgetManager? WidgetManager { get; private set; }
    public ResizeGuideOverlayService ResizeGuideOverlay { get; private set; } = null!;
    public NativeAppNotificationService? NativeNotificationService => _nativeNotificationService;
    public DisplayAreaWatcherService? DisplayAreaWatcher => _displayAreaWatcher;
    public SettingsWindow? SettingsWindowInstance => _settingsWindow;

    public static bool IsVerboseLoggingEnabled => EnableVerboseLogging;

    public App()
    {
        // Register AUMID early so the taskbar button and Jump List work.
        JumpListService.RegisterAppUserModelId();

        Log("App() constructor start");
        bool launchedForStartup = IsStartupLaunch(Environment.GetCommandLineArgs());
        string? nativeNotificationActivationArguments = TryGetCurrentNativeNotificationActivationArguments();
        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "BentoDesk_Activate_Event_7F3A9B2E");
        _singleInstanceMutex = new Mutex(true, "BentoDesk_SingleInstance_Mutex_7F3A9B2E", out bool createdNew);
        if (!createdNew)
        {
            if (!string.IsNullOrWhiteSpace(nativeNotificationActivationArguments))
            {
                StorePendingNativeNotificationActivationArguments(nativeNotificationActivationArguments);
            }
            else if (launchedForStartup)
            {
                Log("Another instance running; startup launch exiting silently");
                Environment.Exit(0);
            }
            else
            {
                // Check for Jump List activation arguments from command line
                string? jumpListArg = JumpListService.TryGetJumpListArgument(
                    string.Join(' ', Environment.GetCommandLineArgs()));
                if (jumpListArg is not null)
                {
                    StorePendingJumpListArgument(jumpListArg);
                }
            }

            Log("Another instance running, signaling existing instance");
            try
            {
                _activationEvent.Set();
            }
            catch (Exception ex)
            {
                Log($"Failed to signal existing instance: {ex}");
            }

            Environment.Exit(0);
        }

        InitializeComponent();

        // Build DI container and resolve core services
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddBentoDeskServices();
        Services = serviceCollection.BuildServiceProvider();

        SettingsService = Services.GetRequiredService<SettingsService>();
        DataBackupService = Services.GetRequiredService<BentoDeskDataBackupService>();
        FileService = Services.GetRequiredService<FileService>();
        OrganizerService = Services.GetRequiredService<OrganizerService>();
        AppUpdateService = Services.GetRequiredService<IAppUpdateService>();
        ResizeGuideOverlay = Services.GetRequiredService<ResizeGuideOverlayService>();

        DirectStartupService.TryRemoveLegacyStartupShortcutSafe();
        AppUpdateService.CheckCompleted += OnUpdateCheckCompleted;
        UnhandledException += OnUnhandledException;
        Log($"Process integrity {GetProcessIntegrityReport()} pid={Environment.ProcessId} processPath={Environment.ProcessPath ?? "unknown"} baseDir={AppContext.BaseDirectory}");
        Log($"Process parent {GetParentProcessReport()} commandLine={Environment.CommandLine}");
        Log($"UAC {GetUacPolicyReport()}");
        Log($"AppCompat {GetAppCompatReport()}");
    }

    private static string GetProcessIntegrityReport()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return $"isAdminRole={principal.IsInRole(WindowsBuiltInRole.Administrator)} {GetProcessTokenReport(GetCurrentProcess())}";
        }
        catch (Exception ex)
        {
            return $"unknown error={ex.Message}";
        }
    }

    private static string GetAppCompatReport()
    {
        string exePath = Path.Combine(AppContext.BaseDirectory, "BentoDesk.exe");
        string? currentUser = GetAppCompatLayerValue(Registry.CurrentUser, exePath);
        string? localMachine = GetAppCompatLayerValue(Registry.LocalMachine, exePath);

        return $"exe='{exePath}' hkcu={(string.IsNullOrWhiteSpace(currentUser) ? "none" : currentUser)} " +
               $"hklm={(string.IsNullOrWhiteSpace(localMachine) ? "none" : localMachine)}";
    }

    private static string GetParentProcessReport()
    {
        try
        {
            if (!TryGetParentProcessId(Environment.ProcessId, out uint parentProcessId) || parentProcessId == 0)
            {
                return "unknown";
            }

            string parentName = "unknown";
            try
            {
                parentName = Process.GetProcessById((int)parentProcessId).ProcessName;
            }
            catch
            {
            }

            string parentTokenReport = GetProcessTokenReport(parentProcessId);
            return $"ppid={parentProcessId} parent={parentName} {parentTokenReport}";
        }
        catch (Exception ex)
        {
            return $"unknown error={ex.Message}";
        }
    }

    private static string GetProcessTokenReport(uint processId)
    {
        IntPtr processHandle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (processHandle == IntPtr.Zero)
        {
            return $"token=unavailable error={Marshal.GetLastWin32Error()}";
        }

        try
        {
            return GetProcessTokenReport(processHandle);
        }
        finally
        {
            CloseHandle(processHandle);
        }
    }

    private static string GetProcessTokenReport(IntPtr processHandle)
    {
        string tokenElevated = TryGetTokenElevation(processHandle, out bool isTokenElevated)
            ? isTokenElevated.ToString()
            : "unknown";
        string integrityLevel = TryGetIntegrityLevel(processHandle, out string level)
            ? level
            : "unknown";

        return $"tokenElevated={tokenElevated} integrity={integrityLevel}";
    }

    private static string GetUacPolicyReport()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System");
            object? enableLua = key?.GetValue("EnableLUA");
            object? consentPrompt = key?.GetValue("ConsentPromptBehaviorAdmin");
            object? promptOnSecureDesktop = key?.GetValue("PromptOnSecureDesktop");

            return $"EnableLUA={FormatRegistryValue(enableLua)} " +
                   $"ConsentPromptBehaviorAdmin={FormatRegistryValue(consentPrompt)} " +
                   $"PromptOnSecureDesktop={FormatRegistryValue(promptOnSecureDesktop)}";
        }
        catch (Exception ex)
        {
            return $"unknown error={ex.Message}";
        }
    }

    private static string FormatRegistryValue(object? value)
    {
        return value is null ? "missing" : value.ToString() ?? "unknown";
    }

    private static bool TryGetParentProcessId(int processId, out uint parentProcessId)
    {
        parentProcessId = 0;
        const uint Th32csSnapProcess = 0x00000002;

        IntPtr snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
        {
            return false;
        }

        try
        {
            var entry = new ProcessEntry32
            {
                dwSize = (uint)Marshal.SizeOf<ProcessEntry32>()
            };

            if (!Process32First(snapshot, ref entry))
            {
                return false;
            }

            do
            {
                if (entry.th32ProcessID == (uint)processId)
                {
                    parentProcessId = entry.th32ParentProcessID;
                    return true;
                }
            }
            while (Process32Next(snapshot, ref entry));

            return false;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    private static string? GetAppCompatLayerValue(RegistryKey? rootKey, string exePath)
    {
        if (rootKey is null)
        {
            return null;
        }

        try
        {
            using var key = rootKey.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers");
            if (key?.GetValue(exePath) is string value && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        catch (Exception ex)
        {
            return $"error:{ex.Message}";
        }

        return null;
    }

    private static bool TryGetTokenElevation(IntPtr processHandle, out bool isElevated)
    {
        isElevated = false;
        if (!OpenProcessToken(processHandle, TokenQuery, out IntPtr tokenHandle))
        {
            return false;
        }

        try
        {
            int length = Marshal.SizeOf<TokenElevation>();
            IntPtr buffer = Marshal.AllocHGlobal(length);
            try
            {
                if (!GetTokenInformation(tokenHandle, TokenInformationClass.TokenElevation, buffer, length, out _))
                {
                    return false;
                }

                var elevation = Marshal.PtrToStructure<TokenElevation>(buffer);
                isElevated = elevation.TokenIsElevated != 0;
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            CloseHandle(tokenHandle);
        }
    }

    private static bool TryGetIntegrityLevel(IntPtr processHandle, out string level)
    {
        level = string.Empty;
        if (!OpenProcessToken(processHandle, TokenQuery, out IntPtr tokenHandle))
        {
            return false;
        }

        try
        {
            _ = GetTokenInformation(tokenHandle, TokenInformationClass.TokenIntegrityLevel, IntPtr.Zero, 0, out int length);
            if (length <= 0)
            {
                return false;
            }

            IntPtr buffer = Marshal.AllocHGlobal(length);
            try
            {
                if (!GetTokenInformation(tokenHandle, TokenInformationClass.TokenIntegrityLevel, buffer, length, out _))
                {
                    return false;
                }

                var label = Marshal.PtrToStructure<TokenMandatoryLabel>(buffer);
                IntPtr subAuthorityCount = GetSidSubAuthorityCount(label.Label.Sid);
                if (subAuthorityCount == IntPtr.Zero)
                {
                    return false;
                }

                byte count = Marshal.ReadByte(subAuthorityCount);
                if (count == 0)
                {
                    return false;
                }

                IntPtr integrityRidPointer = GetSidSubAuthority(label.Label.Sid, (uint)(count - 1));
                int integrityRid = Marshal.ReadInt32(integrityRidPointer);
                level = FormatIntegrityLevel(integrityRid);
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            CloseHandle(tokenHandle);
        }
    }

    private static string FormatIntegrityLevel(int integrityRid)
    {
        return integrityRid switch
        {
            < SecurityMandatoryLowRid => $"Untrusted(0x{integrityRid:X})",
            < SecurityMandatoryMediumRid => $"Low(0x{integrityRid:X})",
            < SecurityMandatoryHighRid => $"Medium(0x{integrityRid:X})",
            < SecurityMandatorySystemRid => $"High(0x{integrityRid:X})",
            < SecurityMandatoryProtectedProcessRid => $"System(0x{integrityRid:X})",
            _ => $"Protected(0x{integrityRid:X})"
        };
    }

    private const uint TokenQuery = 0x0008;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int SecurityMandatoryLowRid = 0x1000;
    private const int SecurityMandatoryMediumRid = 0x2000;
    private const int SecurityMandatoryHighRid = 0x3000;
    private const int SecurityMandatorySystemRid = 0x4000;
    private const int SecurityMandatoryProtectedProcessRid = 0x5000;

    private enum TokenInformationClass
    {
        TokenElevation = 20,
        TokenIntegrityLevel = 25
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenElevation
    {
        public int TokenIsElevated;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenMandatoryLabel
    {
        public SidAndAttributes Label;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SidAndAttributes
    {
        public IntPtr Sid;
        public int Attributes;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(IntPtr hSnapshot, ref ProcessEntry32 lppe);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(IntPtr hSnapshot, ref ProcessEntry32 lppe);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(
        IntPtr tokenHandle,
        TokenInformationClass tokenInformationClass,
        IntPtr tokenInformation,
        int tokenInformationLength,
        out int returnLength);

    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthority(IntPtr sid, uint subAuthority);

    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);

    public bool IsBentoDeskWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        Win32Helper.GetWindowThreadProcessId(hwnd, out uint processId);
        if (processId == (uint)Environment.ProcessId)
        {
            return true;
        }

        IntPtr rootHwnd = Win32Helper.GetAncestor(hwnd, Win32Helper.GA_ROOT);
        if (rootHwnd == IntPtr.Zero)
        {
            rootHwnd = hwnd;
        }

        if (_trayWindow is not null && rootHwnd == WindowNative.GetWindowHandle(_trayWindow))
        {
            return true;
        }

        if (_settingsWindow is not null && rootHwnd == WindowNative.GetWindowHandle(_settingsWindow))
        {
            return true;
        }

        if (_onboardingWindow is not null && rootHwnd == WindowNative.GetWindowHandle(_onboardingWindow))
        {
            return true;
        }

        return WidgetManager?.Widgets.Values.Any(entry => entry.Window.WindowHandle == rootHwnd) == true ||
               WidgetManager?.ContentWidgets.Values.Any(window => window.WindowHandle == rootHwnd) == true;
    }

    public static void Log(string msg)
    {
        try
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}";
            if (Interlocked.Increment(ref s_pendingLogLineCount) > MaxQueuedLogLines)
            {
                Interlocked.Decrement(ref s_pendingLogLineCount);
                return;
            }

            s_logQueue.Enqueue(line);
            EnsureLogWorkerStarted();
            s_logSignal.Release();
        }
        catch
        {
        }
    }

    public static void LogVerbose(string msg)
    {
        if (!EnableVerboseLogging)
        {
            return;
        }

        Log(msg);
    }

    /// <summary>
    /// Safely execute an async action from an event handler, catching and logging any exceptions.
    /// Use this instead of async void to prevent unhandled exceptions from crashing the app.
    /// </summary>
    public static async void SafeFireAndForget(Func<Task> action, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Log($"[SafeFireAndForget] Unhandled exception in {caller}: {ex}");
        }
    }

    private static void EnsureLogWorkerStarted()
    {
        if (Interlocked.CompareExchange(ref s_logWorkerStarted, 1, 0) == 0)
        {
            _ = Task.Run(ProcessLogQueueAsync);
        }
    }

    private static async Task ProcessLogQueueAsync()
    {
        while (true)
        {
            await s_logSignal.WaitAsync().ConfigureAwait(false);
            DrainLogQueue();
        }
    }

    private static void DrainLogQueue()
    {
        var builder = new System.Text.StringBuilder();
        while (s_logQueue.TryDequeue(out string? line))
        {
            Interlocked.Decrement(ref s_pendingLogLineCount);
            builder.Append(line);
        }

        if (builder.Length == 0)
        {
            return;
        }

        try
        {
            EnsureLogDirectory();
            TryRotateLogFileIfNeeded();
            File.AppendAllText(LogPath, builder.ToString());
        }
        catch
        {
        }
    }

    private static void TryRotateLogFileIfNeeded()
    {
        try
        {
            if (!File.Exists(LogPath))
            {
                return;
            }

            var info = new FileInfo(LogPath);
            if (info.Length < MaxLogFileSizeBytes)
            {
                return;
            }

            // Rotate: current → .1, old .1 is deleted
            string rotatedPath = LogPath + ".1";
            if (File.Exists(rotatedPath))
            {
                File.Delete(rotatedPath);
            }

            File.Move(LogPath, rotatedPath);
        }
        catch
        {
            // If rotation fails (e.g., file locked), continue appending to the current file.
        }
    }

    private static void EnsureLogDirectory()
    {
        if (Volatile.Read(ref s_logDirectoryEnsured) != 0)
        {
            return;
        }

        string? dir = Path.GetDirectoryName(LogPath);
        if (dir is not null)
        {
            Directory.CreateDirectory(dir);
        }

        Volatile.Write(ref s_logDirectoryEnsured, 1);
    }

    private static bool IsEnabledEnvironmentValue(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Trim() is "1" or "true" or "TRUE" or "yes" or "YES" or "on" or "ON";
    }

    private static bool IsStartupLaunch(IEnumerable<string> arguments)
    {
        return arguments.Any(IsStartupArgument);
    }

    private static bool IsStartupLaunch(string? arguments)
    {
        return !string.IsNullOrWhiteSpace(arguments) &&
            arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(IsStartupArgument);
    }

    private static bool IsStartupArgument(string argument)
    {
        return string.Equals(argument.Trim().Trim('"'), "--startup", StringComparison.OrdinalIgnoreCase);
    }

    private bool _isLaunched;

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (_isLaunched)
        {
            Log("OnLaunched skipped: already launched");
            return;
        }
        _isLaunched = true;

        using var perfScope = PerformanceLogger.Measure("App.OnLaunched", $"startup={IsStartupLaunch(args.Arguments)}");
        Log("OnLaunched start");
        // Preserve the user's focused app across tray/widget host initialization.
        IntPtr foregroundAtLaunch = Win32Helper.GetForegroundWindow();

        try
        {
            IsStartupMode = IsStartupLaunch(args.Arguments);
            UiDispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            WidgetSegmentedLayoutHelper.Initialize(UiDispatcherQueue);

            // A prepared restore is applied before any service reads or normalizes app data.
            BentoDeskRestoreApplyResult restoreResult = await DataBackupService.ApplyPendingRestoreAsync();

            // Phase 1: Load settings (must complete first)
            await SettingsService.LoadAsync();

            // Sync resize snap setting
            ResizeGuideOverlay.IsSnapEnabled = SettingsService.Settings.ResizeSnapEnabled;

            // Phase 2: Initialize services that depend on settings (parallel)
            ThemeService = Services.GetRequiredService<ThemeService>();
            LocalizationService = Services.GetRequiredService<LocalizationService>();
            LocalizationService.LanguageChanged += OnLanguageChanged;

            var themeService = ThemeService;
            var localizationService = LocalizationService;

            // Parallel: theme refresh only.
            var themeTask = Task.Run(() => themeService.RefreshAppearance());

            // Parallel: independent UI setup
            CreateTrayIcon();
            RegisterActivationListener();

            await themeTask;
            if (GlobalHotkeyService is null)
            {
                try
                {
                    GlobalHotkeyService = new GlobalHotkeyService(SettingsService, localizationService, ToggleTrayWidgetsAsync);
                    Log("[Init] GlobalHotkeyService created");
                }
                catch (Exception ex)
                {
                    Log($"[Init] GlobalHotkeyService creation failed: {ex}");
                }
            }

            if (GlobalHotkeyService is { } hotkeySvc && _trayWindow is not null)
            {
                try
                {
                    var trayHwnd = WindowNative.GetWindowHandle(_trayWindow);
                    if (trayHwnd != IntPtr.Zero && !hotkeySvc.IsRegistered)
                    {
                        Log($"[Init] Late-attaching GlobalHotkeyService to tray hwnd=0x{trayHwnd.ToInt64():X}");
                        hotkeySvc.Attach(trayHwnd);
                    }
                }
                catch (Exception ex)
                {
                    Log($"[Init] GlobalHotkeyService late-attach failed: {ex}");
                }
            }
            WidgetManager = new WidgetManager(SettingsService, FileService, OrganizerService, themeService, localizationService);
            WidgetManager.TrayLayerStateChanged += UpdateTrayLayerStateText;

            DesktopShellIconService ??= new DesktopShellIconService(UiDispatcherQueue);
            DesktopGuardHostService ??= new DesktopGuardHostService();
            FreeDesktopService ??= new FreeDesktopService(
                DesktopShellIconService,
                DesktopGuardHostService);
            OrganizerService.SetMembershipChangedCallback(() =>
            {
                _ = WidgetManager?.RefreshManagedDesktopWidgetsAsync();
            });

            if (DesktopDoubleClickService is null)
            {
                try
                {
                    DesktopDoubleClickService = new DesktopDoubleClickService(
                        SettingsService,
                        DesktopShellIconService,
                        async visible =>
                        {
                            // No free-icon layer; only toggle BentoDesk widgets.
                            if (WidgetManager is null)
                            {
                                return;
                            }

                            await WidgetManager.SetAllWidgetsVisibleAsync(visible);
                            if (!visible)
                            {
                                UpdateTrayLayerStateText(raised: false);
                            }
                        });
                    // WH_MOUSE_LL 延后到 FreeDesktop + RestoreWidgets 之后安装，避免启动重活拖慢整机输入。
                    Log("[Init] DesktopDoubleClickService created (hook deferred)");
                }
                catch (Exception ex)
                {
                    Log($"[Init] DesktopDoubleClickService creation failed: {ex}");
                }
            }

            // Phase 3: Hide native desktop icons first, then restore widgets (avoid dual shell load).
            try
            {
                await FreeDesktopService.StartAsync();
                Log("[Init] FreeDesktop painted mode started (shell hide + Guard)");
            }
            catch (Exception ex)
            {
                Log($"[Init] FreeDesktop start failed: {ex}");
            }

            await WidgetManager.RestoreWidgetsAsync();
            Win32Helper.TryRestoreForegroundWindow(foregroundAtLaunch);

            // Install the low-level mouse hook only after the critical startup path settles.
            try
            {
                DesktopDoubleClickService?.RefreshRegistration();
            }
            catch (Exception ex)
            {
                Log($"[Init] DesktopDoubleClickService late registration failed: {ex}");
            }

            StartNativeNotificationService();
            ShowDataRestoreResultNotification(restoreResult);
            ScheduleDeferredAutomaticSnapshot();

            if (!IsStartupMode && !SettingsService.Settings.HasCompletedOnboarding)
            {
                SettingsService.Settings.HasCompletedOnboarding = true;
                await SettingsService.SaveAsync();
                ShowOnboarding();
            }

            ScheduleBackgroundUpdateCheck();
            _diagnosticsService = new AppDiagnosticsService(UiDispatcherQueue);
            _diagnosticsService.StartAll();

            // Start display area watcher for hot-plug detection
            _displayAreaWatcher = new DisplayAreaWatcherService(UiDispatcherQueue);
            _displayAreaWatcher.DisplaysChanged += OnDisplaysChanged;
            _displayAreaWatcher.Start();

            // Configure taskbar Jump List with quick actions
            _ = JumpListService.ConfigureAsync(LocalizationService);

            // Handle Jump List activation on first launch (not second instance)
            string? firstLaunchJumpArg = JumpListService.TryGetJumpListArgument(args.Arguments);
            if (firstLaunchJumpArg is not null)
            {
                _ = JumpListService.HandleActivationAsync(firstLaunchJumpArg);
            }

            Log("OnLaunched completed successfully");
        }
        catch (Exception ex)
        {
            Log($"Exception in OnLaunched: {ex}");
        }
    }

    /// <summary>
    /// Called when the set of displays changes (hot-plug, resolution change, etc.).
    /// Invalidates caches and triggers widget repositioning.
    /// </summary>
    private async void OnDisplaysChanged()
    {
        try
        {
            Log($"[DisplayAreaWatcher] Displays changed, triggering widget reposition");

            // Invalidate the desktop icon view cache since work areas may have changed
            WidgetLayerService.InvalidateDesktopIconViewCache();
            DesktopShellIconService?.SyncShellIconState();

            // Reposition all widgets to ensure they're on visible screens
            if (WidgetManager is not null)
            {
                await WidgetManager.RestoreWidgetPositionsAsync();
            }
        }
        catch (Exception ex)
        {
            Log($"[DisplayAreaWatcher] OnDisplaysChanged failed: {ex}");
        }
    }

    private AppDiagnosticsService? _diagnosticsService;

    private void ScheduleBackgroundUpdateCheck()
    {
        if (!SettingsService.Settings.AutoCheckForUpdates)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(IsStartupMode ? TimeSpan.FromSeconds(45) : TimeSpan.FromSeconds(12));
                var result = await AppUpdateService.CheckForUpdatesAsync();
                SettingsService.Settings.LastUpdateCheckAt = DateTimeOffset.Now;
                SettingsService.SaveDebounced(notifySubscribers: false);

                if (result.IsUpdateAvailable && result.Manifest is not null)
                {
                    Log($"[Update] New version available: {result.Manifest.Version}");
                }
                else if (result.Status == AppUpdateCheckStatus.Failed)
                {
                    Log($"[Update] Background check failed: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Log($"[Update] Background check crashed: {ex}");
            }
        });
    }

    /// <summary>
    /// Captures an automatic snapshot after first paint so zip I/O does not contend with
    /// widget restore and icon hydration on the startup critical path.
    /// </summary>
    private void ScheduleDeferredAutomaticSnapshot()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(IsStartupMode ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(5));
                await DataBackupService.CreateAutomaticSnapshotIfDueAsync();
            }
            catch (Exception ex)
            {
                Log($"[DataBackup] Deferred automatic snapshot failed: {ex}");
            }
        });
    }

    private void StartNativeNotificationService()
    {
        _nativeNotificationService?.Dispose();
        _nativeNotificationService = new NativeAppNotificationService(
            activation => HandleNativeNotificationActivation(activation.Arguments, activation.UserInput));
        if (_nativeNotificationService.Register())
        {
            HandleCurrentNativeNotificationActivation();
        }
    }

    private void HandleCurrentNativeNotificationActivation()
    {
        NativeAppNotificationActivation? activation = TryGetCurrentNativeNotificationActivation();
        if (activation is not null)
        {
            HandleNativeNotificationActivation(activation.Arguments, activation.UserInput);
        }
    }

    private void HandleNativeNotificationActivation(
        string arguments,
        IReadOnlyDictionary<string, string> userInput)
    {
        if (UiDispatcherQueue is { HasThreadAccess: false } dispatcherQueue)
        {
            dispatcherQueue.TryEnqueue(() => HandleNativeNotificationActivation(arguments, userInput));
            return;
        }

        App.Log($"[Notification] Native notification activated args={arguments}");
        _ = RaiseTrayWidgetsAsync();
    }

    private static NativeAppNotificationActivation? TryGetCurrentNativeNotificationActivation()
    {
        try
        {
            var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            if (activatedArgs.Kind == ExtendedActivationKind.AppNotification &&
                activatedArgs.Data is AppNotificationActivatedEventArgs notificationArgs)
            {
                var userInput = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var input in notificationArgs.UserInput)
                {
                    if (!string.IsNullOrWhiteSpace(input.Key))
                    {
                        userInput[input.Key] = input.Value ?? string.Empty;
                    }
                }

                return new NativeAppNotificationActivation(notificationArgs.Argument, userInput);
            }
        }
        catch (Exception ex)
        {
            Log($"[Notification] Failed to read native notification activation args: {ex.Message}");
        }

        return null;
    }

    private static string? TryGetCurrentNativeNotificationActivationArguments()
    {
        return TryGetCurrentNativeNotificationActivation()?.Arguments;
    }

    private static void StorePendingNativeNotificationActivationArguments(string arguments)
    {
        try
        {
            Directory.CreateDirectory(BentoDeskDataPathService.Current.RootPath);
            string tempPath = Path.Combine(
                BentoDeskDataPathService.Current.RootPath,
                $"pending-notification-activation.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
            File.WriteAllText(tempPath, arguments);
            File.Move(tempPath, PendingNativeNotificationActivationPath, overwrite: true);
            Log($"[Notification] Forwarded native notification activation to running instance args={arguments}");
        }
        catch (Exception ex)
        {
            Log($"[Notification] Failed to forward native notification activation args: {ex}");
        }
    }

    private static string? TakePendingNativeNotificationActivationArguments()
    {
        try
        {
            if (!File.Exists(PendingNativeNotificationActivationPath))
            {
                return null;
            }

            string arguments = File.ReadAllText(PendingNativeNotificationActivationPath);
            File.Delete(PendingNativeNotificationActivationPath);
            return string.IsNullOrWhiteSpace(arguments) ? null : arguments;
        }
        catch (Exception ex)
        {
            Log($"[Notification] Failed to read forwarded native notification activation args: {ex}");
            return null;
        }
    }

    private static void StorePendingJumpListArgument(string argument)
    {
        try
        {
            Directory.CreateDirectory(BentoDeskDataPathService.Current.RootPath);
            File.WriteAllText(PendingJumpListArgumentPath, argument);
            Log($"[JumpList] Forwarded jump list activation to running instance arg={argument}");
        }
        catch (Exception ex)
        {
            Log($"[JumpList] Failed to forward jump list activation: {ex}");
        }
    }

    private static string? TakePendingJumpListArgument()
    {
        try
        {
            if (!File.Exists(PendingJumpListArgumentPath))
            {
                return null;
            }

            string argument = File.ReadAllText(PendingJumpListArgumentPath);
            File.Delete(PendingJumpListArgumentPath);
            return string.IsNullOrWhiteSpace(argument) ? null : argument;
        }
        catch (Exception ex)
        {
            Log($"[JumpList] Failed to read forwarded jump list activation: {ex}");
            return null;
        }
    }

    private void OnUpdateCheckCompleted(AppUpdateCheckResult result)
    {
        if (UiDispatcherQueue is { HasThreadAccess: false } dispatcherQueue)
        {
            dispatcherQueue.TryEnqueue(() => OnUpdateCheckCompleted(result));
            return;
        }

        if (result.IsUpdateAvailable && result.Manifest is not null)
        {
            SetUpdateAvailableReminder(result.Manifest);
        }
        else if (result.Status == AppUpdateCheckStatus.UpToDate)
        {
            ClearUpdateAvailableReminder();
        }

        _settingsWindow?.RefreshUpdateStateFromService();
    }

    private void SetUpdateAvailableReminder(AppUpdateManifest manifest)
    {
        _hasUpdateAvailable = true;
        _availableUpdateVersion = manifest.Version;
        RefreshTrayMenuText();
        RefreshTrayToolTipText();

        if (_updateNotificationShown || _trayIcon is null)
        {
            return;
        }

        try
        {
            _trayIcon.ShowNotification(
                LocalizationService.T("Tray.UpdateAvailableTitle"),
                LocalizationService.Format("Tray.UpdateAvailableMessage", manifest.Version),
                NotificationIcon.Info,
                customIconHandle: null,
                largeIcon: true,
                sound: false,
                respectQuietTime: true,
                realtime: false,
                timeout: TimeSpan.FromSeconds(8));
            _updateNotificationShown = true;
        }
        catch (Exception ex)
        {
            Log($"[Update] Tray notification failed: {ex.Message}");
        }
    }

    private void ClearUpdateAvailableReminder()
    {
        _hasUpdateAvailable = false;
        _availableUpdateVersion = string.Empty;
        RefreshTrayMenuText();
        RefreshTrayToolTipText();
    }

    private void RegisterActivationListener()
    {
        if (_activationEvent is null || _activationRegistration is not null)
        {
            return;
        }

        _activationRegistration = ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            static (_, _) =>
            {
                App.UiDispatcherQueue?.TryEnqueue(() =>
                {
                    _ = Current.HandleExternalActivationAsync();
                });
            },
            null,
            Timeout.Infinite,
            false);
    }

    private async Task HandleExternalActivationAsync()
    {
        Log("HandleExternalActivationAsync invoked");

        string? nativeNotificationActivationArguments = TakePendingNativeNotificationActivationArguments();
        if (!string.IsNullOrWhiteSpace(nativeNotificationActivationArguments))
        {
            HandleNativeNotificationActivation(
                nativeNotificationActivationArguments,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            return;
        }

        string? jumpListArgument = TakePendingJumpListArgument();
        if (!string.IsNullOrWhiteSpace(jumpListArgument))
        {
            await JumpListService.HandleActivationAsync(jumpListArgument);
            return;
        }

        if (WidgetManager is not null)
        {
            bool hasConfiguredWidgets = SettingsService.Settings.Widgets.Any(widget =>
                widget.WidgetKind == WidgetKind.File &&
                !widget.IsDisabled &&
                !SettingsService.Settings.DeletedWidgetIds.Contains(widget.Id));
            bool anyLoadedVisible = WidgetManager.Widgets.Values.Any(entry => entry.Window.Visible);

            if (hasConfiguredWidgets && !anyLoadedVisible)
            {
                await WidgetManager.SetAllWidgetsVisibleAsync(true);
            }
            else
            {
                var firstWidget = WidgetManager.Widgets.Values.FirstOrDefault().Window;
                firstWidget?.RevealFromTray();
            }
        }

        OpenSettings();
    }

    private void ShowDataRestoreResultNotification(BentoDeskRestoreApplyResult result)
    {
        if (!result.HadPendingRestore)
        {
            return;
        }

        string title = LocalizationService.T(result.Succeeded
            ? "Settings.DataBackup.RestoreAppliedTitle"
            : "Settings.DataBackup.RestoreApplyFailedTitle");
        string message = result.Succeeded
            ? LocalizationService.T("Settings.DataBackup.RestoreAppliedBody")
            : LocalizationService.Format(
                "Settings.DataBackup.RestoreApplyFailedBody",
                result.ErrorMessage ?? string.Empty);

        if (_nativeNotificationService?.TryShow(title, message) == true || _trayIcon is null)
        {
            return;
        }

        try
        {
            _trayIcon.ShowNotification(
                title,
                message,
                NotificationIcon.Info,
                customIconHandle: null,
                largeIcon: false,
                sound: false,
                respectQuietTime: true,
                realtime: false,
                timeout: TimeSpan.FromSeconds(7));
        }
        catch (Exception ex)
        {
            Log($"[DataBackup] Restore result notification failed: {ex.Message}");
        }
    }

    private void OnLanguageChanged()
    {
        Localized.RefreshAll(LocalizationService);
        RefreshTrayMenuText();
        RefreshTrayToolTipText();
    }

    private void OpenSettings()
    {
        var settingsWindow = _settingsWindow ?? CreateSettingsWindow();
        settingsWindow.ShowWindow();
    }

    private SettingsWindow CreateSettingsWindow()
    {
        _settingsWindow = new SettingsWindow(SettingsService, ThemeService, LocalizationService);
        _settingsWindow.Closed += (_, _) =>
        {
            _settingsWindow = null;
            ScheduleLightMemoryCleanup();
        };
        return _settingsWindow;
    }

    public void RefreshSettingsWindow()
    {
        if (_settingsWindow is null)
        {
            OpenSettings();
            return;
        }

        _settingsWindow.RefreshLocalizedContent();
    }

    public void ShowSettings()
    {
        OpenSettings();
    }

    public void ShowSettings(string sectionTag)
    {
        var settingsWindow = _settingsWindow ?? CreateSettingsWindow();
        settingsWindow.ShowWindow();
        settingsWindow.ShowSection(sectionTag);
    }

    public void ShowOnboarding()
    {
        bool shouldRestartIntro = _onboardingWindow is not null;
        if (_onboardingWindow is null)
        {
            _onboardingWindow = new OnboardingWindow(SettingsService, LocalizationService);
            _onboardingWindow.Closed += (_, _) =>
            {
                _onboardingWindow = null;
                ScheduleLightMemoryCleanup();
            };
            ThemeService.TrackWindow(_onboardingWindow);
        }

        _onboardingWindow.Activate();
        if (shouldRestartIntro)
        {
            _onboardingWindow.RestartIntro();
        }
    }

    private static int s_lightMemoryCleanupGeneration;

    internal static void ScheduleLightMemoryCleanup()
    {
        int generation = Interlocked.Increment(ref s_lightMemoryCleanupGeneration);
        App.UiDispatcherQueue?.TryEnqueue(async () =>
        {
            await Task.Delay(2000);
            if (generation != Volatile.Read(ref s_lightMemoryCleanupGeneration))
            {
                return;
            }

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: false);
            Localized.PruneDeadTargets();
            Win32Helper.TrimCurrentProcessWorkingSet();
        });
    }

    public async Task ShutdownForUpdateAsync()
    {
        Log("ShutdownForUpdateAsync invoked");
        await ShutdownApplicationAsync();
    }

    public async Task ShutdownForRestartAsync()
    {
        Log("ShutdownForRestartAsync invoked");
        await ShutdownApplicationAsync();
    }

    private async void ExitApplication()
    {
        Log("ExitApplication invoked");
        await ShutdownApplicationAsync();
    }

    private async Task ShutdownApplicationAsync()
    {
        // Stop the display area watcher FIRST, before closing any widgets,
        // so that no DisplaysChanged callback can fire during teardown
        // and access half-closed window objects.
        _displayAreaWatcher?.Dispose();
        _displayAreaWatcher = null;

        _diagnosticsService?.Dispose();
        _diagnosticsService = null;

        DesktopDoubleClickService?.Dispose();
        DesktopDoubleClickService = null;

        // Restore native ListView before tearing down Guard.
        FreeDesktopService?.Stop();
        FreeDesktopService?.Dispose();
        FreeDesktopService = null;
        DesktopGuardHostService?.Dispose();
        DesktopGuardHostService = null;
        DesktopShellIconService?.Dispose();
        DesktopShellIconService = null;

        await SettingsService.SaveAsync();
        _nativeNotificationService?.Dispose();
        _nativeNotificationService = null;
        WidgetManager?.CloseAll();
        _trayIcon?.Dispose();
        _trayIcon = null;
        _activationRegistration?.Unregister(null);
        _activationRegistration = null;
        _activationEvent?.Dispose();
        _activationEvent = null;

        try
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }

        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;
        _settingsWindow?.CloseForShutdown();
        _settingsWindow = null;
        _onboardingWindow?.Close();
        _onboardingWindow = null;
        _trayWindow?.Close();
        _trayWindow = null;
        Exit();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Log($"Unhandled exception: {e.Exception}");
        e.Handled = true;
    }
}
