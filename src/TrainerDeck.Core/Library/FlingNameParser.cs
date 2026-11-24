using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TrainerDeck.Core.Library;

public enum NameSource
{
    FileName,
    FolderName,
    Fallback,
}

public sealed record ParsedTrainerName(string GameName, string? Version, int? OptionCount, NameSource Source)
{
    public string SortKey => FlingNameParser.MakeSortKey(GameName);

    public char JumpLetter => FlingNameParser.MakeJumpLetter(GameName);
}


public static partial class FlingNameParser
{
    private const int MaxFolderWalk = 3;

    private static readonly HashSet<string> GenericNames = new(StringComparer.Ordinal)
    {
        "", "trainer", "trainers", "fling", "flingtrainer", "flingtrainers", "game", "games", "cheat", "cheats",
        "cheattrainer", "newfolder", "downloads", "download", "bin", "x64", "x86", "win64", "win32", "release",
        "files", "unknown", "extracted", "temp", "tmp", "desktop", "documents",
    };

    public static ParsedTrainerName Parse(string exePath, string? scanRoot = null)
    {
        ArgumentNullException.ThrowIfNull(exePath);
        string stem = Path.GetFileNameWithoutExtension(exePath);
        Segment file = ParseSegment(stem);

        string? parentDir = Path.GetDirectoryName(exePath);
        bool fileUsable = file.Game is not null && !IsGeneric(file.Game);

        if (fileUsable)
        {
            if ((file.Version is null || file.Options is null) && parentDir is not null && !IsRootOrAbove(parentDir, scanRoot))
            {
                Segment parent = ParseSegment(Path.GetFileName(parentDir));
                if (parent.Game is not null && Normalize(parent.Game) == Normalize(file.Game!))
                {
                    return new(file.Game!, file.Version ?? parent.Version, file.Options ?? parent.Options, NameSource.FileName);
                }
            }

            return new(file.Game!, file.Version, file.Options, NameSource.FileName);
        }

        string? dir = parentDir;
        for (int depth = 0; dir is not null && depth < MaxFolderWalk && !IsRootOrAbove(dir, scanRoot); depth++)
        {
            Segment folder = ParseSegment(Path.GetFileName(dir));
            if (folder.Game is not null && !IsGeneric(folder.Game))
            {
                return new(folder.Game, folder.Version ?? file.Version, folder.Options ?? file.Options, NameSource.FolderName);
            }

            dir = Path.GetDirectoryName(dir);
        }

        string fallback = CleanForFallback(stem);
        if (fallback.Length == 0)
        {
            fallback = stem.Trim().Length > 0 ? stem.Trim() : "Unknown trainer";
        }

        return new(fallback, file.Version, file.Options, NameSource.Fallback);
    }

    internal readonly record struct Segment(string? Game, string? Version, int? Options);

    internal static Segment ParseSegment(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return default;
        }

        string s = Preclean(raw);

        string head = s;
        MatchCollection trainerWords = TrainerWordRegex().Matches(s);
        if (trainerWords.Count > 0)
        {
            head = s[..trainerWords[^1].Index];
        }

        head = head.Trim();

        int? options = null;
        Match plus = PlusRegex().Match(head);
        if (plus.Success)
        {
            options = int.Parse(plus.Groups["n"].Value, CultureInfo.InvariantCulture);
            head = head[..plus.Index];
        }

        head = TrimSeparators(head);

        string? version = null;
        Match ver = VersionStartRegex().Match(head);
        if (ver.Success)
        {
            version = TrimSeparators(head[ver.Index..]);
            head = TrimSeparators(head[..ver.Index]);
            if (version.Length == 0)
            {
                version = null;
            }
        }

        string game = FixCase(CollapseSpaces(head));
        if (!game.Any(char.IsLetterOrDigit))
        {
            return new(null, version, options);
        }

        return new(game, version, options);
    }

    private static string Preclean(string raw)
    {
        string s = raw.Trim();

        string previous;
        do
        {
            previous = s;
            s = CopySuffixRegex().Replace(s, "");
            s = DuplicateSuffixRegex().Replace(s, "");
        }
        while (s != previous);

        s = s.Replace('_', ' ');

        if (!s.Contains(' '))
        {
            s = DotsToSpaces(s);
        }

        if (!s.Contains(' ') && s.Count(c => c == '-') >= 2)
        {
            s = s.Replace('-', ' ');
        }

        s = SquareBracketRegex().Replace(s, " ");
        s = FlingTagRegex().Replace(s, " ");
        return CollapseSpaces(s);
    }

    private static string DotsToSpaces(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            bool betweenDigits = i > 0 && i + 1 < s.Length && char.IsDigit(s[i - 1]) && char.IsDigit(s[i + 1]);
            sb.Append(c == '.' && !betweenDigits ? ' ' : c);
        }

        return sb.ToString();
    }

    private static string CleanForFallback(string stem)
    {
        string s = Preclean(stem);
        s = TrainerWordRegex().Replace(s, " ");
        s = PlusAnywhereRegex().Replace(s, " ");
        return FixCase(TrimSeparators(CollapseSpaces(s)));
    }

    private static bool IsGeneric(string name) => GenericNames.Contains(Normalize(name)) || Normalize(name).StartsWith("newfolder", StringComparison.Ordinal);

    private static bool IsRootOrAbove(string dir, string? scanRoot)
    {
        if (scanRoot is null)
        {
            return Path.GetDirectoryName(dir) is null;
        }

        string d = TrimSlash(Path.GetFullPath(dir));
        string r = TrimSlash(Path.GetFullPath(scanRoot));
        if (d.Equals(r, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return r.StartsWith(d + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string TrimSlash(string p) => p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public static string? Variant(string exePath)
    {
        string s = Path.GetFileNameWithoutExtension(exePath).Trim();
        string previous;
        do
        {
            previous = s;
            s = CopySuffixRegex().Replace(s, "");
            s = DuplicateSuffixRegex().Replace(s, "");
        }
        while (s != previous);

        MatchCollection words = TrainerWordRegex().Matches(s.Replace('_', ' '));
        if (words.Count == 0)
        {
            return null;
        }

        string tail = s.Replace('_', ' ')[(words[^1].Index + words[^1].Length)..];
        tail = BitsRegex().Replace(tail, m => m.Groups["n"].Value + "-bit");
        tail = X64Regex().Replace(tail, "64-bit");
        tail = X86Regex().Replace(tail, "32-bit");
        tail = ParenthesesRegex().Replace(tail, m => " · " + m.Groups["inner"].Value.Trim() + " ");
        tail = CollapseSpaces(tail).Trim(' ', '-', '·', ',');
        return tail.Length == 0 ? null : tail;
    }

    public static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        return sb.ToString();
    }

    public static string MakeSortKey(string name)
    {
        string n = name.Trim();
        if (n.StartsWith("The ", StringComparison.OrdinalIgnoreCase) && n.Length > 4)
        {
            n = n[4..];
        }

        return n.ToUpperInvariant();
    }

    public static char MakeJumpLetter(string name)
    {
        string key = MakeSortKey(name);
        foreach (char c in key)
        {
            if (char.IsLetterOrDigit(c))
            {
                char upper = char.ToUpperInvariant(c);
                return upper is >= 'A' and <= 'Z' ? upper : '#';
            }
        }

        return '#';
    }

    private static string TrimSeparators(string s) => s.Trim().Trim('-', '–', '—', ',', ':', ';', '|', '~').Trim();

    private static string CollapseSpaces(string s) => SpacesRegex().Replace(s, " ").Trim();

    private static string FixCase(string s) =>
        s.Any(char.IsLetter) && !s.Any(char.IsUpper) ? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s) : s;

    [GeneratedRegex(@"\s*-\s*Copy(\s*\(\d+\))?$", RegexOptions.IgnoreCase)]
    private static partial Regex CopySuffixRegex();

    [GeneratedRegex(@"\s*\(\d{1,2}\)$")]
    private static partial Regex DuplicateSuffixRegex();

    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex SquareBracketRegex();

    [GeneratedRegex(@"[\(\[]?\s*-?\s*(?:by\s+)?\bFLiNG\b\s*[\)\]]?", RegexOptions.IgnoreCase)]
    private static partial Regex FlingTagRegex();

    [GeneratedRegex(@"\bTrainers?\b", RegexOptions.IgnoreCase)]
    private static partial Regex TrainerWordRegex();

    [GeneratedRegex(@"(?:\bPlus\s*|(?<=^|\s)\+\s*)(?<n>\d{1,3})\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex PlusRegex();

    [GeneratedRegex(@"(?:\bPlus\s*|(?<=^|\s)\+\s*)\d{1,3}\b", RegexOptions.IgnoreCase)]
    private static partial Regex PlusAnywhereRegex();

    [GeneratedRegex(
        @"(?<=^|\s)(?:" +
        @"v\.?\s?\d" +
        @"|ver(?:sion)?\.?\s?\d" +
        @"|build\s?\d" +
        @"|update\s?\d" +
        @"|early\s+access\b" +
        @"|\d+(?:\.\d+){2,}\+?(?=\s|$)" +
        @"|all\s+versions?\b" +
        @"|(?:steam|gog|epic(?:\s+games)?|microsoft\s+store|ms\s*store|windows\s+store|xbox|game\s*pass|uwp)(?=(?:\s*[/&+,]\s*\w+)*\s*(?:v\.?\s?\d|$))" +
        @")",
        RegexOptions.IgnoreCase)]
    private static partial Regex VersionStartRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpacesRegex();

    [GeneratedRegex(@"\b(?<n>32|64)\s*-?\s*bits?\b", RegexOptions.IgnoreCase)]
    private static partial Regex BitsRegex();

    [GeneratedRegex(@"\bx64\b", RegexOptions.IgnoreCase)]
    private static partial Regex X64Regex();

    [GeneratedRegex(@"\bx(?:86|32)\b", RegexOptions.IgnoreCase)]
    private static partial Regex X86Regex();

    [GeneratedRegex(@"\((?<inner>[^)]*)\)")]
    private static partial Regex ParenthesesRegex();
}
