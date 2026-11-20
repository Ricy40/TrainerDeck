namespace TrainerDeck.Core.Input;


public sealed class FocusGate
{
    private bool _active;
    private TimeSpan _activatedAt;
    private GamepadButtons _latched;

    public TimeSpan Grace { get; init; } = TimeSpan.FromMilliseconds(150);

    public bool IsActive => _active;

    public GamepadButtons Latched => _latched;

    public GamepadButtons Filter(GamepadButtons held, bool active, TimeSpan now)
    {
        if (!active)
        {
            _active = false;
            _latched = GamepadButtons.None;
            return GamepadButtons.None;
        }

        if (!_active)
        {
            _active = true;
            _activatedAt = now;
            _latched = held;
        }
        else if (now - _activatedAt < Grace)
        {
            _latched |= held;
        }

        _latched &= held;
        return held & ~_latched;
    }
}
