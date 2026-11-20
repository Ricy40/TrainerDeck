namespace TrainerDeck.Core.Input.Hid;

public enum SonyModel
{
    DualShock4,
    DualSense,
}


public static class SonyReportParser
{
    public const ushort SonyVendorId = 0x054C;

    private static readonly HashSet<ushort> DualShock4Products = [0x05C4, 0x09CC, 0x0BA0];
    private static readonly HashSet<ushort> DualSenseProducts = [0x0CE6, 0x0DF2];

    public static SonyModel? Identify(ushort vendorId, ushort productId)
    {
        if (vendorId != SonyVendorId)
        {
            return null;
        }

        if (DualShock4Products.Contains(productId))
        {
            return SonyModel.DualShock4;
        }

        return DualSenseProducts.Contains(productId) ? SonyModel.DualSense : null;
    }

    public static bool TryParse(SonyModel model, ReadOnlySpan<byte> report, bool bluetooth, out GamepadState state)
    {
        state = default;
        if (report.Length < 10)
        {
            return false;
        }

        byte id = report[0];
        return model switch
        {
            SonyModel.DualShock4 when id == 0x01 => ParseDs4Layout(report, 1, out state),
            SonyModel.DualShock4 when id == 0x11 => ParseDs4Layout(report, 3, out state),
            SonyModel.DualSense when id == 0x01 && bluetooth => ParseDs4Layout(report, 1, out state),
            SonyModel.DualSense when id == 0x01 => ParseDualSenseLayout(report, 1, out state),
            SonyModel.DualSense when id == 0x31 => ParseDualSenseLayout(report, 2, out state),
            _ => false,
        };
    }

    // LX LY RX RY [hat+face] [shoulders/menu] [ps/counter] L2 R2
    private static bool ParseDs4Layout(ReadOnlySpan<byte> r, int o, out GamepadState state)
    {
        state = default;
        if (r.Length < o + 9)
        {
            return false;
        }

        state = Build(r[o], r[o + 1], r[o + 4], r[o + 5], r[o + 6], r[o + 7], r[o + 8]);
        return true;
    }

    // LX LY RX RY L2 R2 counter [hat+face] [shoulders/menu] [ps/touch/mute]
    private static bool ParseDualSenseLayout(ReadOnlySpan<byte> r, int o, out GamepadState state)
    {
        state = default;
        if (r.Length < o + 10)
        {
            return false;
        }

        state = Build(r[o], r[o + 1], r[o + 7], r[o + 8], r[o + 9], r[o + 4], r[o + 5]);
        return true;
    }

    private static GamepadState Build(byte lx, byte ly, byte hatFace, byte shoulders, byte system, byte l2, byte r2)
    {
        GamepadButtons b = GamepadState.HatToDPad(hatFace & 0x0F);
        if ((hatFace & 0x10) != 0) b |= GamepadButtons.X;
        if ((hatFace & 0x20) != 0) b |= GamepadButtons.A;
        if ((hatFace & 0x40) != 0) b |= GamepadButtons.B;
        if ((hatFace & 0x80) != 0) b |= GamepadButtons.Y;
        if ((shoulders & 0x01) != 0) b |= GamepadButtons.LeftShoulder;
        if ((shoulders & 0x02) != 0) b |= GamepadButtons.RightShoulder;
        if ((shoulders & 0x10) != 0) b |= GamepadButtons.Back;
        if ((shoulders & 0x20) != 0) b |= GamepadButtons.Start;
        if ((shoulders & 0x40) != 0) b |= GamepadButtons.LeftThumb;
        if ((shoulders & 0x80) != 0) b |= GamepadButtons.RightThumb;
        if ((system & 0x01) != 0) b |= GamepadButtons.Guide;

        return new GamepadState(
            b,
            GamepadState.NormalizeAxis(lx, 0, 255),
            -GamepadState.NormalizeAxis(ly, 0, 255),
            l2 / 255f,
            r2 / 255f,
            ControllerKind.PlayStation);
    }
}
