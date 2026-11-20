using TrainerDeck.Core.Input;
using TrainerDeck.Core.Input.Hid;

namespace TrainerDeck.Tests.Input;

public class SonyReportParserTests
{
    private const byte HatCentred = 0x08;

    [Theory]
    [InlineData(0x054C, 0x05C4, SonyModel.DualShock4)]
    [InlineData(0x054C, 0x09CC, SonyModel.DualShock4)]
    [InlineData(0x054C, 0x0CE6, SonyModel.DualSense)]
    [InlineData(0x054C, 0x0DF2, SonyModel.DualSense)]
    public void Identifies_known_pads(int vid, int pid, SonyModel model) =>
        Assert.Equal(model, SonyReportParser.Identify((ushort)vid, (ushort)pid));

    [Fact]
    public void Unknown_ids_are_not_sony() =>
        Assert.Null(SonyReportParser.Identify(0x045E, 0x0B12));

    // DS4 layout from offset o: LX LY RX RY hat+face shoulders system L2 R2
    private static byte[] Ds4(byte id, int offset, byte lx = 128, byte ly = 128, byte hatFace = HatCentred, byte shoulders = 0, byte l2 = 0, byte r2 = 0, int length = 64)
    {
        var r = new byte[length];
        r[0] = id;
        r[offset] = lx;
        r[offset + 1] = ly;
        r[offset + 2] = 128;
        r[offset + 3] = 128;
        r[offset + 4] = hatFace;
        r[offset + 5] = shoulders;
        r[offset + 7] = l2;
        r[offset + 8] = r2;
        return r;
    }

    // DualSense layout from offset o: LX LY RX RY L2 R2 counter hat+face shoulders system
    private static byte[] DualSense(byte id, int offset, byte lx = 128, byte ly = 128, byte hatFace = HatCentred, byte shoulders = 0, byte l2 = 0, int length = 64)
    {
        var r = new byte[length];
        r[0] = id;
        r[offset] = lx;
        r[offset + 1] = ly;
        r[offset + 2] = 128;
        r[offset + 3] = 128;
        r[offset + 4] = l2;
        r[offset + 7] = hatFace;
        r[offset + 8] = shoulders;
        return r;
    }

    [Fact]
    public void Ds4_usb_face_buttons_map_to_xbox_positions()
    {
        // Cross (0x20) + Triangle (0x80), hat centred
        Assert.True(SonyReportParser.TryParse(SonyModel.DualShock4, Ds4(0x01, 1, hatFace: 0x20 | 0x80 | HatCentred), false, out var s));
        Assert.Equal(GamepadButtons.A | GamepadButtons.Y, s.Buttons);
        Assert.Equal(ControllerKind.PlayStation, s.Kind);
    }

    [Fact]
    public void Ds4_bluetooth_extended_report_is_offset()
    {
        Assert.True(SonyReportParser.TryParse(SonyModel.DualShock4, Ds4(0x11, 3, hatFace: 0x40 | 2, shoulders: 0x01 | 0x20, length: 78), true, out var s));
        Assert.Equal(GamepadButtons.B | GamepadButtons.DPadRight | GamepadButtons.LeftShoulder | GamepadButtons.Start, s.Buttons);
    }

    [Fact]
    public void Ds4_axes_and_triggers_are_normalised()
    {
        Assert.True(SonyReportParser.TryParse(SonyModel.DualShock4, Ds4(0x01, 1, lx: 255, ly: 0, l2: 255), false, out var s));
        Assert.Equal(1f, s.LeftX, 2);
        Assert.Equal(1f, s.LeftY, 2);
        Assert.Equal(1f, s.LeftTrigger, 2);
        Assert.Equal(0f, s.RightTrigger, 2);
    }

    [Fact]
    public void DualSense_usb()
    {
        Assert.True(SonyReportParser.TryParse(SonyModel.DualSense, DualSense(0x01, 1, hatFace: 0x10 | 6, shoulders: 0x02 | 0x10, l2: 200), false, out var s));
        Assert.Equal(GamepadButtons.X | GamepadButtons.DPadLeft | GamepadButtons.RightShoulder | GamepadButtons.Back, s.Buttons);
        Assert.True(s.LeftTrigger > 0.7f);
    }

    [Fact]
    public void DualSense_bluetooth_enhanced_report()
    {
        Assert.True(SonyReportParser.TryParse(SonyModel.DualSense, DualSense(0x31, 2, ly: 255, hatFace: 0x20 | 4, length: 78), true, out var s));
        Assert.Equal(GamepadButtons.A | GamepadButtons.DPadDown, s.Buttons);
        Assert.Equal(-1f, s.LeftY, 2);
    }

    [Fact]
    public void DualSense_bluetooth_simple_report_uses_ds4_layout()
    {
        Assert.True(SonyReportParser.TryParse(SonyModel.DualSense, Ds4(0x01, 1, hatFace: 0x40 | HatCentred, length: 78), true, out var s));
        Assert.Equal(GamepadButtons.B, s.Buttons);
    }

    [Theory]
    [InlineData(0x05)]
    [InlineData(0x02)]
    public void Unknown_report_ids_are_rejected(byte id) =>
        Assert.False(SonyReportParser.TryParse(SonyModel.DualShock4, Ds4(id, 1), false, out _));

    [Fact]
    public void Short_reports_are_rejected() =>
        Assert.False(SonyReportParser.TryParse(SonyModel.DualSense, [0x01, 1, 2], false, out _));
}

public class GenericGamepadMapperTests
{
    [Fact]
    public void Buttons_follow_default_order()
    {
        GamepadState s = GenericGamepadMapper.Map([1, 2, 5, 10], null, null, null);
        Assert.Equal(GamepadButtons.A | GamepadButtons.B | GamepadButtons.LeftShoulder | GamepadButtons.Start, s.Buttons);
        Assert.Equal(ControllerKind.Generic, s.Kind);
    }

    [Fact]
    public void Out_of_range_buttons_are_ignored() =>
        Assert.Equal(GamepadButtons.None, GenericGamepadMapper.Map([0, 13, 99], null, null, null).Buttons);

    [Fact]
    public void Hat_and_axes()
    {
        GamepadState s = GenericGamepadMapper.Map([], 1f, -1f, 2);
        Assert.Equal(GamepadButtons.DPadRight, s.Buttons);
        Assert.Equal(1f, s.LeftX);
        Assert.Equal(1f, s.LeftY);
    }

    [Theory]
    [InlineData(0, 0, 7, 0)]
    [InlineData(7, 0, 7, 7)]
    [InlineData(8, 0, 7, null)]
    [InlineData(15, 0, 7, null)]
    [InlineData(1, 1, 8, 0)]
    [InlineData(8, 1, 8, 7)]
    [InlineData(0, 1, 8, null)]
    [InlineData(1, 0, 3, 2)]
    public void Normalizes_hats(long value, long min, long max, int? expected) =>
        Assert.Equal(expected, GenericGamepadMapper.NormalizeHat(value, min, max));

    [Theory]
    [InlineData(0, 0, 255, -1f)]
    [InlineData(255, 0, 255, 1f)]
    [InlineData(32768, 0, 65535, 0f)]
    [InlineData(-32768, -32768, 32767, -1f)]
    [InlineData(5, 5, 5, 0f)]
    public void Normalizes_axes(long value, long min, long max, float expected) =>
        Assert.Equal(expected, GamepadState.NormalizeAxis(value, min, max), 2);
}
