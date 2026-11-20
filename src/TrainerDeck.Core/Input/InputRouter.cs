namespace TrainerDeck.Core.Input;


public sealed class InputRouter
{
    private readonly ControllerMapping _mapping;
    private GamepadButtons _previous;
    private InputAction? _repeating;
    private TimeSpan _nextRepeat;

    public InputRouter(ControllerMapping mapping) => _mapping = mapping;

    public TimeSpan RepeatDelay { get; init; } = TimeSpan.FromMilliseconds(380);

    public TimeSpan RepeatInterval { get; init; } = TimeSpan.FromMilliseconds(90);

    public TimeSpan PageRepeatInterval { get; init; } = TimeSpan.FromMilliseconds(220);

    public GamepadButtons Held => _previous;

    public void Reset()
    {
        _previous = GamepadButtons.None;
        _repeating = null;
    }

    public bool IsHeld(InputAction action) => (_previous & _mapping.ButtonsFor(action)) != 0;

    public IReadOnlyList<InputAction> Update(GamepadButtons buttons, TimeSpan now)
    {
        var actions = new List<InputAction>();
        GamepadButtons pressed = buttons & ~_previous;

        if (pressed != GamepadButtons.None)
        {
            foreach (ButtonBinding binding in _mapping.Bindings)
            {
                if ((pressed & binding.Button) == 0 || actions.Contains(binding.Action))
                {
                    continue;
                }

                actions.Add(binding.Action);
                if (IsRepeatable(binding.Action))
                {
                    _repeating = binding.Action;
                    _nextRepeat = now + RepeatDelay;
                }
            }
        }

        if (_repeating is InputAction held && !actions.Contains(held))
        {
            if ((buttons & _mapping.ButtonsFor(held)) == 0)
            {
                _repeating = null;
            }
            else if (now >= _nextRepeat)
            {
                actions.Add(held);
                _nextRepeat = now + (held is InputAction.PageUp or InputAction.PageDown ? PageRepeatInterval : RepeatInterval);
            }
        }

        _previous = buttons;
        return actions;
    }

    private static bool IsRepeatable(InputAction action) => action is InputAction.Up or InputAction.Down
        or InputAction.Left or InputAction.Right or InputAction.PageUp or InputAction.PageDown;
}
