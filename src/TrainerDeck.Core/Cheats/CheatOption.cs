using System.Globalization;

namespace TrainerDeck.Core.Cheats;

public enum CheatKind
{
    Toggle,
    ToggleWithValue,
    ToggleWithStepper,
    Action,
    ActionWithValue,
    Choice,
    Section,
}

public sealed record CheatOption(int Index, string Name, string? HotkeyText, CheatKind Kind)
{
    public Hotkey? Hotkey { get; } = Hotkey.Parse(HotkeyText);

    public string Key => Name.Trim().ToUpperInvariant();

    public bool HasValue => Kind is CheatKind.ToggleWithValue or CheatKind.ToggleWithStepper or CheatKind.ActionWithValue or CheatKind.Choice;

    public bool IsToggle => Kind is CheatKind.Toggle or CheatKind.ToggleWithValue or CheatKind.ToggleWithStepper;

    public static CheatKind KindFromClass(string className, bool hasTextBox) => className switch
    {
        "CheatOption" => CheatKind.Toggle,
        "CheatOptionWithInput" => CheatKind.ToggleWithValue,
        "CheatOptionWithInputAdjustment" => CheatKind.ToggleWithStepper,
        "CheatOptionSetValue" => hasTextBox ? CheatKind.ActionWithValue : CheatKind.Action,
        "CheatOptionSetComboBox" => CheatKind.Choice,
        "CheatOptionLabel" => CheatKind.Section,
        _ when className.Contains("ComboBox", StringComparison.Ordinal) => CheatKind.Choice,
        _ when className.Contains("Adjustment", StringComparison.Ordinal) => CheatKind.ToggleWithStepper,
        _ when className.Contains("SetValue", StringComparison.Ordinal) => CheatKind.Action,
        _ => hasTextBox ? CheatKind.ToggleWithValue : CheatKind.Toggle,
    };
}

public static class CheatValue
{
    public static string Step(string current, int direction)
    {
        if (!double.TryParse(current, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
        {
            return current;
        }

        bool isDecimal = current.Contains('.');
        double magnitude = Math.Abs(v);
        double step = magnitude < 1 && isDecimal ? 0.1
            : magnitude < 10 && isDecimal ? 0.5
            : magnitude < 100 ? 1
            : Math.Pow(10, Math.Floor(Math.Log10(magnitude)) - 1);
        double next = Math.Round(v + (direction * step), 4);
        if (v > 0 && next <= 0)
        {
            next = isDecimal ? 0.1 : 0;
        }

        return isDecimal ? next.ToString("0.0###", CultureInfo.InvariantCulture) : next.ToString("0", CultureInfo.InvariantCulture);
    }
}
