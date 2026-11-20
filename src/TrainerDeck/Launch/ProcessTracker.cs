using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Launch;
using TrainerDeck.Core.Library;

namespace TrainerDeck.Launch;

public enum CloseResult
{
    Closed,
    NotRunning,
    NeedsAdministrator,
    Failed,
}

public sealed class ProcessTracker : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<int, Tracked> _tracked = new();
    private bool _disposed;

    public RunningTrainers Running { get; } = new();

    public void Track(string trainerId, SafeProcessHandle handle, bool canTerminate = true)
    {
        int pid = LaunchNative.GetProcessId(handle);
        Tracked t;
        lock (_gate)
        {
            if (_disposed || pid == 0 || _tracked.ContainsKey(pid))
            {
                handle.Dispose();
                return;
            }

            t = new Tracked(trainerId, pid, handle, canTerminate);
            _tracked[pid] = t;
        }

        Running.Add(new RunningProcess(trainerId, pid, DateTime.UtcNow));
        t.Wait = ThreadPool.RegisterWaitForSingleObject(new ProcessWaitHandle(handle), (_, _) => OnExited(pid), null, Timeout.Infinite, executeOnlyOnce: true);
    }

    public int ScanFor(IReadOnlyList<TrainerEntry> entries)
    {
        var names = new HashSet<string>(entries.Select(e => Path.GetFileNameWithoutExtension(e.File.FullPath)), StringComparer.OrdinalIgnoreCase);
        if (names.Count == 0)
        {
            return 0;
        }

        var opened = new List<(int Pid, string Path, SafeProcessHandle Handle, bool CanTerminate)>();
        foreach (Process p in Process.GetProcesses())
        {
            using (p)
            {
                int pid = p.Id;
                if (!names.Contains(p.ProcessName) || Running.IsTracked(pid))
                {
                    continue;
                }

                bool canTerminate = true;
                SafeProcessHandle h = LaunchNative.OpenProcess(LaunchNative.Synchronize | LaunchNative.ProcessQueryLimitedInformation | LaunchNative.ProcessTerminate, false, pid);
                if (h.IsInvalid)
                {
                    h.Dispose();
                    canTerminate = false;
                    h = LaunchNative.OpenProcess(LaunchNative.Synchronize | LaunchNative.ProcessQueryLimitedInformation, false, pid);
                }

                if (h.IsInvalid || LaunchNative.ImagePath(h) is not string path)
                {
                    h.Dispose();
                    continue;
                }

                opened.Add((pid, path, h, canTerminate));
            }
        }

        Dictionary<string, int> matches = ProcessMatcher.Match(
            opened.Select(o => (o.Pid, o.Path)),
            entries.Select(e => (e.Id, e.File.FullPath)));
        var byPid = matches.ToDictionary(m => m.Value, m => m.Key);

        int found = 0;
        foreach (var o in opened)
        {
            if (byPid.TryGetValue(o.Pid, out string? id))
            {
                Log.Info($"Found running trainer {Path.GetFileName(o.Path)} (pid {o.Pid}).");
                Track(id, o.Handle, o.CanTerminate);
                found++;
            }
            else
            {
                o.Handle.Dispose();
            }
        }

        return found;
    }

    public bool BringToFront(int pid)
    {
        IntPtr hwnd = LaunchNative.TopLevelWindows(pid).FirstOrDefault();
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        if (LaunchNative.IsIconic(hwnd))
        {
            LaunchNative.ShowWindow(hwnd, LaunchNative.SwRestore);
        }

        return LaunchNative.SetForegroundWindow(hwnd);
    }

    public async Task<bool> WaitForWindowAsync(int pid, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (LaunchNative.TopLevelWindows(pid).Count > 0)
            {
                return true;
            }

            if (!IsTrackedPid(pid))
            {
                return false;
            }

            await Task.Delay(150).ConfigureAwait(false);
        }

        return false;
    }

    public async Task<CloseResult> CloseAsync(string trainerId)
    {
        Tracked? t;
        lock (_gate)
        {
            t = _tracked.Values.FirstOrDefault(x => x.TrainerId == trainerId);
        }

        if (t is null)
        {
            return CloseResult.NotRunning;
        }

        foreach (IntPtr hwnd in LaunchNative.TopLevelWindows(t.Pid))
        {
            LaunchNative.PostMessageW(hwnd, LaunchNative.WmClose, IntPtr.Zero, IntPtr.Zero);
        }

        if (await WaitAsync(t.Exited.Task, TimeSpan.FromSeconds(2)).ConfigureAwait(false))
        {
            return CloseResult.Closed;
        }

        if (t.CanTerminate && LaunchNative.TerminateProcess(t.Handle, 0))
        {
            Log.Info($"Terminated pid {t.Pid}.");
            await WaitAsync(t.Exited.Task, TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            return CloseResult.Closed;
        }

        int error = t.CanTerminate ? Marshal.GetLastWin32Error() : LaunchErrors.AccessDenied;
        Log.Warn($"Could not close pid {t.Pid} (error {error}).");
        return error == LaunchErrors.AccessDenied ? CloseResult.NeedsAdministrator : CloseResult.Failed;
    }

    private static async Task<bool> WaitAsync(Task task, TimeSpan timeout) =>
        await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false) == task;

    private bool IsTrackedPid(int pid)
    {
        lock (_gate)
        {
            return _tracked.ContainsKey(pid);
        }
    }

    private void OnExited(int pid)
    {
        Tracked? t;
        lock (_gate)
        {
            if (!_tracked.Remove(pid, out t))
            {
                return;
            }
        }

        t.Wait?.Unregister(null);
        t.Handle.Dispose();
        Running.Remove(t.TrainerId, pid);
        t.Exited.TrySetResult();
        Log.Info($"Trainer exited (pid {pid}).");
    }

    public void Dispose()
    {
        List<Tracked> all;
        lock (_gate)
        {
            _disposed = true;
            all = _tracked.Values.ToList();
            _tracked.Clear();
        }

        foreach (Tracked t in all)
        {
            t.Wait?.Unregister(null);
            t.Handle.Dispose();
        }
    }

    private sealed class Tracked(string trainerId, int pid, SafeProcessHandle handle, bool canTerminate)
    {
        public string TrainerId { get; } = trainerId;

        public int Pid { get; } = pid;

        public SafeProcessHandle Handle { get; } = handle;

        public bool CanTerminate { get; } = canTerminate;

        public RegisteredWaitHandle? Wait { get; set; }

        public TaskCompletionSource Exited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class ProcessWaitHandle : WaitHandle
    {
        public ProcessWaitHandle(SafeProcessHandle process) =>
            SafeWaitHandle = new SafeWaitHandle(process.DangerousGetHandle(), ownsHandle: false);
    }
}
