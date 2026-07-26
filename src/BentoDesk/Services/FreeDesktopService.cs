namespace BentoDesk.Services;

/// <summary>
/// Painted-desktop orchestration without a free-icon layer: hide SysListView32 and host Guard.
/// Uncategorized desktop files are shown in the default managed widget instead.
/// </summary>
public sealed class FreeDesktopService : IDisposable
{
    private readonly DesktopShellIconService _shellIcons;
    private readonly DesktopGuardHostService _guardHost;
    private bool _isActive;
    private bool _disposed;

    public FreeDesktopService(
        DesktopShellIconService shellIcons,
        DesktopGuardHostService guardHost)
    {
        _shellIcons = shellIcons;
        _guardHost = guardHost;
    }

    public bool IsActive => _isActive;

    public Task StartAsync()
    {
        if (_disposed || _isActive)
        {
            return Task.CompletedTask;
        }

        _isActive = true;
        _shellIcons.RequestHidden(DesktopShellIconService.HideReason.PaintedDesktop);
        _guardHost.Start(Environment.ProcessId);
        App.Log("[FreeDesktop] Painted desktop mode started (shell hide + Guard; uncategorized widget)");
        return Task.CompletedTask;
    }

    public void Stop()
    {
        if (!_isActive)
        {
            return;
        }

        _isActive = false;
        _shellIcons.ReleaseHidden(DesktopShellIconService.HideReason.PaintedDesktop);
        _guardHost.Stop();
        App.Log("[FreeDesktop] Painted desktop mode stopped");
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
}
