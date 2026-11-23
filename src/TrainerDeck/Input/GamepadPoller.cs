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
    private readonly Func<bool> _backgroundWanted;
    private readonly Action<GamepadButtons> _onBackground;
    private readonly FocusGate _gate = new();
    private readonly StickDigitizer _digitizer = new();
    private readonly InputRouter _router;
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public GamepadPoller(IReadOnlyList<IGamepadSource> sources, ControllerMapping mapping, Func<bool> isActive, Action<InputAction, ControllerKind> onAction,
        Func<bool>? backgroundWanted = null, Action<GamepadButtons>? onBackground = null)
    {
        _sources = sources;
        _isActive = isActive;
        _onAction = onAction;
        _backgroundWanted = backgroundWanted ?? (() => false);
        _onBackground = onBackground ?? (_ => { });
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
        var mode = Mode.Paused;
        using var timer = new PeriodicTimer(Tick);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                TimeSpan now = clock.Elapsed;
                bool active = _isActive();
                Mode wanted = active ? Mode.Foreground : _backgroundWanted() ? Mode.Shortcuts : Mode.Paused;
                if (wanted != mode)
                {
                    _router.Reset();
                    _digitizer.Reset();
                    _onBackground(GamepadButtons.None);
                    if (wanted == Mode.Paused)
                    {
                        foreach (IGamepadSource s in _sources)
                        {
                            s.Suspend();
                        }
                    }
                    else if (mode == Mode.Paused)
                    {
                        foreach (IGamepadSource s in _sources)
                        {
                            s.Resume();
                        }
                    }

                    Log.Info(wanted switch
                    {
                        Mode.Foreground => "Controller input resumed.",
                        Mode.Shortcuts => "Controller input paused; listening only for the overlay combo and cheat shortcuts.",
                        _ => "Controller input paused (window not in foreground).",
                    });
                    mode = wanted;
                }

                if (mode == Mode.Paused)
                {
                    _gate.Filter(GamepadButtons.None, active: false, now);
                    await _wake.WaitAsync(IdleCheck, ct).ConfigureAwait(false);
                    continue;
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

                GamepadButtons buttons = _digitizer.Digitize(merged);
                if (mode == Mode.Foreground)
                {
                    GamepadButtons allowed = _gate.Filter(buttons, active: true, now);
                    foreach (InputAction action in _router.Update(allowed, now))
                    {
                        _onAction(action, kind);
                    }
                }
                else
                {
                    _gate.Filter(GamepadButtons.None, active: false, now);
                    _onBackground(buttons);
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

    private enum Mode
    {
        Paused,
        Foreground,
        Shortcuts,
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
