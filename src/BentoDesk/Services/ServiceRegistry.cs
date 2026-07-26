using Microsoft.Extensions.DependencyInjection;

namespace BentoDesk.Services;

/// <summary>
/// Central DI registration for all core BentoDesk services.
/// All services use Singleton lifetime (desktop app = single process).
/// </summary>
public static class ServiceRegistry
{
    /// <summary>
    /// Registers all core application services into the given service collection.
    /// </summary>
    public static IServiceCollection AddBentoDeskServices(this IServiceCollection services)
    {
        // ── Core infrastructure ──────────────────────────────────────────
        services.AddSingleton<SettingsService>();
        services.AddSingleton<SettingsMigrationPipeline>();
        services.AddSingleton<BentoDeskDataBackupService>();
        services.AddSingleton<FileService>();
        services.AddSingleton<ResizeGuideOverlayService>();

        // ── Feature services ─────────────────────────────────────────────
        services.AddSingleton<OrganizerService>(sp =>
            new OrganizerService(
                sp.GetRequiredService<SettingsService>(),
                sp.GetRequiredService<FileService>()));
        services.AddSingleton<LocalizationService>();
        services.AddSingleton<ThemeService>();

        // ── Update ───────────────────────────────────────────────────────
        services.AddSingleton<IAppUpdateService, AppUpdateService>();

        return services;
    }
}
