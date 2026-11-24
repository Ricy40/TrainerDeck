using TrainerDeck.Core.Library;

namespace TrainerDeck.Tests.Library;

public class LibraryQueryTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static TrainerEntry E(string game, int createdDay = 0, int? launchedDay = null, bool fav = false, bool hidden = false, string? displayName = null)
    {
        string rel = game + " Plus 10 Trainer.exe";
        var file = new TrainerFile(TrainerId.Create(rel, 1), Path.Combine("X:", rel), "X:", rel, 1, T0.AddDays(createdDay), T0,
            new ParsedTrainerName(game, null, 10, NameSource.FileName));
        var meta = new TrainerMetadata
        {
            Favourite = fav,
            Hidden = hidden,
            DisplayName = displayName,
            LastLaunchedUtc = launchedDay is int d ? T0.AddDays(d) : null,
        };
        return new TrainerEntry(file, meta);
    }

    private static readonly TrainerEntry[] Sample =
    [
        E("The Witcher 3", createdDay: 1, launchedDay: 5),
        E("Elden Ring", createdDay: 3, fav: true),
        E("Cyberpunk 2077", createdDay: 2, launchedDay: 9, fav: true),
        E("Hades II", createdDay: 0, hidden: true),
        E("Baldur's Gate 3", createdDay: 4),
    ];

    private static string[] Names(IEnumerable<TrainerEntry> e) => e.Select(x => x.DisplayName).ToArray();

    [Fact]
    public void All_tab_sorts_alphabetically_ignoring_the_and_hides_hidden() =>
        Assert.Equal(["Baldur's Gate 3", "Cyberpunk 2077", "Elden Ring", "The Witcher 3"],
            Names(LibraryQuery.Apply(Sample, LibraryTab.All, SortMode.Alphabetical, null)));

    [Fact]
    public void Hidden_can_be_included() =>
        Assert.Contains("Hades II", Names(LibraryQuery.Apply(Sample, LibraryTab.All, SortMode.Alphabetical, null, includeHidden: true)));

    [Fact]
    public void Favourites_tab() =>
        Assert.Equal(["Cyberpunk 2077", "Elden Ring"], Names(LibraryQuery.Apply(Sample, LibraryTab.Favourites, SortMode.Alphabetical, null)));

    [Fact]
    public void Recent_tab_is_always_by_recency() =>
        Assert.Equal(["Cyberpunk 2077", "The Witcher 3"], Names(LibraryQuery.Apply(Sample, LibraryTab.Recent, SortMode.Alphabetical, null)));

    [Fact]
    public void Sort_by_recently_launched_puts_never_launched_last() =>
        Assert.Equal(["Cyberpunk 2077", "The Witcher 3", "Baldur's Gate 3", "Elden Ring"],
            Names(LibraryQuery.Apply(Sample, LibraryTab.All, SortMode.RecentlyLaunched, null)));

    [Fact]
    public void Sort_by_recently_added() =>
        Assert.Equal(["Baldur's Gate 3", "Elden Ring", "Cyberpunk 2077", "The Witcher 3"],
            Names(LibraryQuery.Apply(Sample, LibraryTab.All, SortMode.RecentlyAdded, null)));

    [Theory]
    [InlineData("elden", "Elden Ring")]
    [InlineData("ELD rin", "Elden Ring")]
    [InlineData("baldurs", "Baldur's Gate 3")]
    [InlineData("2077", "Cyberpunk 2077")]
    [InlineData("witcher 3", "The Witcher 3")]
    public void Search_matches_all_tokens_loosely(string search, string expected) =>
        Assert.Equal([expected], Names(LibraryQuery.Apply(Sample, LibraryTab.All, SortMode.Alphabetical, search)));

    [Fact]
    public void Short_tokens_match_word_starts_only()
    {
        TrainerEntry[] entries = [E("Elden Ring"), E("Starfield")];
        Assert.Equal(["Elden Ring"], Names(LibraryQuery.Apply(entries, LibraryTab.All, SortMode.Alphabetical, "eld")));
        Assert.Equal(["Starfield"], Names(LibraryQuery.Apply(entries, LibraryTab.All, SortMode.Alphabetical, "field")));
    }

    [Fact]
    public void Search_with_no_match_is_empty() =>
        Assert.Empty(LibraryQuery.Apply(Sample, LibraryTab.All, SortMode.Alphabetical, "zelda"));

    [Fact]
    public void Display_name_override_is_used_for_sort_and_search()
    {
        TrainerEntry[] entries = [E("Elden Ring", displayName: "Zzz Souls"), E("Baldur's Gate 3")];
        Assert.Equal(["Baldur's Gate 3", "Zzz Souls"], Names(LibraryQuery.Apply(entries, LibraryTab.All, SortMode.Alphabetical, null)));
        Assert.Single(LibraryQuery.Apply(entries, LibraryTab.All, SortMode.Alphabetical, "zzz"));
        Assert.Single(LibraryQuery.Apply(entries, LibraryTab.All, SortMode.Alphabetical, "elden"));
    }

    [Fact]
    public void Sort_cycles() =>
        Assert.Equal(SortMode.Alphabetical, LibraryQuery.Next(LibraryQuery.Next(LibraryQuery.Next(SortMode.Alphabetical))));
}
