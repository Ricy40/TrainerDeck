namespace TrainerDeck.Core.Library;

public sealed record GameGroup(string Key, TrainerEntry Default, IReadOnlyList<TrainerEntry> Versions)
{
    public bool HasVersions => Versions.Count > 1;

    public bool Contains(string trainerId) => Versions.Any(v => v.Id == trainerId);

    public static string KeyFor(TrainerEntry entry) => FlingNameParser.Normalize(entry.File.Parsed.GameName);

    public static List<GameGroup> Build(IEnumerable<TrainerEntry> entries)
    {
        var groups = new List<GameGroup>();
        foreach (IGrouping<string, TrainerEntry> g in entries.GroupBy(KeyFor, StringComparer.Ordinal))
        {
            List<TrainerEntry> versions = g.ToList();
            TrainerEntry chosen = versions.FirstOrDefault(v => v.Meta.Preferred)
                ?? versions.Where(v => v.Meta.LastLaunchedUtc is not null).MaxBy(v => v.Meta.LastLaunchedUtc)
                ?? versions.MaxBy(v => v.File.ModifiedUtc)!;
            versions.Sort((a, b) => a == chosen ? -1 : b == chosen ? 1 : string.Compare(a.File.FileName, b.File.FileName, StringComparison.OrdinalIgnoreCase));
            groups.Add(new GameGroup(g.Key, chosen, versions));
        }

        return groups;
    }

    public static string Describe(TrainerEntry entry)
    {
        ParsedTrainerName p = entry.File.Parsed;
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(p.Version))
        {
            parts.Add(p.Version);
        }

        if (FlingNameParser.Variant(entry.File.FileName) is string variant)
        {
            parts.Add(variant);
        }

        if (p.OptionCount is int n)
        {
            parts.Add($"+{n} options");
        }

        return parts.Count == 0 ? entry.File.FileName : string.Join("  ·  ", parts);
    }
}
