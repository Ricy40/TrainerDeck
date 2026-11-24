using TrainerDeck.Core.Library;

namespace TrainerDeck.Tests.Library;

public class GameGroupTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static TrainerEntry E(string fileName, int modifiedDay = 0, int? launchedDay = null, bool preferred = false, bool fav = false, bool hidden = false)
    {
        string rel = fileName;
        var file = new TrainerFile(TrainerId.Create(rel, 1), Path.Combine("X:", rel), "X:", rel, 1, T0, T0.AddDays(modifiedDay),
            FlingNameParser.Parse(Path.Combine("X:", rel), "X:"));
        var meta = new TrainerMetadata
        {
            Preferred = preferred,
            Favourite = fav,
            Hidden = hidden,
            LastLaunchedUtc = launchedDay is int d ? T0.AddDays(d) : null,
        };
        return new TrainerEntry(file, meta);
    }

    private const string Dc32 = "Dead Cells v1.9-v35 Plus 13 Trainer 32 Bit (Steam, GOG).exe";
    private const string Dc64 = "Dead Cells v1.9-v35 Plus 13 Trainer 64 Bit (Epic Games Store, Game Pass).exe";

    [Theory]
    [InlineData(Dc32, "32-bit · Steam, GOG")]
    [InlineData(Dc64, "64-bit · Epic Games Store, Game Pass")]
    [InlineData("Hades v1.0-v1.38 Plus 15 Trainer x64.exe", "64-bit")]
    [InlineData("Baldurs Gate 3 v4.1.1 Plus 21 Trainer Updated 2025.04.16.exe", "Updated 2025.04.16")]
    [InlineData("Resident Evil 4 v1.0-v20260203 Plus 36 Trainer(1).exe", null)]
    [InlineData("Elden Ring v1.02-v1.16.1 Plus 35 Trainer.exe", null)]
    public void Reads_the_build_notes_after_trainer(string file, string? expected) =>
        Assert.Equal(expected, FlingNameParser.Variant(file));

    [Fact]
    public void Trainers_for_the_same_game_become_one_group()
    {
        List<GameGroup> groups = GameGroup.Build([E(Dc32), E(Dc64), E("Hades v1.0-v1.38 Plus 15 Trainer x64.exe")]);
        Assert.Equal(2, groups.Count);
        GameGroup dc = groups.Single(g => g.HasVersions);
        Assert.Equal(2, dc.Versions.Count);
        Assert.Equal("Dead Cells", dc.Default.DisplayName);
    }

    [Fact]
    public void Default_is_preferred_then_last_launched_then_newest()
    {
        Assert.Equal(Dc32, GameGroup.Build([E(Dc32, preferred: true), E(Dc64, launchedDay: 5)]).Single().Default.File.FileName);
        Assert.Equal(Dc64, GameGroup.Build([E(Dc32, launchedDay: 1), E(Dc64, launchedDay: 5)]).Single().Default.File.FileName);
        Assert.Equal(Dc32, GameGroup.Build([E(Dc32, modifiedDay: 9), E(Dc64, modifiedDay: 2)]).Single().Default.File.FileName);
    }

    [Fact]
    public void Default_is_listed_first()
    {
        GameGroup g = GameGroup.Build([E(Dc32), E(Dc64, preferred: true)]).Single();
        Assert.Equal(Dc64, g.Versions[0].File.FileName);
    }

    [Fact]
    public void Describe_tells_versions_apart()
    {
        Assert.Equal("v1.9-v35  ·  32-bit · Steam, GOG  ·  +13 options", GameGroup.Describe(E(Dc32)));
    }

    [Fact]
    public void Query_returns_one_result_per_game_and_matches_any_version()
    {
        TrainerEntry[] entries = [E(Dc32), E(Dc64, fav: true), E("Hades v1.0-v1.38 Plus 15 Trainer x64.exe"), E("Elden Ring v1.02 Plus 35 Trainer.exe", hidden: true)];
        Assert.Equal(2, LibraryQuery.ApplyGroups(entries, LibraryTab.All, SortMode.Alphabetical, null).Count);
        Assert.Equal("Dead Cells", LibraryQuery.ApplyGroups(entries, LibraryTab.Favourites, SortMode.Alphabetical, null).Single().Default.DisplayName);
        Assert.Single(LibraryQuery.ApplyGroups(entries, LibraryTab.All, SortMode.Alphabetical, "epic"));
    }
}
