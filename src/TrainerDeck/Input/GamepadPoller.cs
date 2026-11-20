using System.Diagnostics;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Input;

namespace TrainerDeck.Input;


public sealed class GamepadPoller : IDisposable
{
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(16);
    private static readonly TimeSpan IdleCheck = TimeSpan.FromMilliseconds(500);

    private readonly IReadOnlyList<IGamepadSource> _sources;
    private readonly Func<bool> _isActive;
    private readonly Action<InputAction, ControllerKind> _onAction;
    private readonly FocusGate _gate = new();
    private readonly StickDigitizer _digitizer = new();
    private readonly InputRouter _router;
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public GamepadPoller(IReadOnlyList<IGamepadSource> sources, ControllerMapping mapping, Func<bool> isActive, Action<InputAction, ControllerKind> onAction)
    {
        _sources = sources;
        _isActive = isActive;
        _onAction = onAction;
        _router = new InputRouter(mapping);
    }

    public void Start() => _loop ??= Task.Run(() => LoopAsync(_cts.Token));

    public void Wake()
    {
        try
        {
            _wake.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        var states = new List<GamepadState>(4);
        var kind = ControllerKind.Xbox;
        bool running = false;
        using var timer = new PeriodicTimer(Tick);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                TimeSpan now = clock.Elapsed;
                if (!_isActive())
                {
                    if (running)
                    {
                        running = false;
                        _router.Reset();
                        _digitizer.Reset();
                        foreach (IGamepadSource s in _sources)
                        {
                            s.Suspend();
                        }

                        Log.Info("Controller input paused (window not in foreground).");
                    }

                    _gate.Filter(GamepadButtons.None, active: false, now);
                    await _wake.WaitAsync(IdleCheck, ct).ConfigureAwait(false);
                    continue;
                }

                if (!running)
                {
                    running = true;
                    foreach (IGamepadSource s in _sources)
                    {
                        s.Resume();
                    }

                    Log.Info("Controller input resumed.");
                }

                states.Clear();
                foreach (IGamepadSource s in _sources)
                {
                    s.Poll(states, now);
                }

                GamepadState merged = default;
                foreach (GamepadState st in states)
                {
                    merged = GamepadState.Merge(merged, st);
                }

                if (!merged.IsIdle())
                {
                    kind = merged.Kind;
                }

                GamepadButtons allowed = _gate.Filter(_digitizer.Digitize(merged), active: true, now);
                foreach (InputAction action in _router.Update(allowed, now))
                {
                    _onAction(action, kind);
                }

                await timer.WaitForNextTickAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error("Controller loop error", ex);
                await Task.Delay(1000, ct).ConfigureAwait(false);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _loop?.Wait(500);
        }
        catch (AggregateException)
        {
        }

        foreach (IGamepadSource s in _sources)
        {
            s.Dispose();
        }

        _cts.Dispose();
    }
}
