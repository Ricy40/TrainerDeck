using System.Windows.Media;
using TrainerDeck.Core.Input;

namespace TrainerDeck.UI.Input;

public readonly record struct ButtonGlyph(string Text, bool Round, Color Color);


public static class ControllerGlyphs
{
    private static readonly Color Neutral = Color.FromRgb(0xF3, 0xF5, 0xF9);

    private static readonly GamepadButtons[] Priority =
    [
        GamepadButtons.A, GamepadButtons.B, GamepadButtons.X, GamepadButtons.Y,
        GamepadButtons.LeftShoulder, GamepadButtons.RightShoulder,
        GamepadButtons.LeftTrigger, GamepadButtons.RightTrigger,
        GamepadButtons.Back, GamepadButtons.Start,
        GamepadButtons.LeftThumb, GamepadButtons.RightThumb,
        GamepadButtons.DPadUp, GamepadButtons.DPadDown, GamepadButtons.DPadLeft, GamepadButtons.DPadRight,
    ];

    public static ButtonGlyph For(InputAction action, ControllerKind kind, ControllerMapping mapping)
    {
        if (action is InputAction.NextTab or InputAction.PrevTab or InputAction.LetterRail)
        {
            ButtonGlyph l = For(GamepadButtons.LeftShoulder, kind), r = For(GamepadButtons.RightShoulder, kind);
            return new ButtonGlyph($"{l.Text} {r.Text}", false, Neutral);
        }

        if (action is InputAction.Up or InputAction.Down or InputAction.Left or InputAction.Right)
        {
            return new ButtonGlyph("✥", false, Neutral);
        }

        GamepadButtons bound = mapping.ButtonsFor(action);
        foreach (GamepadButtons b in Priority)
        {
            if ((bound & b) != 0)
            {
                return For(b, kind);
            }
        }

        return new ButtonGlyph("?", false, Neutral);
    }

    public static ButtonGlyph For(GamepadButtons button, ControllerKind kind) => kind switch
    {
        ControllerKind.PlayStation => button switch
        {
            GamepadButtons.A => new("✕", true, Color.FromRgb(0x8F, 0xB2, 0xF0)),
            GamepadButtons.B => new("○", true, Color.FromRgb(0xF0, 0x6E, 0x78)),
            GamepadButtons.X => new("□", true, Color.FromRgb(0xE0, 0x9A, 0xCB)),
            GamepadButtons.Y => new("△", true, Color.FromRgb(0x4F, 0xD1, 0xAC)),
            GamepadButtons.LeftShoulder => new("L1", false, Neutral),
            GamepadButtons.RightShoulder => new("R1", false, Neutral),
            GamepadButtons.LeftTrigger => new("L2", false, Neutral),
            GamepadButtons.RightTrigger => new("R2", false, Neutral),
            GamepadButtons.Back => new("Share", false, Neutral),
            GamepadButtons.Start => new("Options", false, Neutral),
            GamepadButtons.LeftThumb => new("L3", false, Neutral),
            GamepadButtons.RightThumb => new("R3", false, Neutral),
            _ => new("✥", false, Neutral),
        },
        ControllerKind.Generic => button switch
        {
            GamepadButtons.A => new("1", true, Neutral),
            GamepadButtons.B => new("2", true, Neutral),
            GamepadButtons.X => new("3", true, Neutral),
            GamepadButtons.Y => new("4", true, Neutral),
            GamepadButtons.LeftShoulder => new("5", false, Neutral),
            GamepadButtons.RightShoulder => new("6", false, Neutral),
            GamepadButtons.LeftTrigger => new("7", false, Neutral),
            GamepadButtons.RightTrigger => new("8", false, Neutral),
            GamepadButtons.Back => new("9", false, Neutral),
            GamepadButtons.Start => new("10", false, Neutral),
            GamepadButtons.LeftThumb => new("11", false, Neutral),
            GamepadButtons.RightThumb => new("12", false, Neutral),
            _ => new("✥", false, Neutral),
        },
        _ => button switch
        {
            GamepadButtons.A => new("A", true, Color.FromRgb(0x6C, 0xCB, 0x2E)),
            GamepadButtons.B => new("B", true, Color.FromRgb(0xF0, 0x55, 0x4D)),
            GamepadButtons.X => new("X", true, Color.FromRgb(0x3E, 0x9B, 0xF2)),
            GamepadButtons.Y => new("Y", true, Color.FromRgb(0xF5, 0xC2, 0x1B)),
            GamepadButtons.LeftShoulder => new("LB", false, Neutral),
            GamepadButtons.RightShoulder => new("RB", false, Neutral),
            GamepadButtons.LeftTrigger => new("LT", false, Neutral),
            GamepadButtons.RightTrigger => new("RT", false, Neutral),
            GamepadButtons.Back => new("View", false, Neutral),
            GamepadButtons.Start => new("Menu", false, Neutral),
            GamepadButtons.LeftThumb => new("LS", false, Neutral),
            GamepadButtons.RightThumb => new("RS", false, Neutral),
            _ => new("✥", false, Neutral),
        },
    };
}
