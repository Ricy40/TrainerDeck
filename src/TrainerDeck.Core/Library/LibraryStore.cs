using TrainerDeck.Core.Storage;

namespace TrainerDeck.Core.Library;

public sealed class TrainerMetadata
{
    public string RelativePath { get; set; } = "";

    public string? DisplayName { get; set; }

    public string? ArtPath { get; set; }

    public bool Hidden { get; set; }

    public bool Favourite { get; set; }

    public DateTime FirstSeenUtc { get; set; }

    public DateTime? LastLaunchedUtc { get; set; }

    public int LaunchCount { get; set; }

    public int? SteamAppId { get; set; }

    public bool SteamAppIdLocked { get; set; }
}

public sealed class LibraryDocument
{
    public int Version { get; set; } = 1;

    public Dictionary<string, TrainerMetadata> Entries { get; set; } = new(StringComparer.Ordinal);
}

public sealed class LibraryStore
{
    private readonly object _gate = new();
    private readonly LibraryDocument _doc;

    private LibraryStore(string path, LibraryDocument doc)
    {
        FilePath = path;
        _doc = doc;
    }

    public string FilePath { get; }

    public static LibraryStore Load(string path, Action<string>? onWarning = null)
    {
        var doc = JsonFile.LoadOrDefault<LibraryDocument>(path, onWarning);
        doc.Entries = new Dictionary<string, TrainerMetadata>(doc.Entries ?? new(), StringComparer.Ordinal);
        return new LibraryStore(path, doc);
    }

    public TrainerMetadata GetOrCreate(TrainerFile trainer, DateTime nowUtc)
    {
        lock (_gate)
        {
            string rel = TrainerId.NormalizeRelativePath(trainer.RelativePath);
            if (_doc.Entries.TryGetValue(trainer.Id, out var existing))
            {
                existing.RelativePath = rel;
                return existing;
            }

            var moved = _doc.Entries.FirstOrDefault(kv => kv.Value.RelativePath == rel);
            if (moved.Value is not null)
            {
                _doc.Entries.Remove(moved.Key);
                _doc.Entries[trainer.Id] = moved.Value;
                return moved.Value;
            }

            var created = new TrainerMetadata { RelativePath = rel, FirstSeenUtc = nowUtc };
            _doc.Entries[trainer.Id] = created;
            return created;
        }
    }

    public TrainerMetadata? TryGet(string id)
    {
        lock (_gate)
        {
            return _doc.Entries.GetValueOrDefault(id);
        }
    }

    public bool Update(string id, Action<TrainerMetadata> change)
    {
        lock (_gate)
        {
            if (!_doc.Entries.TryGetValue(id, out var meta))
            {
                return false;
            }

            change(meta);
            return true;
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _doc.Entries.Count;
            }
        }
    }

    public void Save()
    {
        lock (_gate)
        {
            JsonFile.Save(FilePath, _doc);
        }
    }
}
