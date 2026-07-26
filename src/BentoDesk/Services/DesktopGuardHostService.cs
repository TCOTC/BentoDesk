using System.Diagnostics;

namespace BentoDesk.Services;

/// <summary>
/// Starts and stops <c>BentoDesk.DesktopGuard.exe</c> for crash-safe native icon restore.
/// </summary>
public sealed class DesktopGuardHostService : IDisposable
{
    private readonly object _lock = new();
    private Process? _guardProcess;
    private bool _disposed;

    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _guardProcess is { HasExited: false };
            }
        }
    }

    public void Start(int parentProcessId)
    {
        if (_disposed)
        {
            return;
        }

        string? exePath = ResolveGuardExecutablePath();
        if (exePath is null)
        {
            App.Log("[DesktopGuardHost] BentoDesk.DesktopGuard.exe not found; crash restore unavailable");
            return;
        }

        lock (_lock)
        {
            if (_guardProcess is { HasExited: false })
            {
                return;
            }

            try
            {
                _guardProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"--pid {parentProcessId}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                App.Log($"[DesktopGuardHost] Started pid={_guardProcess?.Id} for parent={parentProcessId}");
            }
            catch (Exception ex)
            {
                App.Log($"[DesktopGuardHost] Start failed: {ex}");
                _guardProcess = null;
            }
        }
    }

    /// <summary>
    /// Stops the guard after the main process has already restored native icons.
    /// Killing the guard is safe because restore is idempotent.
    /// </summary>
    public void Stop()
    {
        Process? process;
        lock (_lock)
        {
            process = _guardProcess;
            _guardProcess = null;
        }

        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: false);
                _ = process.WaitForExit(2000);
            }
        }
        catch (Exception ex)
        {
            App.Log($"[DesktopGuardHost] Stop failed: {ex.Message}");
        }
        finally
        {
            process.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }

    private static string? ResolveGuardExecutablePath()
    {
        string baseDir = AppContext.BaseDirectory;
        string candidate = Path.Combine(baseDir, "BentoDesk.DesktopGuard.exe");
        if (File.Exists(candidate))
        {
            return candidate;
        }

        return null;
    }
}
