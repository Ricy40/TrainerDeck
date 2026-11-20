using System.Text.RegularExpressions;

namespace TrainerDeck.Core.Cheats;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Ctrl = 1,
    Alt = 2,
    Shift = 4,
}

public sealed partial record Hotkey(HotkeyModifiers Modifiers, int Key, string KeyName)
{
    private static readonly Dictionary<string, (int Vk, string Name)> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["num *"] = (0x6A, "Num *"),
        ["num +"] = (0x6B, "Num +"),
        ["num -"] = (0x6D, "Num -"),
        ["num ."] = (0x6E, "Num ."),
        ["num del"] = (0x6E, "Num ."),
        ["num /"] = (0x6F, "Num /"),
        ["pageup"] = (0x21, "PageUp"),
        ["page up"] = (0x21, "PageUp"),
        ["pgup"] = (0x21, "PageUp"),
        ["pagedown"] = (0x22, "PageDown"),
        ["page down"] = (0x22, "PageDown"),
        ["pgdn"] = (0x22, "PageDown"),
        ["end"] = (0x23, "End"),
        ["home"] = (0x24, "Home"),
        ["left"] = (0x25, "Left"),
        ["up"] = (0x26, "Up"),
        ["right"] = (0x27, "Right"),
        ["down"] = (0x28, "Down"),
        ["insert"] = (0x2D, "Insert"),
        ["ins"] = (0x2D, "Insert"),
        ["delete"] = (0x2E, "Delete"),
        ["del"] = (0x2E, "Delete"),
        ["space"] = (0x20, "Space"),
        ["tab"] = (0x09, "Tab"),
        ["enter"] = (0x0D, "Enter"),
        ["backspace"] = (0x08, "Backspace"),
        ["pause"] = (0x13, "Pause"),
        ["capslock"] = (0x14, "CapsLock"),
        ["~"] = (0xC0, "~"),
        ["`"] = (0xC0, "`"),
        ["-"] = (0xBD, "-"),
        ["="] = (0xBB, "="),
        ["["] = (0xDB, "["),
        ["]"] = (0xDD, "]"),
        [";"] = (0xBA, ";"),
        ["'"] = (0xDE, "'"),
        [","] = (0xBC, ","),
        ["."] = (0xBE, "."),
        ["/"] = (0xBF, "/"),
        ["\\"] = (0xDC, "\\"),
    };

    public override string ToString()
    {
        string mods = (Modifiers.HasFlag(HotkeyModifiers.Ctrl) ? "Ctrl+" : "")
            + (Modifiers.HasFlag(HotkeyModifiers.Alt) ? "Alt+" : "")
            + (Modifiers.HasFlag(HotkeyModifiers.Shift) ? "Shift+" : "");
        return mods + KeyName;
    }

    public bool IsExtendedKey => Key is 0x6F or (>= 0x21 and <= 0x28) or 0x2D or 0x2E;

    public static Hotkey? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string s = Spaces().Replace(text.Trim(), " ");
        var mods = HotkeyModifiers.None;
        while (true)
        {
            Match m = ModifierPrefix().Match(s);
            if (!m.Success)
            {
                break;
            }

            mods |= m.Groups[1].Value.ToLowerInvariant() switch
            {
                "ctrl" or "control" => HotkeyModifiers.Ctrl,
                "alt" => HotkeyModifiers.Alt,
                _ => HotkeyModifiers.Shift,
            };
            s = s[m.Length..].TrimStart();
        }

        if (s.Length == 0)
        {
            return null;
        }

        Match num = NumpadDigit().Match(s);
        if (num.Success)
        {
            int d = num.Groups[1].Value[0] - '0';
            return new Hotkey(mods, 0x60 + d, $"Num {d}");
        }

        Match f = FunctionKey().Match(s);
        if (f.Success && int.TryParse(f.Groups[1].Value, out int fn) && fn is >= 1 and <= 24)
        {
            return new Hotkey(mods, 0x6F + fn, $"F{fn}");
        }

        string key = NumpadSymbol().Replace(s, "num $1");
        if (Named.TryGetValue(key, out var named))
        {
            return new Hotkey(mods, named.Vk, named.Name);
        }

        if (s.Length == 1 && char.IsAsciiLetterOrDigit(s[0]))
        {
            char c = char.ToUpperInvariant(s[0]);
            return new Hotkey(mods, c, c.ToString());
        }

        return null;
    }

    [GeneratedRegex(@"^(ctrl|control|alt|shift)\s*\+", RegexOptions.IgnoreCase)]
    private static partial Regex ModifierPrefix();

    [GeneratedRegex(@"^num(?:pad)?\s*([0-9])$", RegexOptions.IgnoreCase)]
    private static partial Regex NumpadDigit();

    [GeneratedRegex(@"^f([0-9]{1,2})$", RegexOptions.IgnoreCase)]
    private static partial Regex FunctionKey();

    [GeneratedRegex(@"^num(?:pad)?\s*([*+\-./]|del)$", RegexOptions.IgnoreCase)]
    private static partial Regex NumpadSymbol();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
