using TrainerDeck.Core.Input;

namespace TrainerDeck.Tests.Input;

public class InputRouterTests
{
    private static TimeSpan Ms(int ms) => TimeSpan.FromMilliseconds(ms);

    private static InputRouter Router() => new(ControllerMapping.Default)
    {
        RepeatDelay = Ms(400),
        RepeatInterval = Ms(100),
        PageRepeatInterval = Ms(250),
    };

    [Fact]
    public void Press_edge_produces_one_action()
    {
        var r = Router();
        Assert.Equal([InputAction.Activate], r.Update(GamepadButtons.A, Ms(0)));
        Assert.Empty(r.Update(GamepadButtons.A, Ms(16)));
        Assert.Empty(r.Update(GamepadButtons.A, Ms(2000))); // A never repeats
        Assert.Empty(r.Update(GamepadButtons.None, Ms(2016)));
        Assert.Equal([InputAction.Activate], r.Update(GamepadButtons.A, Ms(2032)));
    }

    [Theory]
    [InlineData(GamepadButtons.B, InputAction.Back)]
    [InlineData(GamepadButtons.X, InputAction.Favourite)]
    [InlineData(GamepadButtons.Y, InputAction.Details)]
    [InlineData(GamepadButtons.LeftTrigger, InputAction.PageUp)]
    [InlineData(GamepadButtons.RightTrigger, InputAction.PageDown)]
    [InlineData(GamepadButtons.Back, InputAction.Sort)]
    [InlineData(GamepadButtons.Start, InputAction.Settings)]
    [InlineData(GamepadButtons.StickLeft, InputAction.Left)]
    [InlineData(GamepadButtons.DPadDown, InputAction.Down)]
    public void Default_mapping(GamepadButtons button, InputAction expected) =>
        Assert.Equal([expected], Router().Update(button, Ms(0)));

    [Theory]
    [InlineData(GamepadButtons.LeftShoulder, InputAction.PrevTab)]
    [InlineData(GamepadButtons.RightShoulder, InputAction.NextTab)]
    public void Shoulder_tap_switches_tab_on_release(GamepadButtons button, InputAction expected)
    {
        var r = Router();
        Assert.Empty(r.Update(button, Ms(0)));
        Assert.Empty(r.Update(button, Ms(120)));
        Assert.Equal([expected], r.Update(GamepadButtons.None, Ms(150)));
    }

    [Fact]
    public void Shoulder_hold_opens_letter_rail_and_release_closes_it()
    {
        var r = Router();
        Assert.Empty(r.Update(GamepadButtons.RightShoulder, Ms(0)));
        Assert.Equal([InputAction.LetterRail], r.Update(GamepadButtons.RightShoulder, Ms(320)));
        Assert.Empty(r.Update(GamepadButtons.RightShoulder, Ms(600)));
        Assert.Equal([InputAction.Down], r.Update(GamepadButtons.RightShoulder | GamepadButtons.DPadDown, Ms(650)));
        Assert.Empty(r.Update(GamepadButtons.RightShoulder, Ms(700)));
        Assert.Equal([InputAction.LetterRailEnd], r.Update(GamepadButtons.None, Ms(800)));
    }

    [Fact]
    public void Reset_forgets_a_pending_hold()
    {
        var r = Router();
        r.Update(GamepadButtons.LeftShoulder, Ms(0));
        r.Reset();
        Assert.Empty(r.Update(GamepadButtons.None, Ms(100)));
    }

    [Fact]
    public void Mapping_reports_shoulders_for_tab_and_rail_actions()
    {
        Assert.Equal(GamepadButtons.LeftShoulder, ControllerMapping.Default.ButtonsFor(InputAction.PrevTab));
        Assert.Equal(GamepadButtons.LeftShoulder | GamepadButtons.RightShoulder, ControllerMapping.Default.ButtonsFor(InputAction.LetterRail));
    }

    [Fact]
    public void Held_direction_repeats_after_delay_then_at_rate()
    {
        var r = Router();
        Assert.Equal([InputAction.Down], r.Update(GamepadButtons.DPadDown, Ms(0)));
        Assert.Empty(r.Update(GamepadButtons.DPadDown, Ms(200)));
        Assert.Empty(r.Update(GamepadButtons.DPadDown, Ms(399)));
        Assert.Equal([InputAction.Down], r.Update(GamepadButtons.DPadDown, Ms(400)));
        Assert.Empty(r.Update(GamepadButtons.DPadDown, Ms(450)));
        Assert.Equal([InputAction.Down], r.Update(GamepadButtons.DPadDown, Ms(500)));
        Assert.Equal([InputAction.Down], r.Update(GamepadButtons.DPadDown, Ms(600)));
    }

    [Fact]
    public void Page_repeat_is_slower()
    {
        var r = Router();
        r.Update(GamepadButtons.RightTrigger, Ms(0));
        Assert.Equal([InputAction.PageDown], r.Update(GamepadButtons.RightTrigger, Ms(400)));
        Assert.Empty(r.Update(GamepadButtons.RightTrigger, Ms(500)));
        Assert.Equal([InputAction.PageDown], r.Update(GamepadButtons.RightTrigger, Ms(650)));
    }

    [Fact]
    public void Releasing_stops_repeat()
    {
        var r = Router();
        r.Update(GamepadButtons.DPadRight, Ms(0));
        r.Update(GamepadButtons.None, Ms(100));
        Assert.Empty(r.Update(GamepadButtons.None, Ms(1000)));
    }

    [Fact]
    public void Dpad_and_stick_together_give_one_action_and_repeat_while_either_is_held()
    {
        var r = Router();
        Assert.Equal([InputAction.Up], r.Update(GamepadButtons.DPadUp | GamepadButtons.StickUp, Ms(0)));
        Assert.Equal([InputAction.Up], r.Update(GamepadButtons.StickUp, Ms(400)));
    }

    [Fact]
    public void New_direction_takes_over_repeat()
    {
        var r = Router();
        r.Update(GamepadButtons.DPadDown, Ms(0));
        Assert.Equal([InputAction.Right], r.Update(GamepadButtons.DPadDown | GamepadButtons.DPadRight, Ms(300)));
        Assert.Empty(r.Update(GamepadButtons.DPadDown | GamepadButtons.DPadRight, Ms(500)));
        Assert.Equal([InputAction.Right], r.Update(GamepadButtons.DPadDown | GamepadButtons.DPadRight, Ms(700)));
    }

    [Fact]
    public void Reset_forgets_held_buttons()
    {
        var r = Router();
        r.Update(GamepadButtons.A, Ms(0));
        r.Reset();
        Assert.Equal([InputAction.Activate], r.Update(GamepadButtons.A, Ms(16)));
    }

    [Fact]
    public void IsHeld_reports_held_actions()
    {
        var r = Router();
        r.Update(GamepadButtons.RightThumb | GamepadButtons.Back, Ms(0));
        Assert.True(r.IsHeld(InputAction.Sort));
        Assert.False(r.IsHeld(InputAction.Activate));
    }
}

public class StickDigitizerTests
{
    private static GamepadState Stick(float x, float y) => new(GamepadButtons.None, x, y, 0, 0);

    [Theory]
    [InlineData(0.6f, 0f, GamepadButtons.StickRight)]
    [InlineData(-0.6f, 0.2f, GamepadButtons.StickLeft)]
    [InlineData(0.1f, 0.9f, GamepadButtons.StickUp)]
    [InlineData(0f, -0.55f, GamepadButtons.StickDown)]
    [InlineData(0.45f, 0.3f, GamepadButtons.None)]
    [InlineData(0.7f, 0.69f, GamepadButtons.StickRight)] // dominant axis only
    public void Deadzone_and_dominant_axis(float x, float y, GamepadButtons expected) =>
        Assert.Equal(expected, new StickDigitizer().Digitize(Stick(x, y)));

    [Fact]
    public void Hysteresis_keeps_direction_until_below_release_threshold()
    {
        var d = new StickDigitizer();
        Assert.Equal(GamepadButtons.StickRight, d.Digitize(Stick(0.55f, 0)));
        Assert.Equal(GamepadButtons.StickRight, d.Digitize(Stick(0.45f, 0)));
        Assert.Equal(GamepadButtons.None, d.Digitize(Stick(0.35f, 0)));
        Assert.Equal(GamepadButtons.None, d.Digitize(Stick(0.45f, 0)));
    }

    [Fact]
    public void Triggers_become_buttons_with_hysteresis()
    {
        var d = new StickDigitizer();
        Assert.Equal(GamepadButtons.RightTrigger, d.Digitize(new GamepadState(GamepadButtons.None, 0, 0, 0, 0.6f)));
        Assert.Equal(GamepadButtons.RightTrigger, d.Digitize(new GamepadState(GamepadButtons.None, 0, 0, 0, 0.4f)));
        Assert.Equal(GamepadButtons.None, d.Digitize(new GamepadState(GamepadButtons.None, 0, 0, 0, 0.2f)));
        Assert.Equal(GamepadButtons.LeftTrigger | GamepadButtons.A, d.Digitize(new GamepadState(GamepadButtons.A, 0, 0, 1f, 0)));
    }

    [Fact]
    public void Merge_combines_controllers()
    {
        var a = new GamepadState(GamepadButtons.A, 0.1f, -0.9f, 0, 0.2f, ControllerKind.Xbox);
        var b = new GamepadState(GamepadButtons.B, -0.5f, 0.3f, 0.7f, 0, ControllerKind.PlayStation);
        GamepadState m = GamepadState.Merge(a, b);
        Assert.Equal(GamepadButtons.A | GamepadButtons.B, m.Buttons);
        Assert.Equal(-0.5f, m.LeftX);
        Assert.Equal(-0.9f, m.LeftY);
        Assert.Equal(0.7f, m.LeftTrigger);
        Assert.Equal(ControllerKind.Xbox, m.Kind);
        Assert.Equal(ControllerKind.PlayStation, GamepadState.Merge(default, b).Kind);
    }
}
