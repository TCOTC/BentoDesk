using System.Diagnostics;
using BentoDesk.Models;

namespace BentoDesk.Services;

/// <summary>
/// Coordinates search across all layers: BentoDesk internal data, custom file index,
/// and (future) Windows Search Index.
/// </summary>
public sealed class SearchEngineService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly LocalizationService _localizationService;
    private readonly SearchIndexService _indexService;
    private readonly WindowsIndexSearchService _windowsIndexService;
    private readonly UsnJournalIndexService? _usnIndexService;
    private bool _isDisposed;

    public SearchEngineService(
        SettingsService settingsService,
        LocalizationService localizationService,
        SearchIndexService indexService,
        WindowsIndexSearchService windowsIndexService,
        UsnJournalIndexService? usnIndexService = null)
    {
        _settingsService = settingsService;
        _localizationService = localizationService;
        _indexService = indexService;
        _windowsIndexService = windowsIndexService;
        _usnIndexService = usnIndexService;
        _indexService.IndexUpdated += OnIndexUpdated;
        _indexService.ProgressChanged += OnIndexProgressChanged;
        if (_usnIndexService is not null)
        {
            _usnIndexService.IndexUpdated += OnIndexUpdated;
            _usnIndexService.ProgressChanged += OnIndexProgressChanged;
        }
    }

    public SearchIndexService IndexService => _indexService;

    public int IndexedItemCount => _usnIndexService is { IsAvailable: true }
        ? _usnIndexService.EntryCount
        : _indexService.EntryCount;

    public bool IsCustomIndexing => _indexService.IsScanning ||
                                    _usnIndexService is { IsScanning: true };

    public bool IsIndexPaused => _indexService.IsPaused ||
                                 _usnIndexService is { IsPaused: true };

    public DateTime? LastScanTime => _indexService.LastScanTime;

    public event Action? IndexUpdated;

    /// <summary>Raised periodically during indexing with the current total entry count.</summary>
    public event Action<int>? IndexProgressChanged;

    private void OnIndexUpdated() => IndexUpdated?.Invoke();

    private void OnIndexProgressChanged(int _)
    {
        // Aggregate both services' counts and forward to subscribers.
        IndexProgressChanged?.Invoke(IndexedItemCount);
    }

    public void SetCustomIndexingEnabled(bool enabled)
    {
        if (enabled)
        {
            _indexService.StartIndexing();
            _usnIndexService?.StartIndexing();
        }
        else
        {
            _indexService.StopIndexing();
            _usnIndexService?.StopIndexing();
        }
    }

    /// <summary>Pauses all in-progress indexing.</summary>
    public void PauseIndexing()
    {
        _indexService.PauseIndexing();
        _usnIndexService?.PauseIndexing();
    }

    /// <summary>Resumes paused indexing.</summary>
    public void ResumeIndexing()
    {
        _indexService.ResumeIndexing();
        _usnIndexService?.ResumeIndexing();
    }

    /// <summary>Clears and rebuilds the index from scratch.</summary>
    public void RebuildIndex()
    {
        _indexService.RebuildIndex();
        // USN journal index is ephemeral (no disk persistence); just restart it.
        if (_usnIndexService is not null)
        {
            _usnIndexService.StopIndexing();
            _usnIndexService.StartIndexing();
        }
    }

    /// <summary>Returns the on-disk storage size (bytes) of the persisted index.</summary>
    public long GetIndexStorageBytes() => _indexService.GetIndexStorageBytes();

    /// <summary>
    /// Performs a unified search across all enabled layers.
    /// </summary>
    public async Task<SearchResponse> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var settings = _settingsService.Settings;
        int maxResults = Math.Clamp(settings.SearchMaxResults, 10, 200);

        var providerTasks = new List<Task<IReadOnlyList<SearchResultItem>>>();

        // Start every enabled provider together.
        providerTasks.Add(Task.FromResult(SearchActions(query)));

        // Layer 2: Windows Search Index (system-indexed locations)
        if (settings.SearchIncludeSystemIndex)
        {
            providerTasks.Add(_windowsIndexService.SearchAsync(query, maxResults, cancellationToken));
        }

        // Layer 3: File index. Prefer the USN journal full-disk index when it is
        // available (elevated); otherwise fall back to the directory-scan index, which
        // now covers every fixed drive so coverage stays broad without admin.
        if (settings.SearchCustomIndexerEnabled)
        {
            providerTasks.Add(_usnIndexService is { IsAvailable: true }
                ? Task.Run(() => _usnIndexService.Search(query, maxResults), cancellationToken)
                : Task.Run(() => _indexService.Search(query, maxResults), cancellationToken));
        }

        IReadOnlyList<SearchResultItem>[] providerResults = await Task.WhenAll(providerTasks);
        cancellationToken.ThrowIfCancellationRequested();

        var rankedItems = SearchResultRanker.MergeAndRank(
            providerResults.SelectMany(items => items),
            query.Trim(),
            maxResults);
        var groups = BuildGroups(rankedItems);
        stopwatch.Stop();

        return new SearchResponse
        {
            Query = query,
            RankedItems = rankedItems,
            Groups = groups,
            TotalResultCount = rankedItems.Count,
            Elapsed = stopwatch.Elapsed,
            IsComplete = true
        };
    }

    /// <summary>
    /// Gets recommendations for the empty-state view.
    /// </summary>
    public async Task<IReadOnlyList<SearchRecommendationItem>> GetRecommendationsAsync(
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(
            () => BuildApplicationRecommendations(cancellationToken),
            cancellationToken);
    }

    private IReadOnlyList<SearchRecommendationItem> BuildApplicationRecommendations(
        CancellationToken cancellationToken)
    {
        var recommendations = new List<SearchRecommendationItem>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddShortcut(string path, string subtitle)
        {
            if (cancellationToken.IsCancellationRequested ||
                !path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(path))
            {
                return;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch
            {
                return;
            }

            if (!seenPaths.Add(fullPath))
            {
                return;
            }

            recommendations.Add(new SearchRecommendationItem
            {
                Kind = SearchResultKind.File,
                Title = Path.GetFileName(fullPath),
                Subtitle = subtitle,
                DetailPath = fullPath
            });
        }

        // The user's widgets are an explicit curation signal, so every shortcut shown
        // by an enabled file widget comes before generic Start menu applications.
        foreach (var widget in _settingsService.Settings.Widgets
                     .Where(widget => widget.WidgetKind == WidgetKind.File && !widget.IsDisabled))
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var item in widget.Items.OrderBy(item => item.SortOrder))
            {
                AddShortcut(item.Path, widget.Name);
            }

            if (!string.IsNullOrWhiteSpace(widget.MappedFolderPath))
            {
                foreach (string shortcut in EnumerateShortcutFilesSafely(
                             widget.MappedFolderPath, recursive: false, cancellationToken))
                {
                    AddShortcut(shortcut, widget.Name);
                }
            }
        }

        string startMenuLabel = _localizationService.T("Search.Recommend.StartMenu");
        string[] startMenuRoots =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
        ];

        const int MaxStartMenuApps = 40;
        int startMenuCount = 0;
        foreach (string root in startMenuRoots
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (string shortcut in EnumerateShortcutFilesSafely(
                         root, recursive: true, cancellationToken)
                     .OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase))
            {
                int before = recommendations.Count;
                AddShortcut(shortcut, startMenuLabel);
                if (recommendations.Count > before && ++startMenuCount >= MaxStartMenuApps)
                {
                    return recommendations;
                }
            }
        }

        return recommendations;
    }

    private static IEnumerable<string> EnumerateShortcutFilesSafely(
        string root,
        bool recursive,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            string current = pending.Pop();
            string[] files;
            try
            {
                files = Directory.GetFiles(current, "*.lnk", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            foreach (string file in files)
            {
                yield return file;
            }

            if (!recursive)
            {
                continue;
            }

            try
            {
                foreach (string directory in Directory.GetDirectories(current))
                {
                    pending.Push(directory);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // Keep results already found in accessible Start menu folders.
            }
        }
    }

    private IReadOnlyList<SearchResultItem> SearchActions(string query)
    {
        var actions = new (string Id, string NameKey, string Glyph)[]
        {
            ("open-settings", "Search.Action.OpenSettings", "\uE713"),
            ("toggle-widgets", "Search.Action.ToggleWidgets", "\uE8A5"),
            ("toggle-theme", "Search.Action.ToggleTheme", "\uE793")
        };

        var results = new List<SearchResultItem>();
        foreach (var (id, nameKey, glyph) in actions)
        {
            string name = _localizationService.T(nameKey);
            if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new SearchResultItem
                {
                    Kind = SearchResultKind.Action,
                    Title = name,
                    ActionId = id,
                    Glyph = glyph,
                    RelevanceScore = ComputeTextRelevance(name, query) + 5
                });
            }
        }

        return results;
    }

    private IReadOnlyList<SearchResultGroup> BuildGroups(
        IReadOnlyList<SearchResultItem> rankedResults)
    {
        var groups = new List<SearchResultGroup>();

        var groupOrder = new[]
        {
            (SearchResultKind.Action, _localizationService.T("Search.Group.Actions")),
            (SearchResultKind.File, _localizationService.T("Search.Group.Files")),
            (SearchResultKind.Folder, _localizationService.T("Search.Group.Folders"))
        };

        foreach (var (kind, displayName) in groupOrder)
        {
            var items = rankedResults
                .Where(r => r.Kind == kind)
                .ToList();

            if (items.Count > 0)
            {
                groups.Add(new SearchResultGroup
                {
                    Kind = kind,
                    DisplayName = displayName,
                    Items = items,
                    TotalCount = items.Count
                });
            }
        }

        return groups;
    }

    private static double ComputeTextRelevance(string text, string query)
    {
        if (text.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        if (text.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 80;
        }

        if (text.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 50;
        }

        return 30;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _indexService.IndexUpdated -= OnIndexUpdated;
        if (_usnIndexService is not null)
        {
            _usnIndexService.IndexUpdated -= OnIndexUpdated;
        }
        _indexService.Dispose();
    }
}
