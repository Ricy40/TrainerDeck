namespace TrainerDeck.Core.Library;

public enum LibraryTab
{
    All,
    Favourites,
    Recent,
}

public enum SortMode
{
    Alphabetical,
    RecentlyLaunched,
    RecentlyAdded,
}

public static class LibraryQuery
{
    public static List<TrainerEntry> Apply(
        IEnumerable<TrainerEntry> entries,
        LibraryTab tab,
        SortMode sort,
        string? search,
        bool includeHidden = false)
    {
        string[] tokens = Tokenize(search);
        IEnumerable<TrainerEntry> q = entries.Where(e => includeHidden || !e.Meta.Hidden);

        q = tab switch
        {
            LibraryTab.Favourites => q.Where(e => e.Meta.Favourite),
            LibraryTab.Recent => q.Where(e => e.Meta.LastLaunchedUtc is not null),
            _ => q,
        };

        if (tokens.Length > 0)
        {
            q = q.Where(e => Matches(e, tokens));
        }

        SortMode effective = tab == LibraryTab.Recent ? SortMode.RecentlyLaunched : sort;
        var list = q.ToList();
        list.Sort(Comparer(effective));
        return list;
    }

    public static List<GameGroup> ApplyGroups(
        IEnumerable<TrainerEntry> entries,
        LibraryTab tab,
        SortMode sort,
        string? search,
        bool includeHidden = false)
    {
        string[] tokens = Tokenize(search);
        IEnumerable<GameGroup> q = GameGroup.Build(entries.Where(e => includeHidden || !e.Meta.Hidden));
        q = tab switch
        {
            LibraryTab.Favourites => q.Where(g => g.Versions.Any(v => v.Meta.Favourite)),
            LibraryTab.Recent => q.Where(g => g.Versions.Any(v => v.Meta.LastLaunchedUtc is not null)),
            _ => q,
        };

        if (tokens.Length > 0)
        {
            q = q.Where(g => g.Versions.Any(v => Matches(v, tokens)));
        }

        SortMode effective = tab == LibraryTab.Recent ? SortMode.RecentlyLaunched : sort;
        Comparison<TrainerEntry> compare = Comparer(effective);
        var list = q.ToList();
        list.Sort((a, b) => compare(Representative(a, effective), Representative(b, effective)));
        return list;
    }

    private static TrainerEntry Representative(GameGroup g, SortMode sort) => sort switch
    {
        SortMode.RecentlyLaunched => g.Versions.MaxBy(v => v.Meta.LastLaunchedUtc ?? DateTime.MinValue)!,
        SortMode.RecentlyAdded => g.Versions.MaxBy(v => v.File.CreatedUtc)!,
        _ => g.Default,
    };

    public static SortMode Next(SortMode sort) => sort switch
    {
        SortMode.Alphabetical => SortMode.RecentlyLaunched,
        SortMode.RecentlyLaunched => SortMode.RecentlyAdded,
        _ => SortMode.Alphabetical,
    };

    public static string Label(SortMode sort) => sort switch
    {
        SortMode.RecentlyLaunched => "Recently launched",
        SortMode.RecentlyAdded => "Recently added",
        _ => "A–Z",
    };

    internal static string[] Tokenize(string? search) =>
        string.IsNullOrWhiteSpace(search)
            ? []
            : search.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(FlingNameParser.Normalize)
                .Where(t => t.Length > 0)
                .ToArray();

    internal static bool Matches(TrainerEntry e, string[] tokens)
    {
        string text = $"{e.DisplayName} {e.File.Parsed.GameName} {Path.GetFileNameWithoutExtension(e.File.FileName)}";
        string[] words = text.Split([' ', '.', '_', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Select(FlingNameParser.Normalize)
            .ToArray();
        string joined = string.Concat(words);
        return tokens.All(t => words.Any(w => w.StartsWith(t, StringComparison.Ordinal))
            || (t.Length >= 4 && joined.Contains(t, StringComparison.Ordinal)));
    }

    private static Comparison<TrainerEntry> Comparer(SortMode sort)
    {
        static int Alpha(TrainerEntry a, TrainerEntry b)
        {
            int c = string.Compare(a.SortKey, b.SortKey, StringComparison.OrdinalIgnoreCase);
            return c != 0 ? c : string.Compare(a.File.FullPath, b.File.FullPath, StringComparison.OrdinalIgnoreCase);
        }

        return sort switch
        {
            SortMode.RecentlyLaunched => (a, b) =>
            {
                DateTime la = a.Meta.LastLaunchedUtc ?? DateTime.MinValue;
                DateTime lb = b.Meta.LastLaunchedUtc ?? DateTime.MinValue;
                int c = lb.CompareTo(la);
                return c != 0 ? c : Alpha(a, b);
            },
            SortMode.RecentlyAdded => (a, b) =>
            {
                int c = b.File.CreatedUtc.CompareTo(a.File.CreatedUtc);
                return c != 0 ? c : Alpha(a, b);
            },
            _ => Alpha,
        };
    }
}
