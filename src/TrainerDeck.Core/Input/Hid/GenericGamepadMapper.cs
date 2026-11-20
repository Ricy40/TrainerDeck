namespace TrainerDeck.Core.Input.Hid;


public static class GenericGamepadMapper
{
    public static readonly IReadOnlyList<GamepadButtons> DefaultButtonOrder =
    [
        GamepadButtons.A,
        GamepadButtons.B,
        GamepadButtons.X,
        GamepadButtons.Y,
        GamepadButtons.LeftShoulder,
        GamepadButtons.RightShoulder,
        GamepadButtons.LeftTrigger,
        GamepadButtons.RightTrigger,
        GamepadButtons.Back,
        GamepadButtons.Start,
        GamepadButtons.LeftThumb,
        GamepadButtons.RightThumb,
    ];

    public static GamepadState Map(
        IEnumerable<int> buttons,
        float? x,
        float? y,
        int? hat,
        IReadOnlyList<GamepadButtons>? order = null)
    {
        order ??= DefaultButtonOrder;
        GamepadButtons b = hat is int h ? GamepadState.HatToDPad(h) : GamepadButtons.None;
        foreach (int n in buttons)
        {
            if (n >= 1 && n <= order.Count)
            {
                b |= order[n - 1];
            }
        }

        return new GamepadState(b, x ?? 0, -(y ?? 0), 0, 0, ControllerKind.Generic);
    }

    public static int? NormalizeHat(long value, long logicalMin, long logicalMax)
    {
        long range = logicalMax - logicalMin + 1;
        long v = value - logicalMin;
        if (v < 0 || v >= range || range <= 0)
        {
            return null;
        }

        // 4-position hats report 0..3 in 90 degree steps.
        return range == 4 ? (int)(v * 2) : (int)(v * 8 / range);
    }
}
