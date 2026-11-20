using TrainerDeck.Art;
using TrainerDeck.Core.Cheats;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Input;
using TrainerDeck.Core.Launch;
using TrainerDeck.Core.Library;
using TrainerDeck.Core.Settings;
using TrainerDeck.Launch;

namespace TrainerDeck.Cheats;

public enum TriggerResult
{
    Toggled,
    SentHotkey,
    Failed,
}


public sealed class CheatService
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);

    private readonly TrainerLibrary _library;
    private readonly LaunchService _launcher;
    private readonly SettingsStore _settings;
    private readonly ArtService _art;
    private readonly object _gate = new();
    private readonly HashSet<int> _coverAttempts = new();
    private readonly Dictionary<string, TrainerSession> _sessions = new(StringComparer.Ordinal);
    private volatile GamepadButtons _activeModifier;

    public CheatService(TrainerLibrary library, LaunchService launcher, SettingsStore settings, ArtService art, bool elevated)
    {
        _library = library;
        _launcher = launcher;
        _settings = settings;
        _art = art;
        IsElevated = elevated;
        _launcher.Running.Changed += (_, _) => OnRunningChanged();
        _settings.Changed += (_, _) => RefreshShortcutState();
    }

    public bool IsElevated { get; }

    public GamepadButtons ActiveShortcutModifier => _activeModifier;

    public GamepadButtons ConfiguredModifier => ParseButtons(_settings.Current.CheatShortcutModifier);

    public event EventHandler? ShortcutStateChanged;

    public event Action<string>? CoverCaptured;

    public async Task<SessionSnapshot> ConnectAsync(string trainerId)
    {
        TrainerSession? session = SessionFor(trainerId);
        if (session is null)
        {
            return Cached(trainerId, SessionStatus.Unreadable);
        }

        SessionSnapshot? snapshot = session.Status == SessionStatus.Connecting
            ? await session.ConnectAsync(IsElevated, ConnectTimeout).ConfigureAwait(false)
            : new SessionSnapshot(session.Status, session.Options, null);
        snapshot ??= new SessionSnapshot(SessionStatus.Unreadable, [], null);

        if (snapshot.Options.Count > 0)
        {
            SaveCache(trainerId, snapshot.Options);
            _ = CaptureCoverIfMissingAsync(trainerId, session);
            return snapshot;
        }

        return Cached(trainerId, snapshot.Status);
    }

    public SessionSnapshot CachedSnapshot(string trainerId) => Cached(trainerId, SessionStatus.Connecting);

    public TrainerSession? ExistingSession(string trainerId)
    {
        lock (_gate)
        {
            return _sessions.GetValueOrDefault(trainerId);
        }
    }

    public async Task<TriggerResult> TriggerAsync(string trainerId, CheatOption option)
    {
        TrainerSession? session = ExistingSession(trainerId);
        if (session?.Status == SessionStatus.Connected && await session.ToggleAsync(option.Index).ConfigureAwait(false))
        {
            return TriggerResult.Toggled;
        }

        if (option.Hotkey is Hotkey hotkey && await HotkeySender.SendAsync(hotkey).ConfigureAwait(false))
        {
            return TriggerResult.SentHotkey;
        }

        return TriggerResult.Failed;
    }

    public IReadOnlyDictionary<string, string> Shortcuts(string trainerId) =>
        _library.Store.TryGet(trainerId)?.CheatShortcuts ?? new Dictionary<string, string>();

    public void SetShortcut(string trainerId, GamepadButtons button, string? cheatKey)
    {
        _library.UpdateMetadata(trainerId, m =>
        {
            m.CheatShortcuts ??= new Dictionary<string, string>();
            foreach (string b in m.CheatShortcuts.Where(kv => kv.Value == cheatKey).Select(kv => kv.Key).ToList())
            {
                m.CheatShortcuts.Remove(b);
            }

            if (cheatKey is null)
            {
                m.CheatShortcuts.Remove(button.ToString());
            }
            else if (button != GamepadButtons.None)
            {
                m.CheatShortcuts[button.ToString()] = cheatKey;
            }

            if (m.CheatShortcuts.Count == 0)
            {
                m.CheatShortcuts = null;
            }
        });
        RefreshShortcutState();
    }

    public void ClearShortcutFor(string trainerId, string cheatKey) => SetShortcut(trainerId, GamepadButtons.None, cheatKey);

    public void OnChord(GamepadButtons button) => _ = Task.Run(async () =>
    {
        foreach (RunningProcess running in _launcher.Running.Snapshot())
        {
            if (!Shortcuts(running.TrainerId).TryGetValue(button.ToString(), out string? key))
            {
                continue;
            }

            SessionSnapshot snapshot = await ConnectAsync(running.TrainerId).ConfigureAwait(false);
            CheatOption? option = snapshot.Options.FirstOrDefault(o => o.Key == key);
            if (option is null)
            {
                Log.Warn($"Cheats: shortcut {button} points at a missing cheat '{key}'.");
                continue;
            }

            TriggerResult result = await TriggerAsync(running.TrainerId, option).ConfigureAwait(false);
            Log.Info($"Cheats: shortcut {button} -> {option.Name} ({result}).");
        }
    });

    public static GamepadButtons ParseButtons(string? text) => ChordDetector.ParseButtons(text);

    private SessionSnapshot Cached(string trainerId, SessionStatus status)
    {
        List<CachedCheat>? cache = _library.Store.TryGet(trainerId)?.Cheats;
        if (cache is null || cache.Count == 0)
        {
            return new SessionSnapshot(status, [], null);
        }

        List<CheatOption> options = cache.Select((c, i) => new CheatOption(i, c.Name, c.Hotkey, c.Kind)).ToList();
        return new SessionSnapshot(status == SessionStatus.Connected ? SessionStatus.HotkeysOnly : status, options, null);
    }

    private void SaveCache(string trainerId, IReadOnlyList<CheatOption> options)
    {
        List<CachedCheat> fresh = options.Select(o => new CachedCheat(o.Name, o.HotkeyText, o.Kind)).ToList();
        List<CachedCheat>? old = _library.Store.TryGet(trainerId)?.Cheats;
        if (old is not null && old.SequenceEqual(fresh))
        {
            return;
        }

        _library.Store.Update(trainerId, m => m.Cheats = fresh);
        _library.SaveMetadata();
    }

    private TrainerSession? SessionFor(string trainerId)
    {
        RunningProcess? running = _launcher.Running.TryGet(trainerId);
        lock (_gate)
        {
            if (running is null)
            {
                _sessions.Remove(trainerId);
                return null;
            }

            if (!_sessions.TryGetValue(trainerId, out TrainerSession? s) || s.ProcessId != running.ProcessId
                || s.Status is SessionStatus.NeedsAdministrator or SessionStatus.Unreadable)
            {
                s = new TrainerSession(running.ProcessId);
                _sessions[trainerId] = s;
            }

            return s;
        }
    }

    private void OnRunningChanged()
    {
        var live = _launcher.Running.Snapshot().ToDictionary(r => r.TrainerId, r => r.ProcessId);
        lock (_gate)
        {
            foreach (string id in _sessions.Where(kv => !live.TryGetValue(kv.Key, out int pid) || pid != kv.Value.ProcessId).Select(kv => kv.Key).ToList())
            {
                _sessions.Remove(id);
            }
        }

        RefreshShortcutState();
        foreach (var (id, pid) in live)
        {
            _ = AutoConnectAsync(id, pid);
        }
    }

    private async Task AutoConnectAsync(string trainerId, int pid)
    {
        lock (_gate)
        {
            if (_art.HasTrainerCover(trainerId) || !_coverAttempts.Add(pid))
            {
                return;
            }
        }

        if (!IsElevated && LaunchNative.IsProcessElevated(pid) == true)
        {
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        if (_launcher.Running.TryGet(trainerId)?.ProcessId == pid)
        {
            await ConnectAsync(trainerId).ConfigureAwait(false);
        }
    }

    private async Task CaptureCoverIfMissingAsync(string trainerId, TrainerSession session)
    {
        if (_art.HasTrainerCover(trainerId))
        {
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        System.Windows.Media.Imaging.BitmapSource? cover = await session.CaptureCoverAsync().ConfigureAwait(false);
        if (cover is null)
        {
            Log.Info($"Cheats: no game cover found in the trainer window for {trainerId}.");
            return;
        }

        _art.SaveTrainerCover(trainerId, cover);
        Log.Info($"Cheats: saved the game cover from the trainer window ({cover.PixelWidth}x{cover.PixelHeight}).");
        CoverCaptured?.Invoke(trainerId);
    }

    private void RefreshShortcutState()
    {
        GamepadButtons modifier = GamepadButtons.None;
        if (_settings.Current.CheatShortcutsEnabled
            && _launcher.Running.Snapshot().Any(r => _library.Store.TryGet(r.TrainerId)?.CheatShortcuts is { Count: > 0 }))
        {
            modifier = ConfiguredModifier;
        }

        if (modifier != _activeModifier)
        {
            _activeModifier = modifier;
            Log.Info(modifier == GamepadButtons.None ? "Cheats: in-game shortcuts off." : $"Cheats: in-game shortcuts on (hold {modifier}).");
            ShortcutStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
