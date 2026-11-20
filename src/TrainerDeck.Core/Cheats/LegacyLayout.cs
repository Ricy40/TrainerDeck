using System.Globalization;

namespace TrainerDeck.Core.Cheats;

public sealed record TextLabel(string Text, double X, double Y, double Width, double Height);


public static class LegacyLayout
{
    public static List<CheatOption> Build(IEnumerable<TextLabel> labels)
    {
        List<TextLabel> raw = labels.Where(l => !string.IsNullOrWhiteSpace(l.Text) && l.Height >= 6).ToList();

        var blocks = new HashSet<string>(raw.Where(l => l.Height >= 40).Select(l => l.Text));
        List<TextLabel> all = raw.Where(l => l.Height < 40 && !blocks.Contains(l.Text)).ToList();
        TextLabel? hotkeysHeader = all.FirstOrDefault(l => l.Text.Trim().Equals("Hotkeys", StringComparison.OrdinalIgnoreCase));
        TextLabel? optionsHeader = hotkeysHeader is null ? null : all.FirstOrDefault(l =>
            l.Text.Trim().Equals("Options", StringComparison.OrdinalIgnoreCase) && l.X > hotkeysHeader.X + 20);
        if (hotkeysHeader is null || optionsHeader is null)
        {
            return [];
        }

        double left = hotkeysHeader.X - 8;
        double top = Math.Max(hotkeysHeader.Y + hotkeysHeader.Height, optionsHeader.Y + optionsHeader.Height) - 2;
        List<TextLabel> area = all
            .Where(l => l.X >= left && l.Y >= top && l != hotkeysHeader && l != optionsHeader)
            .OrderBy(l => l.Y).ThenBy(l => l.X)
            .ToList();

        var rows = new List<List<TextLabel>>();
        foreach (TextLabel l in area)
        {
            List<TextLabel>? row = rows.LastOrDefault();
            if (row is not null && Math.Abs(CenterY(row[0]) - CenterY(l)) < Math.Min(row[0].Height, l.Height) / 2)
            {
                row.Add(l);
            }
            else
            {
                rows.Add([l]);
            }
        }

        var result = new List<CheatOption>();
        foreach (List<TextLabel> row in rows)
        {
            List<TextLabel> ordered = row.OrderBy(l => l.X).ThenBy(l => l.Width).ToList();
            TextLabel? hotkey = ordered.FirstOrDefault(l => l.X < optionsHeader.X - 4 && Hotkey.Parse(l.Text) is not null);
            bool IsWords(TextLabel l) => !IsNumber(l.Text) && Hotkey.Parse(l.Text) is null && l.Text.Any(char.IsLetter);
            TextLabel? name = ordered.FirstOrDefault(l => l.X >= optionsHeader.X - 4 && IsWords(l));
            bool hasValue = ordered.Any(l => IsNumber(l.Text));

            if (hotkey is not null && name is not null)
            {
                result.Add(new CheatOption(result.Count, Clean(name.Text), hotkey.Text.Trim(), hasValue ? CheatKind.ToggleWithValue : CheatKind.Toggle));
            }
            else if (hotkey is null && ordered.Where(IsWords).Select(l => l.Text).Distinct().Count() == 1
                && ordered.First(IsWords) is { } section && section.Text.Length <= 40)
            {
                result.Add(new CheatOption(result.Count, Clean(section.Text), null, CheatKind.Section));
            }
        }

        while (result.Count > 0 && result[^1].Kind == CheatKind.Section)
        {
            result.RemoveAt(result.Count - 1);
        }

        return result;
    }

    private static double CenterY(TextLabel l) => l.Y + (l.Height / 2);

    private static bool IsNumber(string s) =>
        double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    private static string Clean(string s) => s.Replace('\n', ' ').Replace('\r', ' ').Trim();
}
