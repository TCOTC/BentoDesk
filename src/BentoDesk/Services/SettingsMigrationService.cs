using BentoDesk.Models;

namespace BentoDesk.Services;

/// <summary>
/// Defines a single settings migration step from one schema version to the next.
/// </summary>
public interface ISettingsMigration
{
    /// <summary>The source schema version this migration upgrades from.</summary>
    int FromVersion { get; }

    /// <summary>Applies the migration to the given settings instance.</summary>
    void Migrate(AppSettings settings);
}

/// <summary>
/// Pipeline that executes registered settings migrations in version order.
/// </summary>
public sealed class SettingsMigrationPipeline
{
    /// <summary>The current schema version that the application expects.</summary>
    public const int CurrentSchemaVersion = 1;

    private readonly List<ISettingsMigration> _migrations = [];

    /// <summary>
    /// Runs all necessary migrations to bring the settings from their current
    /// schema version up to <see cref="CurrentSchemaVersion"/>.
    /// Returns true if any migration was applied.
    /// </summary>
    public bool RunMigrations(AppSettings settings)
    {
        if (settings.SchemaVersion >= CurrentSchemaVersion)
        {
            return false;
        }

        bool anyApplied = false;
        int version = settings.SchemaVersion;

        foreach (var migration in _migrations.OrderBy(m => m.FromVersion))
        {
            if (migration.FromVersion >= version && migration.FromVersion < CurrentSchemaVersion)
            {
                try
                {
                    migration.Migrate(settings);
                    version = migration.FromVersion + 1;
                    anyApplied = true;
                    App.Log($"[SettingsMigration] Applied migration from version {migration.FromVersion} to {version}");
                }
                catch (Exception ex)
                {
                    App.Log($"[SettingsMigration] Migration from {migration.FromVersion} failed: {ex.Message}");
                }
            }
        }

        settings.SchemaVersion = CurrentSchemaVersion;
        return anyApplied || settings.SchemaVersion != version;
    }
}
