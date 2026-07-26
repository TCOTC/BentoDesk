using Windows.ApplicationModel;

namespace BentoDesk.Services;

public sealed class StoreStartupService : IStartupService
{
    private const string StartupTaskId = "BentoDeskStartupTask";

    public bool IsEnabled()
    {
        try
        {
            var task = GetStartupTask();
            return task.State == StartupTaskState.Enabled;
        }
        catch (Exception ex)
        {
            global::BentoDesk.App.Log($"[StoreStartupService] Failed to query startup state: {ex.Message}");
            return false;
        }
    }

    public string? GetRunValue() => null;

    public void Enable()
    {
        try
        {
            var task = GetStartupTask();
            if (task.State == StartupTaskState.Enabled)
            {
                return;
            }

            if (task.State == StartupTaskState.Disabled)
            {
                // Fire-and-forget: RequestEnableAsync may show a consent dialog
                // that requires the UI thread. Blocking with GetAwaiter().GetResult()
                // would dead-lock the UI thread.
                _ = task.RequestEnableAsync().AsTask().ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        global::BentoDesk.App.Log($"[StoreStartupService] StartupTask enable requested: {t.Result}");
                    }
                    else if (t.IsFaulted)
                    {
                        global::BentoDesk.App.Log($"[StoreStartupService] StartupTask enable failed: {t.Exception?.GetBaseException()?.Message}");
                    }
                }, TaskScheduler.Default);
                return;
            }

            global::BentoDesk.App.Log($"[StoreStartupService] StartupTask cannot be enabled from app state: {task.State}");
        }
        catch (Exception ex)
        {
            global::BentoDesk.App.Log($"[StoreStartupService] Failed to enable startup: {ex.Message}");
        }
    }

    public void Disable()
    {
        try
        {
            var task = GetStartupTask();
            if (task.State == StartupTaskState.Enabled)
            {
                task.Disable();
            }
        }
        catch (Exception ex)
        {
            global::BentoDesk.App.Log($"[StoreStartupService] Failed to disable startup: {ex.Message}");
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            Enable();
            return;
        }

        Disable();
    }

    private static StartupTask GetStartupTask()
    {
        return StartupTask.GetAsync(StartupTaskId).AsTask().GetAwaiter().GetResult();
    }
}
