using System.Text;
using System.Text.RegularExpressions;

namespace TrainerDeck.Core.Steam;

public static partial class SteamMatcher
{
    private static readonly string[] EditionSuffixes =
    [
        "game of the year edition", "goty edition", "goty", "definitive edition", "complete edition", "enhanced edition",
        "deluxe edition", "ultimate edition", "gold edition", "anniversary edition", "special edition", "standard edition",
        "directors cut", "remastered", "remaster", "hd", "early access",
    ];

    public static int? Match(string gameName, IEnumerable<(int AppId, string Name)> candidates)
    {
        string[] wanted = Variants(gameName).ToArray();
        if (wanted.Length == 0 || wanted[0].Length == 0)
        {
            return null;
        }

        int? loose = null;
        foreach (var (id, name) in candidates)
        {
            string[] theirs = Variants(name).ToArray();
            if (theirs.Length == 0)
            {
                continue;
            }

            if (theirs[0] == wanted[0])
            {
                return id;
            }

            if (loose is null && theirs.Intersect(wanted).Any())
            {
                loose = id;
            }
        }

        return loose;
    }

    public static IEnumerable<string> Variants(string name)
    {
        string n = Normalize(name);
        if (n.Length == 0)
        {
            yield break;
        }

        yield return n;
        string current = n;
        bool changed = true;
        while (changed)
        {
            changed = false;
            Match year = TrailingYear().Match(current);
            if (year.Success && year.Index > 0)
            {
                current = current[..year.Index].TrimEnd();
                changed = true;
                yield return current;
            }

            foreach (string suffix in EditionSuffixes)
            {
                if (current.Length > suffix.Length + 1 && current.EndsWith(" " + suffix, StringComparison.Ordinal))
                {
                    current = current[..^(suffix.Length + 1)].TrimEnd();
                    changed = true;
                    yield return current;
                    break;
                }
            }
        }
    }

    public static string Normalize(string name)
    {
        var sb = new StringBuilder(name.Length);
        string stripped = name.Replace("™", "").Replace("®", "").Replace("©", "");
        foreach (char raw in stripped.Normalize(NormalizationForm.FormKD))
        {
            char c = char.ToLowerInvariant(raw);
            if (c is '\'' or '’' ||char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (c == '&')
            {
                sb.Append(" and ");
            }
            else if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
            else
            {
                sb.Append(' ');
            }
        }

        return Spaces().Replace(sb.ToString(), " ").Trim();
    }

    [GeneratedRegex(@"\s(19|20)\d\d$")]
    private static partial Regex TrailingYear();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
