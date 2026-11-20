using TrainerDeck.Core.Steam;

namespace TrainerDeck.Tests.Steam;

public class VdfTests
{
    [Fact]
    public void Parses_nested_nodes_escapes_and_comments()
    {
        VdfNode root = Vdf.Parse("""
            // comment
            "libraryfolders"
            {
                "0"
                {
                    "path"    "C:\\Program Files (x86)\\Steam"
                    "apps" { "1245620" "123" }
                }
                "1" { "path" "D:\\Steam \"Library\"" }
            }
            """);

        VdfNode folders = root.GetNode("LIBRARYFOLDERS")!;
        Assert.Equal(@"C:\Program Files (x86)\Steam", folders.GetNode("0")!.GetString("path"));
        Assert.Equal("123", folders.GetNode("0")!.GetNode("apps")!.GetString("1245620"));
        Assert.Equal("D:\\Steam \"Library\"", folders.GetNode("1")!.GetString("path"));
        Assert.Equal(["0", "1"], folders.Keys);
    }

    [Fact]
    public void Truncated_input_does_not_throw() =>
        Assert.NotNull(Vdf.Parse("\"a\" { \"b\" \"c"));
}

public class SteamLibraryTests
{
    [Fact]
    public void Reads_new_and_old_library_folder_formats()
    {
        Assert.Equal([@"D:\SteamLibrary"], SteamLibrary.ParseLibraryFolders("""
            "libraryfolders" { "contentstatsid" "1" "1" { "path" "D:\\SteamLibrary" } }
            """));
        Assert.Equal([@"E:\Games"], SteamLibrary.ParseLibraryFolders("""
            "LibraryFolders" { "TimeNextStatsReport" "123" "1" "E:\\Games" }
            """));
    }

    [Fact]
    public void Parses_manifest_and_skips_tools()
    {
        SteamApp app = SteamLibrary.ParseManifest("""
            "AppState" { "appid" "1245620" "name" "ELDEN RING" "installdir" "ELDEN RING" "StateFlags" "4" }
            """, @"D:\SteamLibrary")!;
        Assert.Equal(1245620, app.AppId);
        Assert.Equal("ELDEN RING", app.Name);
        Assert.True(app.Installed);
        Assert.Equal(Path.Combine(@"D:\SteamLibrary", "steamapps", "common", "ELDEN RING"), app.InstallDirectory);

        Assert.Null(SteamLibrary.ParseManifest("""
            "AppState" { "appid" "228980" "name" "Steamworks Common Redistributables" "StateFlags" "4" }
            """, "x"));
        Assert.False(SteamLibrary.ParseManifest("""
            "AppState" { "appid" "10" "name" "Counter-Strike" "StateFlags" "1026" }
            """, "x")!.Installed);
    }

    [Fact]
    public void Finds_installed_apps_across_libraries()
    {
        using var dir = new TempDir();
        string steam = Path.Combine(dir.Path, "Steam");
        string lib2 = Path.Combine(dir.Path, "Lib2");
        dir.File("Steam/steamapps/libraryfolders.vdf", $$"""
            "libraryfolders" { "0" { "path" "{{steam.Replace("\\", "\\\\")}}" } "1" { "path" "{{lib2.Replace("\\", "\\\\")}}" } }
            """);
        dir.File("Steam/steamapps/appmanifest_1.acf", "\"AppState\" { \"appid\" \"1\" \"name\" \"One\" \"StateFlags\" \"4\" }");
        dir.File("Lib2/steamapps/appmanifest_2.acf", "\"AppState\" { \"appid\" \"2\" \"name\" \"Two\" \"StateFlags\" \"4\" }");

        List<SteamApp> apps = SteamLibrary.InstalledApps(steam);
        Assert.Equal([1, 2], apps.Select(a => a.AppId));
    }

    [Fact]
    public void Finds_local_art_in_old_and_new_cache_layouts()
    {
        using var dir = new TempDir();
        dir.File("appcache/librarycache/10_library_600x900.jpg");
        dir.File("appcache/librarycache/20/abc123/library_600x900.jpg");
        dir.File("appcache/librarycache/20/library_hero.jpg");

        Assert.EndsWith("10_library_600x900.jpg", SteamLibrary.FindLocalArt(dir.Path, 10, SteamArtKind.Portrait));
        Assert.EndsWith(Path.Combine("abc123", "library_600x900.jpg"), SteamLibrary.FindLocalArt(dir.Path, 20, SteamArtKind.Portrait));
        Assert.EndsWith("library_hero.jpg", SteamLibrary.FindLocalArt(dir.Path, 20, SteamArtKind.Hero));
        Assert.Null(SteamLibrary.FindLocalArt(dir.Path, 30, SteamArtKind.Portrait));
    }

    [Fact]
    public void Parses_store_search_results()
    {
        var results = SteamLibrary.ParseStoreSearch("""
            {"total":2,"items":[{"type":"app","name":"ELDEN RING","id":1245620},{"type":"sub","name":"Bundle","id":5},{"type":"app","name":"Nightreign","id":2622380}]}
            """);
        Assert.Equal([(1245620, "ELDEN RING"), (2622380, "Nightreign")], results);
        Assert.Empty(SteamLibrary.ParseStoreSearch("not json"));
    }

    [Fact]
    public void Search_url_escapes_the_term() =>
        Assert.Contains("term=Baldur%27s%20Gate%203", SteamLibrary.StoreSearchUrl("Baldur's Gate 3"));
}

public class SteamMatcherTests
{
    private static readonly (int, string)[] Apps =
    [
        (1245620, "ELDEN RING"),
        (2050650, "Resident Evil 4"),
        (292030, "The Witcher® 3: Wild Hunt"),
        (1086940, "Baldur's Gate 3"),
        (1145360, "Hades"),
        (1145350, "Hades II"),
        (377160, "Fallout 4"),
        (489830, "The Elder Scrolls V: Skyrim Special Edition"),
    ];

    [Theory]
    [InlineData("Elden Ring", 1245620)]
    [InlineData("The Witcher 3 Wild Hunt", 292030)]
    [InlineData("Baldurs Gate 3", 1086940)]
    [InlineData("Hades II", 1145350)]
    [InlineData("Hades", 1145360)]
    [InlineData("Resident Evil 4 (2023)", 2050650)]
    [InlineData("The Elder Scrolls V Skyrim", 489830)]
    public void Matches_installed_games(string trainerGame, int expected) =>
        Assert.Equal(expected, SteamMatcher.Match(trainerGame, Apps));

    [Theory]
    [InlineData("Fallout 3")]
    [InlineData("Elden")]
    [InlineData("")]
    public void Does_not_guess(string trainerGame) =>
        Assert.Null(SteamMatcher.Match(trainerGame, Apps));

    [Theory]
    [InlineData("The Witcher® 3: Wild Hunt", "the witcher 3 wild hunt")]
    [InlineData("Pokémon™ & Friends", "pokemon and friends")]
    [InlineData("  Baldur’s   Gate 3 ", "baldurs gate 3")]
    public void Normalizes(string input, string expected) =>
        Assert.Equal(expected, SteamMatcher.Normalize(input));
}
