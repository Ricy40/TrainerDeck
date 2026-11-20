namespace TrainerDeck.Core.Input;

public readonly record struct ButtonBinding(GamepadButtons Button, InputAction Action);


public readonly record struct HoldBinding(GamepadButtons Button, InputAction TapAction, InputAction HoldAction, InputAction EndAction);

public sealed class ControllerMapping
{
    public ControllerMapping(IEnumerable<ButtonBinding> bindings, IEnumerable<HoldBinding>? holds = null)
    {
        Bindings = bindings.ToArray();
        Holds = holds?.ToArray() ?? [];
    }

    public IReadOnlyList<ButtonBinding> Bindings { get; }

    public IReadOnlyList<HoldBinding> Holds { get; }

    public static ControllerMapping Default { get; } = new(
    [
        new(GamepadButtons.DPadUp, InputAction.Up),
        new(GamepadButtons.DPadDown, InputAction.Down),
        new(GamepadButtons.DPadLeft, InputAction.Left),
        new(GamepadButtons.DPadRight, InputAction.Right),
        new(GamepadButtons.StickUp, InputAction.Up),
        new(GamepadButtons.StickDown, InputAction.Down),
        new(GamepadButtons.StickLeft, InputAction.Left),
        new(GamepadButtons.StickRight, InputAction.Right),
        new(GamepadButtons.A, InputAction.Activate),
        new(GamepadButtons.B, InputAction.Back),
        new(GamepadButtons.X, InputAction.Favourite),
        new(GamepadButtons.Y, InputAction.Details),
        new(GamepadButtons.LeftTrigger, InputAction.PageUp),
        new(GamepadButtons.RightTrigger, InputAction.PageDown),
        new(GamepadButtons.Back, InputAction.Sort),
        new(GamepadButtons.Start, InputAction.Settings),
    ],
    [
        new(GamepadButtons.LeftShoulder, InputAction.PrevTab, InputAction.LetterRail, InputAction.LetterRailEnd),
        new(GamepadButtons.RightShoulder, InputAction.NextTab, InputAction.LetterRail, InputAction.LetterRailEnd),
    ]);

    public GamepadButtons ButtonsFor(InputAction action)
    {
        GamepadButtons result = GamepadButtons.None;
        foreach (ButtonBinding b in Bindings)
        {
            if (b.Action == action)
            {
                result |= b.Button;
            }
        }

        foreach (HoldBinding h in Holds)
        {
            if (h.TapAction == action || h.HoldAction == action)
            {
                result |= h.Button;
            }
        }

        return result;
    }
}
