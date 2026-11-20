using TrainerDeck.Core.Input;

namespace TrainerDeck.Tests.Input;

public class FocusGateTests
{
    private static TimeSpan Ms(int ms) => TimeSpan.FromMilliseconds(ms);

    private const GamepadButtons A = GamepadButtons.A;
    private const GamepadButtons B = GamepadButtons.B;
    private const GamepadButtons None = GamepadButtons.None;

    [Fact]
    public void Blocks_everything_while_not_active()
    {
        var gate = new FocusGate();
        Assert.Equal(None, gate.Filter(A | B, active: false, Ms(0)));
        Assert.False(gate.IsActive);
    }

    [Fact]
    public void Button_held_when_focus_returns_is_ignored_until_released()
    {
        var gate = new FocusGate();
        gate.Filter(A, active: false, Ms(0));

        Assert.Equal(None, gate.Filter(A, active: true, Ms(16)));
        Assert.Equal(None, gate.Filter(A, active: true, Ms(500)));
        Assert.Equal(None, gate.Filter(None, active: true, Ms(600)));
        Assert.Equal(A, gate.Filter(A, active: true, Ms(700)));
    }

    [Fact]
    public void Latch_clears_per_button()
    {
        var gate = new FocusGate();
        gate.Filter(A | B, active: true, Ms(0));
        Assert.Equal(None, gate.Filter(A, active: true, Ms(300)));
        Assert.Equal(B, gate.Filter(A | B, active: true, Ms(400)));
    }

    [Fact]
    public void New_presses_after_activation_pass_through()
    {
        var gate = new FocusGate();
        Assert.Equal(None, gate.Filter(None, active: true, Ms(0)));
        Assert.Equal(B, gate.Filter(B, active: true, Ms(300)));
    }

    [Fact]
    public void Late_arriving_state_within_grace_is_latched()
    {
        var gate = new FocusGate { Grace = Ms(150) };
        Assert.Equal(None, gate.Filter(None, active: true, Ms(0)));
        Assert.Equal(None, gate.Filter(A, active: true, Ms(40)));
        Assert.Equal(None, gate.Filter(A, active: true, Ms(400)));
        Assert.Equal(None, gate.Filter(None, active: true, Ms(500)));
        Assert.Equal(A, gate.Filter(A, active: true, Ms(600)));
    }

    [Fact]
    public void Losing_focus_relatches_on_return()
    {
        var gate = new FocusGate();
        gate.Filter(None, active: true, Ms(0));
        Assert.Equal(A, gate.Filter(A, active: true, Ms(300)));

        Assert.Equal(None, gate.Filter(A, active: false, Ms(400)));
        Assert.Equal(None, gate.Filter(A, active: true, Ms(900)));
        Assert.Equal(None, gate.Filter(None, active: true, Ms(1000)));
        Assert.Equal(A, gate.Filter(A, active: true, Ms(1100)));
    }

    [Fact]
    public void First_ever_poll_latches_too() =>
        Assert.Equal(None, new FocusGate().Filter(A, active: true, Ms(0)));
}
