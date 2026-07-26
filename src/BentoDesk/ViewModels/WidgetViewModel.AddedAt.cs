using BentoDesk.Models;

namespace BentoDesk.ViewModels;

public partial class WidgetViewModel
{
    private void ApplyPersistedAddedTimes(IReadOnlyList<WidgetItem> items)
    {
        EnsureAddedAtDictionaryComparer();
        bool changed = false;
        var currentPaths = items
            .Select(item => item.Path)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (Config.FileAddedAtByPath.TryGetValue(item.Path, out DateTimeOffset addedAt))
            {
                item.AddedAt = addedAt;
                continue;
            }

            addedAt = DateTimeOffset.Now;
            Config.FileAddedAtByPath[item.Path] = addedAt;
            item.AddedAt = addedAt;
            changed = true;
        }

        foreach (string stalePath in Config.FileAddedAtByPath.Keys
                     .Where(path => !currentPaths.Contains(path))
                     .ToArray())
        {
            Config.FileAddedAtByPath.Remove(stalePath);
            changed = true;
        }

        if (changed)
        {
            PersistAddedAtTracking();
        }
    }

    private void AssignAddedAt(WidgetItem item, DateTimeOffset? preferred = null)
    {
        EnsureAddedAtDictionaryComparer();
        if (!Config.FileAddedAtByPath.TryGetValue(item.Path, out DateTimeOffset addedAt))
        {
            addedAt = preferred ?? DateTimeOffset.Now;
            Config.FileAddedAtByPath[item.Path] = addedAt;
            PersistAddedAtTracking();
        }

        item.AddedAt = addedAt;
    }

    private void RecordFileAddedAt(string path, DateTimeOffset addedAt)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        EnsureAddedAtDictionaryComparer();
        Config.FileAddedAtByPath[path] = addedAt;
        PersistAddedAtTracking();
    }

    private void TransferFileAddedAt(string oldPath, string newPath)
    {
        EnsureAddedAtDictionaryComparer();
        DateTimeOffset addedAt = Config.FileAddedAtByPath.TryGetValue(oldPath, out var existing)
            ? existing
            : DateTimeOffset.Now;
        Config.FileAddedAtByPath.Remove(oldPath);
        Config.FileAddedAtByPath[newPath] = addedAt;
        PersistAddedAtTracking();
    }

    private void RemoveFileAddedAt(string path)
    {
        EnsureAddedAtDictionaryComparer();
        if (Config.FileAddedAtByPath.Remove(path))
        {
            PersistAddedAtTracking();
        }
    }

    private void ResetAddedAtTracking()
    {
        Config.FileAddedAtByPath = new Dictionary<string, DateTimeOffset>(
            StringComparer.OrdinalIgnoreCase);
    }

    private void EnsureAddedAtDictionaryComparer()
    {
        if (Config.FileAddedAtByPath is null)
        {
            Config.FileAddedAtByPath = new Dictionary<string, DateTimeOffset>(
                StringComparer.OrdinalIgnoreCase);
            return;
        }

        if (Config.FileAddedAtByPath.Comparer.Equals(StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        Config.FileAddedAtByPath = new Dictionary<string, DateTimeOffset>(
            Config.FileAddedAtByPath,
            StringComparer.OrdinalIgnoreCase);
    }

    private void PersistAddedAtTracking()
    {
        _settingsService.UpdateWidget(Config, notifySubscribers: false);
    }
}
