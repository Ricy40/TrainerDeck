namespace TrainerDeck.Core.Input;


[Flags]
public enum GamepadButtons : uint
{
    None = 0,
    DPadUp = 0x0001,
    DPadDown = 0x0002,
    DPadLeft = 0x0004,
    DPadRight = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LeftThumb = 0x0040,
    RightThumb = 0x0080,
    LeftShoulder = 0x0100,
    RightShoulder = 0x0200,
    Guide = 0x0400,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000,
    LeftTrigger = 0x1_0000,
    RightTrigger = 0x2_0000,
    StickUp = 0x10_0000,
    StickDown = 0x20_0000,
    StickLeft = 0x40_0000,
    StickRight = 0x80_0000,
}

public enum ControllerKind
{
    Xbox,
    PlayStation,
    Generic,
}

public readonly record struct GamepadState(
    GamepadButtons Buttons,
    float LeftX,
    float LeftY,
    float LeftTrigger,
    float RightTrigger,
    ControllerKind Kind = ControllerKind.Xbox)
{
    public bool IsIdle(float threshold = 0.3f) =>
        Buttons == GamepadButtons.None
        && Math.Abs(LeftX) < threshold && Math.Abs(LeftY) < threshold
        && LeftTrigger < threshold && RightTrigger < threshold;

    public static GamepadState Merge(GamepadState a, GamepadState b) => new(
        a.Buttons | b.Buttons,
        MaxAbs(a.LeftX, b.LeftX),
        MaxAbs(a.LeftY, b.LeftY),
        Math.Max(a.LeftTrigger, b.LeftTrigger),
        Math.Max(a.RightTrigger, b.RightTrigger),
        a.IsIdle() ? b.Kind : a.Kind);

    public static GamepadButtons HatToDPad(int hat) => hat switch
    {
        0 => GamepadButtons.DPadUp,
        1 => GamepadButtons.DPadUp | GamepadButtons.DPadRight,
        2 => GamepadButtons.DPadRight,
        3 => GamepadButtons.DPadDown | GamepadButtons.DPadRight,
        4 => GamepadButtons.DPadDown,
        5 => GamepadButtons.DPadDown | GamepadButtons.DPadLeft,
        6 => GamepadButtons.DPadLeft,
        7 => GamepadButtons.DPadUp | GamepadButtons.DPadLeft,
        _ => GamepadButtons.None,
    };

    public static float NormalizeAxis(long value, long min, long max)
    {
        if (max <= min)
        {
            return 0;
        }

        double v = ((double)(value - min) / (max - min) * 2.0) - 1.0;
        return (float)Math.Clamp(v, -1.0, 1.0);
    }

    private static float MaxAbs(float a, float b) => Math.Abs(a) >= Math.Abs(b) ? a : b;
}
