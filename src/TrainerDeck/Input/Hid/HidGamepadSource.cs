using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Input;

namespace TrainerDeck.Input.Hid;


public sealed class HidGamepadSource : IGamepadSource
{
    private readonly object _gate = new();
    private readonly Dictionary<string, HidGamepad> _pads = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> _known = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _rescanRequested = true;
    private bool _suspended;

    public event Action<string, bool>? ConnectionChanged;

    public void RequestRescan() => _rescanRequested = true;

    public void Poll(List<GamepadState> states, TimeSpan now)
    {
        lock (_gate)
        {
            if (_suspended)
            {
                return;
            }

            if (_rescanRequested)
            {
                _rescanRequested = false;
                Rescan();
            }

            foreach (var (path, pad) in _pads.ToList())
            {
                if (!pad.IsConnected)
                {
                    _pads.Remove(path);
                    pad.Dispose();
                    Forget(path);
                    continue;
                }

                states.Add(pad.State);
            }
        }
    }

    private void Rescan()
    {
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (string path in HidNative.EnumerateDevicePaths())
            {
                present.Add(path);
                if (_pads.ContainsKey(path))
                {
                    continue;
                }

                HidGamepad? pad = HidGamepad.TryOpen(path);
                if (pad is null)
                {
                    continue;
                }

                _pads[path] = pad;
                if (_known.TryAdd(path, pad.Name))
                {
                    Log.Info($"HID gamepad connected: {pad.Name} ({path})");
                    ConnectionChanged?.Invoke(pad.Name, true);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"HID enumeration failed: {ex.Message}");
        }

        foreach (string gone in _pads.Keys.Where(p => !present.Contains(p)).ToList())
        {
            _pads[gone].Dispose();
            _pads.Remove(gone);
        }

        foreach (string gone in _known.Keys.Where(p => !present.Contains(p)).ToList())
        {
            Forget(gone);
        }
    }

    private void Forget(string path)
    {
        if (_known.Remove(path, out string? name))
        {
            Log.Info($"HID gamepad disconnected: {name}");
            ConnectionChanged?.Invoke(name, false);
        }
    }

    public void Suspend()
    {
        lock (_gate)
        {
            _suspended = true;
            CloseAll();
        }
    }

    public void Resume()
    {
        lock (_gate)
        {
            _suspended = false;
            _rescanRequested = true;
        }
    }

    private void CloseAll()
    {
        foreach (HidGamepad pad in _pads.Values)
        {
            pad.Dispose();
        }

        _pads.Clear();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            CloseAll();
        }
    }
}
