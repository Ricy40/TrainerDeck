namespace TrainerDeck.Core.Input;

public readonly record struct ButtonBinding(GamepadButtons Button, InputAction Action);

public sealed class ControllerMapping
{
    public ControllerMapping(IEnumerable<ButtonBinding> bindings)
    {
        Bindings = bindings.ToArray();
    }

    public IReadOnlyList<ButtonBinding> Bindings { get; }

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
        new(GamepadButtons.LeftShoulder, InputAction.PrevTab),
        new(GamepadButtons.RightShoulder, InputAction.NextTab),
        new(GamepadButtons.LeftTrigger, InputAction.PageUp),
        new(GamepadButtons.RightTrigger, InputAction.PageDown),
        new(GamepadButtons.Back, InputAction.Sort),
        new(GamepadButtons.Start, InputAction.Settings),
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

        return result;
    }
}
