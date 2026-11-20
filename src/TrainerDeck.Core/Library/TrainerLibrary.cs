using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Settings;

namespace TrainerDeck.Core.Library;


public sealed class TrainerLibrary : IDisposable
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(1500);

    private readonly SettingsStore _settings;
    private readonly PortablePaths _paths;
    private readonly Func<DateTime> _utcNow;
    private readonly SemaphoreSlim _scanLock = new(1, 1);
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly Timer _debounce;
    private readonly object _watchGate = new();
    private int _rescanRequested;
    private bool _watching;
    private string[] _watchedRoots = [];
    private bool _disposed;

    public TrainerLibrary(SettingsStore settings, PortablePaths paths, LibraryStore store, Func<DateTime>? utcNow = null)
    {
        _settings = settings;
        _paths = paths;
        Store = store;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _debounce = new Timer(_ => _ = RescanAsync(), null, Timeout.Infinite, Timeout.Infinite);
        _settings.Changed += OnSettingsChanged;
    }

    public LibraryStore Store { get; }

    public IReadOnlyList<TrainerEntry> Entries { get; private set; } = [];

    public IReadOnlyList<string> PendingZips { get; private set; } = [];

    public IReadOnlyList<string> LastErrors { get; private set; } = [];

    public bool IsScanning { get; private set; }

    public IReadOnlyList<string> Roots => _settings.Current.EffectiveTrainerFolders(_paths);

    public event EventHandler? Changed;

    public async Task RescanAsync()
    {
        if (!await _scanLock.WaitAsync(0).ConfigureAwait(false))
        {
            Volatile.Write(ref _rescanRequested, 1);
            return;
        }

        try
        {
            do
            {
                Volatile.Write(ref _rescanRequested, 0);
                await Task.Run(ScanOnce).ConfigureAwait(false);
            }
            while (Volatile.Read(ref _rescanRequested) == 1 && !_disposed);
        }
        finally
        {
            _scanLock.Release();
        }
    }

    private void ScanOnce()
    {
        IsScanning = true;
        RaiseChanged();
        try
        {
            string[] roots = Roots.ToArray();
            if (_settings.Current.TrainerFolders.Count == 0)
            {
                TryCreate(_paths.DefaultTrainersDirectory);
            }

            var started = DateTime.UtcNow;
            var scanner = new TrainerScanner(new IgnoreRules(_settings.Current.IgnorePatterns));
            ScanResult result = scanner.Scan(roots);

            var liveIds = result.Trainers.Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
            DateTime now = _utcNow();
            Entries = result.Trainers.Select(t => new TrainerEntry(t, Store.GetOrCreate(t, now, liveIds.Contains))).ToList();
            PendingZips = result.PendingZips;
            LastErrors = result.Errors;

            foreach (string error in result.Errors)
            {
                Log.Warn($"Scan: {error}");
            }

            Log.Info($"Scan found {Entries.Count} trainer(s), {PendingZips.Count} zip(s) in {(DateTime.UtcNow - started).TotalMilliseconds:F0} ms across {roots.Length} folder(s).");
            TrySave();
        }
        catch (Exception ex)
        {
            Log.Error("Scan failed", ex);
            LastErrors = [ex.Message];
        }
        finally
        {
            IsScanning = false;
            RaiseChanged();
        }
    }

    public void SaveMetadata() => TrySave();

    private void TrySave()
    {
        try
        {
            Store.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error("Could not save library.json", ex);
        }
    }

    public void StartWatching()
    {
        lock (_watchGate)
        {
            _watching = true;
            RebuildWatchers();
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        string[] roots = Roots.ToArray();
        lock (_watchGate)
        {
            if (roots.SequenceEqual(_watchedRoots, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            if (_watching)
            {
                RebuildWatchers();
            }
        }

        _ = RescanAsync();
    }

    private void RebuildWatchers()
    {
        foreach (var w in _watchers)
        {
            w.Dispose();
        }

        _watchers.Clear();
        _watchedRoots = Roots.ToArray();
        foreach (string root in _watchedRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            try
            {
                var w = new FileSystemWatcher(root)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.LastWrite,
                    InternalBufferSize = 64 * 1024,
                };
                w.Created += OnFileSystemEvent;
                w.Deleted += OnFileSystemEvent;
                w.Changed += OnFileSystemEvent;
                w.Renamed += OnFileSystemEvent;
                w.Error += (_, args) =>
                {
                    Log.Warn($"Watcher error on {root}: {args.GetException().Message}; rescanning.");
                    ScheduleRescan();
                };
                w.EnableRaisingEvents = true;
                _watchers.Add(w);
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
            {
                Log.Warn($"Cannot watch {root}: {ex.Message}");
            }
        }
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        string ext = Path.GetExtension(e.FullPath);
        bool relevant = ext.Length == 0 // probably a directory
            || ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".zip", StringComparison.OrdinalIgnoreCase)
            || (e is RenamedEventArgs r && Path.GetExtension(r.OldFullPath).Equals(".exe", StringComparison.OrdinalIgnoreCase));
        if (relevant)
        {
            ScheduleRescan();
        }
    }

    private void ScheduleRescan()
    {
        if (!_disposed)
        {
            _debounce.Change(DebounceDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private void RaiseChanged()
    {
        try
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error("Library change handler failed", ex);
        }
    }

    private static void TryCreate(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"Could not create {dir}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _settings.Changed -= OnSettingsChanged;
        _debounce.Dispose();
        lock (_watchGate)
        {
            foreach (var w in _watchers)
            {
                w.Dispose();
            }

            _watchers.Clear();
        }
    }
}
