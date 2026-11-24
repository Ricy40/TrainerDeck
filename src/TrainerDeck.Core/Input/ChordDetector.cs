namespace TrainerDeck.Core.Input;


public sealed class ComboDetector
{
    private bool _wasHeld;

    public GamepadButtons Combo { get; set; }

    public void Reset() => _wasHeld = false;

    public bool Update(GamepadButtons held)
    {
        bool now = Combo != GamepadButtons.None && (held & Combo) == Combo;
        bool fired = now && !_wasHeld;
        _wasHeld = now;
        return fired;
    }
}

public sealed class ChordDetector
{
    private GamepadButtons _previous;

    public ChordDetector(GamepadButtons modifier) => Modifier = modifier;

    public GamepadButtons Modifier { get; set; }

    public void Reset() => _previous = GamepadButtons.None;

    public static GamepadButtons ParseButtons(string? text)
    {
        GamepadButtons result = GamepadButtons.None;
        foreach (string part in (text ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse(part, ignoreCase: true, out GamepadButtons b))
            {
                result |= b;
            }
        }

        return result;
    }

    public IReadOnlyList<GamepadButtons> Update(GamepadButtons held)
    {
        var fired = new List<GamepadButtons>();
        bool modifierHeld = Modifier != GamepadButtons.None && (held & Modifier) == Modifier;
        bool modifierWasHeld = Modifier != GamepadButtons.None && (_previous & Modifier) == Modifier;
        if (modifierHeld && modifierWasHeld)
        {
            GamepadButtons pressed = held & ~_previous & ~Modifier;
            for (uint bit = 1; bit != 0 && bit <= (uint)pressed; bit <<= 1)
            {
                if (((uint)pressed & bit) != 0)
                {
                    fired.Add((GamepadButtons)bit);
                }
            }
        }

        _previous = held;
        return fired;
    }
}
