namespace BentoDesk.Services;

/// <summary>
/// Compatibility facade for registry-based startup registration.
/// </summary>
public static class StartupService
{
    private static readonly IStartupService s_current = new DirectStartupService();

    public static IStartupService Current => s_current;

    /// <summary>
    /// Check if BentoDesk is registered for auto-start.
    /// </summary>
    public static bool IsEnabled()
    {
        try
        {
            return s_current.IsEnabled();
        }
        catch
        {
            return false;
        }
    }

    public static string? GetRunValue()
    {
        try
        {
            return s_current.GetRunValue();
        }
        catch
        {
            return null;
        }
    }

    public static void Enable()
    {
        try
        {
            s_current.Enable();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StartupService] Failed to enable startup: {ex.Message}");
        }
    }

    public static void Disable()
    {
        try
        {
            s_current.Disable();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StartupService] Failed to disable startup: {ex.Message}");
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            s_current.SetEnabled(enabled);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StartupService] Failed to set startup: {ex.Message}");
        }
    }
}
