using System.Text.RegularExpressions;

namespace TrainerDeck.Core.Library;


public sealed class IgnoreRules
{
    public static readonly IReadOnlyList<string> BuiltInPatterns =
    [
        "unins*.exe",
        "*setup*.exe",
        "*install*.exe",
        "vcredist*",
        "vc_redist*",
        "dxsetup*",
        "dxwebsetup*",
        "directx*",
        "dotnet*",
        "ndp*",
        "*redist*",
        "UnityCrashHandler*",
        "crashpad_handler*",
        "CrashReport*",
        "*updater*.exe",
        "*/_CommonRedist/*",
        "*/__MACOSX/*",
    ];

    private readonly List<(Regex Regex, bool MatchPath)> _rules = new();

    public IgnoreRules(IEnumerable<string>? userPatterns = null, bool includeBuiltIns = true)
    {
        IEnumerable<string> all = includeBuiltIns ? BuiltInPatterns : [];
        if (userPatterns is not null)
        {
            all = all.Concat(userPatterns);
        }

        foreach (string raw in all)
        {
            string p = (raw ?? "").Trim().Replace('\\', '/');
            if (p.Length == 0 || p.StartsWith('#'))
            {
                continue;
            }

            _rules.Add((GlobToRegex(p), p.Contains('/')));
        }
    }

    public bool IsIgnored(string relativePath)
    {
        string rel = "/" + relativePath.Replace('\\', '/').TrimStart('/');
        string name = rel[(rel.LastIndexOf('/') + 1)..];
        foreach (var (regex, matchPath) in _rules)
        {
            bool hit = matchPath ? regex.IsMatch(rel) || regex.IsMatch(rel[1..]) : regex.IsMatch(name);
            if (hit)
            {
                return true;
            }
        }

        return false;
    }

    internal static Regex GlobToRegex(string glob)
    {
        string pattern = "^" + Regex.Escape(glob).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
