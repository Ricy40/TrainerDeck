namespace TrainerDeck.Core.Launch;

public sealed record RunningProcess(string TrainerId, int ProcessId, DateTime StartedUtc);

public sealed class RunningTrainers
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RunningProcess> _byId = new(StringComparer.Ordinal);

    public event EventHandler? Changed;

    public bool Add(RunningProcess process)
    {
        lock (_gate)
        {
            if (_byId.TryGetValue(process.TrainerId, out var existing) && existing.ProcessId == process.ProcessId)
            {
                return false;
            }

            _byId[process.TrainerId] = process;
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Remove(string trainerId, int processId)
    {
        lock (_gate)
        {
            if (!_byId.TryGetValue(trainerId, out var existing) || existing.ProcessId != processId)
            {
                return false;
            }

            _byId.Remove(trainerId);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool IsRunning(string trainerId)
    {
        lock (_gate)
        {
            return _byId.ContainsKey(trainerId);
        }
    }

    public RunningProcess? TryGet(string trainerId)
    {
        lock (_gate)
        {
            return _byId.GetValueOrDefault(trainerId);
        }
    }

    public bool IsTracked(int processId)
    {
        lock (_gate)
        {
            return _byId.Values.Any(p => p.ProcessId == processId);
        }
    }

    public IReadOnlyList<RunningProcess> Snapshot()
    {
        lock (_gate)
        {
            return _byId.Values.ToList();
        }
    }
}

public static class ProcessMatcher
{
    public static Dictionary<string, int> Match(
        IEnumerable<(int ProcessId, string ImagePath)> processes,
        IEnumerable<(string TrainerId, string FullPath)> trainers)
    {
        var byPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, path) in trainers)
        {
            byPath.TryAdd(Normalize(path), id);
        }

        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (pid, image) in processes)
        {
            if (!string.IsNullOrEmpty(image) && byPath.TryGetValue(Normalize(image), out string? id))
            {
                result.TryAdd(id, pid);
            }
        }

        return result;
    }

    public static string Normalize(string path)
    {
        string p = path.Trim();
        if (p.StartsWith(@"\\?\UNC\", StringComparison.Ordinal))
        {
            p = @"\\" + p[8..];
        }
        else if (p.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            p = p[4..];
        }

        return p.Replace('/', '\\').TrimEnd('\\');
    }
}
