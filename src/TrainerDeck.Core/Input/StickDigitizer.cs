namespace TrainerDeck.Core.Input;


public sealed class StickDigitizer
{
    private GamepadButtons _stick;
    private bool _leftTrigger;
    private bool _rightTrigger;

    public float PressThreshold { get; init; } = 0.5f;

    public float ReleaseThreshold { get; init; } = 0.4f;

    public float TriggerPressThreshold { get; init; } = 0.5f;

    public float TriggerReleaseThreshold { get; init; } = 0.35f;

    public void Reset()
    {
        _stick = GamepadButtons.None;
        _leftTrigger = _rightTrigger = false;
    }

    public GamepadButtons Digitize(GamepadState s)
    {
        float x = s.LeftX, y = s.LeftY;
        float ax = Math.Abs(x), ay = Math.Abs(y);

        GamepadButtons candidate = GamepadButtons.None;
        if (Math.Max(ax, ay) >= PressThreshold)
        {
            candidate = ax > ay
                ? (x > 0 ? GamepadButtons.StickRight : GamepadButtons.StickLeft)
                : (y > 0 ? GamepadButtons.StickUp : GamepadButtons.StickDown);
        }

        if (candidate != GamepadButtons.None)
        {
            _stick = candidate;
        }
        else if (_stick != GamepadButtons.None && Along(_stick, x, y) < ReleaseThreshold)
        {
            _stick = GamepadButtons.None;
        }

        _leftTrigger = _leftTrigger ? s.LeftTrigger >= TriggerReleaseThreshold : s.LeftTrigger >= TriggerPressThreshold;
        _rightTrigger = _rightTrigger ? s.RightTrigger >= TriggerReleaseThreshold : s.RightTrigger >= TriggerPressThreshold;

        GamepadButtons result = s.Buttons | _stick;
        if (_leftTrigger)
        {
            result |= GamepadButtons.LeftTrigger;
        }

        if (_rightTrigger)
        {
            result |= GamepadButtons.RightTrigger;
        }

        return result;
    }

    private static float Along(GamepadButtons dir, float x, float y) => dir switch
    {
        GamepadButtons.StickRight => x,
        GamepadButtons.StickLeft => -x,
        GamepadButtons.StickUp => y,
        GamepadButtons.StickDown => -y,
        _ => 0,
    };
}
