using TrainerDeck.Core.Cheats;
using TrainerDeck.Core.Input;

namespace TrainerDeck.Tests.Cheats;

public class HotkeyTests
{
    [Theory]
    [InlineData("Num 1", HotkeyModifiers.None, 0x61, "Num 1")]
    [InlineData("NUM 0", HotkeyModifiers.None, 0x60, "Num 0")]
    [InlineData("Ctrl+Num 1", HotkeyModifiers.Ctrl, 0x61, "Ctrl+Num 1")]
    [InlineData("CTRL+NUM 3", HotkeyModifiers.Ctrl, 0x63, "Ctrl+Num 3")]
    [InlineData("Alt+ Num /", HotkeyModifiers.Alt, 0x6F, "Alt+Num /")]
    [InlineData("Alt+Num .", HotkeyModifiers.Alt, 0x6E, "Alt+Num .")]
    [InlineData("Alt+Num +", HotkeyModifiers.Alt, 0x6B, "Alt+Num +")]
    [InlineData("Alt+Num *", HotkeyModifiers.Alt, 0x6A, "Alt+Num *")]
    [InlineData("Num -", HotkeyModifiers.None, 0x6D, "Num -")]
    [InlineData("Shift+F1", HotkeyModifiers.Shift, 0x70, "Shift+F1")]
    [InlineData("F6", HotkeyModifiers.None, 0x75, "F6")]
    [InlineData("Shift+PageUp", HotkeyModifiers.Shift, 0x21, "Shift+PageUp")]
    [InlineData("PageDown", HotkeyModifiers.None, 0x22, "PageDown")]
    [InlineData("Ctrl+Shift+Home", HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, 0x24, "Ctrl+Shift+Home")]
    [InlineData("ctrl + k", HotkeyModifiers.Ctrl, 'K', "Ctrl+K")]
    public void Parses_trainer_hotkeys(string text, HotkeyModifiers mods, int vk, string display)
    {
        Hotkey h = Hotkey.Parse(text)!;
        Assert.Equal(mods, h.Modifiers);
        Assert.Equal(vk, h.Key);
        Assert.Equal(display, h.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Hotkeys")]
    [InlineData("GOD MODE")]
    [InlineData("Ctrl+")]
    [InlineData("2.0")]
    public void Rejects_non_hotkeys(string text) => Assert.Null(Hotkey.Parse(text));

    [Fact]
    public void Numpad_divide_and_navigation_keys_are_extended()
    {
        Assert.True(Hotkey.Parse("Num /")!.IsExtendedKey);
        Assert.True(Hotkey.Parse("PageUp")!.IsExtendedKey);
        Assert.False(Hotkey.Parse("Num 5")!.IsExtendedKey);
    }
}

public class CheatOptionTests
{
    [Theory]
    [InlineData("CheatOption", false, CheatKind.Toggle)]
    [InlineData("CheatOptionWithInput", true, CheatKind.ToggleWithValue)]
    [InlineData("CheatOptionWithInputAdjustment", true, CheatKind.ToggleWithStepper)]
    [InlineData("CheatOptionSetValue", true, CheatKind.ActionWithValue)]
    [InlineData("CheatOptionSetValue", false, CheatKind.Action)]
    [InlineData("CheatOptionSetComboBox", false, CheatKind.Choice)]
    [InlineData("CheatOptionLabel", false, CheatKind.Section)]
    [InlineData("CheatOptionSomethingNew", true, CheatKind.ToggleWithValue)]
    public void Maps_fling_option_classes(string cls, bool textBox, CheatKind expected) =>
        Assert.Equal(expected, CheatOption.KindFromClass(cls, textBox));

    [Theory]
    [InlineData("2.0", 1, "2.5")]
    [InlineData("2.0", -1, "1.5")]
    [InlineData("0.5", -1, "0.4")]
    [InlineData("0.1", -1, "0.1")]
    [InlineData("10", 1, "11")]
    [InlineData("1000", 1, "1100")]
    [InlineData("9999999", -1, "9899999")]
    [InlineData("abc", 1, "abc")]
    public void Steps_values(string value, int dir, string expected) =>
        Assert.Equal(expected, CheatValue.Step(value, dir));
}

public class LegacyLayoutTests
{
    private static readonly TextLabel[] Hades =
    [
        new("Battle Options", 804, 278, 104, 19),
        new("GOD MODE", 964, 313, 85, 19), new("NUM 1", 804, 313, 55, 19),
        new("100% CRITICAL RATE", 964, 338, 147, 19), new("NUM 2", 804, 338, 55, 19),
        new("2.0", 1267, 388, 28, 19), new("DAMAGE MULTIPLIER", 964, 388, 151, 19), new("NUM 4", 804, 388, 55, 19),
        new("Items Editor", 804, 477, 89, 19),
        new("9999999", 1011, 512, 85, 18), new("GOLD", 964, 512, 47, 19), new("CTRL+NUM 1", 804, 512, 98, 19),
        new("Options", 962, 248, 77, 24), new("Hotkeys", 803, 248, 80, 24),
        new("General Notes:\nNum refers to numpad keys.", 800, 740, 550, 118),
        new("Hades.exe", 570, 539, 230, 35),
        new("English", 1185, 194, 89, 28),
        new("Hades", 572, 185, 464, 25),
    ];

    [Fact]
    public void Rebuilds_rows_from_label_positions()
    {
        List<CheatOption> options = LegacyLayout.Build(Hades);
        Assert.Equal(
            ["Battle Options", "GOD MODE", "100% CRITICAL RATE", "DAMAGE MULTIPLIER", "Items Editor", "GOLD"],
            options.Select(o => o.Name));
        Assert.Equal(CheatKind.Section, options[0].Kind);
        Assert.Equal("NUM 1", options[1].HotkeyText);
        Assert.Equal(CheatKind.Toggle, options[1].Kind);
        Assert.Equal(CheatKind.ToggleWithValue, options[3].Kind);
        Assert.Equal("Ctrl+Num 1", options[5].Hotkey!.ToString());
    }

    private static readonly TextLabel[] HadesLive =
    [
        new("General Notes: Num refers to numpad keys.", 800, 278, 545, 458),
        new("General Notes: Num refers to numpad keys.", 800, 278, 111, 19),
        new("Battle Options", 804, 278, 104, 19),
        new("NUM 1", 964, 313, 122, 24), new("GOD MODE", 964, 313, 85, 19), new("NUM 1", 923, 312, 41, 25), new("NUM 1", 804, 313, 55, 19),
        new("NUM 4", 964, 388, 351, 24), new("DAMAGE MULTIPLIER", 1115, 388, 200, 24), new("2.0", 1124, 392, 12, 12),
        new("2.0", 1115, 397, 150, 2), new("2.0", 1267, 388, 28, 19), new("DAMAGE MULTIPLIER", 964, 388, 151, 19),
        new("NUM 4", 923, 387, 41, 25), new("NUM 4", 804, 388, 55, 19),
        new("Items Editor", 804, 477, 89, 19),
        new("Options", 962, 248, 77, 24), new("Hotkeys", 803, 248, 80, 24),
    ];

    [Fact]
    public void Ignores_duplicate_names_from_containers_and_switches()
    {
        List<CheatOption> options = LegacyLayout.Build(HadesLive);
        Assert.Equal(["Battle Options", "GOD MODE", "DAMAGE MULTIPLIER"], options.Select(o => o.Name));
        Assert.Equal("NUM 1", options[1].HotkeyText);
        Assert.Equal(CheatKind.Toggle, options[1].Kind);
        Assert.Equal(CheatKind.ToggleWithValue, options[2].Kind);
    }

    [Fact]
    public void Reads_the_full_live_hades_trainer()
    {
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Cheats", "hades-legacy-labels.json"));
        List<TextLabel> labels = System.Text.Json.JsonSerializer.Deserialize<List<TextLabel>>(json)!;

        List<CheatOption> options = LegacyLayout.Build(labels);

        Assert.Equal(
        [
            "Battle Options", "GOD MODE", "100% CRITICAL RATE", "SUPER DAMAGE/ONE HIT KILL", "DAMAGE MULTIPLIER", "SET PLAYER SPEED",
            "SET GAME SPEED", "Items Editor", "GOLD", "KEYS", "DARKNESS", "REROLL STONES", "GEMSTONES", "DIAMOND", "TITAN BLOOD",
            "NECTAR", "AMBROSIA",
        ], options.Select(o => o.Name));
        Assert.Equal("CTRL+NUM 9", options[^1].HotkeyText);
        Assert.Equal(2, options.Count(o => o.Kind == CheatKind.Section));
    }

    [Fact]
    public void Needs_both_column_headers() =>
        Assert.Empty(LegacyLayout.Build([new("GOD MODE", 10, 10, 50, 19), new("NUM 1", 0, 10, 40, 19)]));
}

public class ChordDetectorTests
{
    private const GamepadButtons View = GamepadButtons.Back;

    [Fact]
    public void Fires_when_modifier_is_held_first()
    {
        var c = new ChordDetector(View);
        Assert.Empty(c.Update(View));
        Assert.Equal([GamepadButtons.A], c.Update(View | GamepadButtons.A));
        Assert.Empty(c.Update(View | GamepadButtons.A));
        Assert.Equal([GamepadButtons.DPadUp], c.Update(View | GamepadButtons.A | GamepadButtons.DPadUp));
    }

    [Fact]
    public void Ignores_buttons_without_modifier_or_pressed_together_with_it()
    {
        var c = new ChordDetector(View);
        Assert.Empty(c.Update(GamepadButtons.A));
        Assert.Empty(c.Update(GamepadButtons.None));
        Assert.Empty(c.Update(View | GamepadButtons.B));
    }

    [Theory]
    [InlineData("Back", GamepadButtons.Back)]
    [InlineData("LeftShoulder|RightShoulder", GamepadButtons.LeftShoulder | GamepadButtons.RightShoulder)]
    [InlineData(" back | start ", GamepadButtons.Back | GamepadButtons.Start)]
    [InlineData("Nonsense", GamepadButtons.None)]
    [InlineData(null, GamepadButtons.None)]
    public void Parses_button_names(string? text, GamepadButtons expected) =>
        Assert.Equal(expected, ChordDetector.ParseButtons(text));

    [Fact]
    public void Supports_two_button_modifiers()
    {
        var c = new ChordDetector(GamepadButtons.LeftShoulder | GamepadButtons.RightShoulder);
        Assert.Empty(c.Update(GamepadButtons.LeftShoulder));
        Assert.Empty(c.Update(GamepadButtons.LeftShoulder | GamepadButtons.X));
        c.Update(GamepadButtons.LeftShoulder | GamepadButtons.RightShoulder);
        Assert.Equal([GamepadButtons.Y], c.Update(GamepadButtons.LeftShoulder | GamepadButtons.RightShoulder | GamepadButtons.Y));
    }
}
