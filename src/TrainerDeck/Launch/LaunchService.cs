using System.IO;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Launch;
using TrainerDeck.Core.Library;

namespace TrainerDeck.Launch;

public sealed class LaunchService : IDisposable
{
    private static readonly TimeSpan WindowTimeout = TimeSpan.FromSeconds(10);

    private readonly TrainerLibrary _library;
    private readonly Func<IntPtr> _owner;
    private int _scanning;

    public LaunchService(TrainerLibrary library, Func<IntPtr> owner)
    {
        _library = library;
        _owner = owner;
    }

    public ProcessTracker Tracker { get; } = new();

    public RunningTrainers Running => Tracker.Running;

    public bool IsLaunching { get; private set; }

    public async Task<LaunchResult> LaunchAsync(TrainerEntry entry)
    {
        if (Running.TryGet(entry.Id) is RunningProcess running)
        {
            Tracker.BringToFront(running.ProcessId);
            return LaunchResult.AlreadyRunning(running.ProcessId);
        }

        string path = entry.File.FullPath;
        if (!File.Exists(path))
        {
            Log.Warn($"Launch: {path} no longer exists.");
            _ = _library.RescanAsync();
            return LaunchResult.Failed(LaunchErrors.FileNotFound, entry.DisplayName);
        }

        IsLaunching = true;
        try
        {
            Log.Info($"Launching {path}");
            ShellResult r = await ShellLauncher.RunAsync(path, null, entry.File.Directory, "runas", _owner());
            if (r.Error == LaunchErrors.Cancelled)
            {
                Log.Info("Launch cancelled at the UAC prompt.");
                return LaunchResult.Cancelled();
            }

            if (r.Error != 0)
            {
                Log.Warn($"Launch failed with error {r.Error}: {path}");
                if (LaunchErrors.SuggestsMissingFile(r.Error))
                {
                    _ = _library.RescanAsync();
                }

                return LaunchResult.Failed(r.Error, entry.DisplayName);
            }

            _library.RecordLaunch(entry.Id);
            int pid = 0;
            if (r.Process is { } handle)
            {
                pid = LaunchNative.GetProcessId(handle);
                Tracker.Track(entry.Id, handle);
            }

            Log.Info($"Started {entry.File.FileName} (pid {pid}).");
            return LaunchResult.Started(pid);
        }
        catch (Exception ex)
        {
            Log.Error($"Launch threw for {path}", ex);
            return LaunchResult.Failed(-1, entry.DisplayName);
        }
        finally
        {
            IsLaunching = false;
        }
    }

    public async Task<bool> FocusWhenReadyAsync(int pid)
    {
        if (pid == 0 || !await Tracker.WaitForWindowAsync(pid, WindowTimeout).ConfigureAwait(false))
        {
            return false;
        }

        await Task.Delay(200).ConfigureAwait(false);
        return Tracker.BringToFront(pid);
    }

    public Task<CloseResult> CloseAsync(string trainerId) => Tracker.CloseAsync(trainerId);

    public void ScanRunningInBackground()
    {
        if (Interlocked.Exchange(ref _scanning, 1) == 1)
        {
            return;
        }

        IReadOnlyList<TrainerEntry> entries = _library.Entries;
        _ = Task.Run(() =>
        {
            try
            {
                Tracker.ScanFor(entries);
            }
            catch (Exception ex)
            {
                Log.Warn($"Running-trainer scan failed: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _scanning, 0);
            }
        });
    }

    public void Dispose() => Tracker.Dispose();
}
