using TrainerDeck.Core.Library;

namespace TrainerDeck.Tests.Library;

public class FlingNameParserTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "TD", "Trainers");

    private static string P(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    private static ParsedTrainerName Parse(string relative) => FlingNameParser.Parse(P(relative), Root);

    [Theory]

    [InlineData("Elden Ring v1.02-v1.16 Plus 35 Trainer.exe", "Elden Ring", "v1.02-v1.16", 35)]
    [InlineData("Hades II Early Access Plus 20 Trainer.exe", "Hades II", "Early Access", 20)]
    [InlineData("Cyberpunk 2077 v2.1+ Plus 30 Trainer.exe", "Cyberpunk 2077", "v2.1+", 30)]

    [InlineData("Monster Hunter Wilds Plus 50 Trainer.exe", "Monster Hunter Wilds", null, 50)]
    [InlineData("Baldur's Gate 3 v4.1.1.3624901+ Plus 37 Trainer.exe", "Baldur's Gate 3", "v4.1.1.3624901+", 37)]
    [InlineData("Resident Evil 4 (2023) v1.0-v20230407 Plus 25 Trainer.exe", "Resident Evil 4 (2023)", "v1.0-v20230407", 25)]
    [InlineData("Starfield v1.7.23-v1.8.86 Plus 45 Trainer Updated 2023.11.29.exe", "Starfield", "v1.7.23-v1.8.86", 45)]
    [InlineData("Grand Theft Auto V Plus 15 Trainer.exe", "Grand Theft Auto V", null, 15)]
    [InlineData("The Witcher 3 Wild Hunt v4.04 Plus 30 Trainer [Steam].exe", "The Witcher 3 Wild Hunt", "v4.04", 30)]
    [InlineData("Hogwarts Legacy Early Access v1.0 Plus 30 Trainer.exe", "Hogwarts Legacy", "Early Access v1.0", 30)]
    [InlineData("Dead Island 2 v1.0 - v2.0 Plus 26 Trainer.exe", "Dead Island 2", "v1.0 - v2.0", 26)]
    [InlineData("Lies of P v1.2.0.0+ Plus 24 Trainer.exe", "Lies of P", "v1.2.0.0+", 24)]
    [InlineData("Street Fighter 6 v20230602+ Plus 9 Trainer.exe", "Street Fighter 6", "v20230602+", 9)]
    [InlineData("Persona 5 Royal Build 12345678 Plus 20 Trainer.exe", "Persona 5 Royal", "Build 12345678", 20)]
    [InlineData("Elden Ring Steam v1.10 Plus 35 Trainer.exe", "Elden Ring", "Steam v1.10", 35)]
    [InlineData("Ys X Nordics v1.0.2 Plus 20 Trainer.exe", "Ys X Nordics", "v1.0.2", 20)]
    [InlineData("Stellar Blade +40 Trainer.exe", "Stellar Blade", null, 40)]
    [InlineData("Vampire Survivors v1.0+ Plus 18 Trainer.exe", "Vampire Survivors", "v1.0+", 18)]
    [InlineData("Sid Meier's Civilization VII v1.0 Plus 15 Trainer.exe", "Sid Meier's Civilization VII", "v1.0", 15)]

    [InlineData("ELDEN RING v1.02 Plus 35 Trainer (1).exe", "ELDEN RING", "v1.02", 35)]
    [InlineData("Elden Ring v1.02 Plus 35 Trainer - Copy (2).exe", "Elden Ring", "v1.02", 35)]
    [InlineData("Elden_Ring_v1.02-v1.16_Plus_35_Trainer.exe", "Elden Ring", "v1.02-v1.16", 35)]
    [InlineData("Elden-Ring-v1.16-Plus-35-Trainer.exe", "Elden Ring", "v1.16", 35)]
    [InlineData("Hades.II.v0.90-v0.92.Plus.20.Trainer-FLiNG.exe", "Hades II", "v0.90-v0.92", 20)]
    [InlineData("  weird   spacing  Plus 5  Trainer .exe", "Weird Spacing", null, 5)]
    [InlineData("Elden Ring Plus 35 Trainer by FLiNG.exe", "Elden Ring", null, 35)]
    [InlineData("[FLiNG] Elden Ring v1.16 Plus 35 Trainer.exe", "Elden Ring", "v1.16", 35)]
    [InlineData("Elden Ring Trainer.exe", "Elden Ring", null, null)]
    [InlineData("NieR Automata.exe", "NieR Automata", null, null)]
    public void Parses_file_names(string file, string game, string? version, int? options)
    {
        ParsedTrainerName r = Parse(file);
        Assert.Equal(game, r.GameName);
        Assert.Equal(version, r.Version);
        Assert.Equal(options, r.OptionCount);
        Assert.Equal(NameSource.FileName, r.Source);
    }

    [Theory]
    [InlineData("Elden.Ring.Trainer-FLiNG/Trainer.exe", "Elden Ring", null, null)]
    [InlineData("Hades.II.v0.90-v0.92.Plus.20.Trainer-FLiNG/Trainer.exe", "Hades II", "v0.90-v0.92", 20)]
    [InlineData("My Game/trainer.exe", "My Game", null, null)]
    [InlineData("Some Game/Plus 10 Trainer.exe", "Some Game", null, 10)]
    [InlineData("Elden Ring v1.16 Plus 35 Trainer/FLiNG Trainer.exe", "Elden Ring", "v1.16", 35)]
    [InlineData("Cyberpunk 2077/x64/Trainer.exe", "Cyberpunk 2077", null, null)]
    [InlineData("Cyberpunk 2077/New folder (3)/Trainer.exe", "Cyberpunk 2077", null, null)]
    [InlineData("Hades II/v1.0 Plus 20 Trainer.exe", "Hades II", "v1.0", 20)]
    public void Falls_back_to_folder_names(string file, string game, string? version, int? options)
    {
        ParsedTrainerName r = Parse(file);
        Assert.Equal(game, r.GameName);
        Assert.Equal(version, r.Version);
        Assert.Equal(options, r.OptionCount);
        Assert.Equal(NameSource.FolderName, r.Source);
    }

    [Fact]
    public void Fills_version_from_matching_parent_folder()
    {
        ParsedTrainerName r = Parse("Elden Ring v1.16 Plus 35 Trainer/Elden Ring Trainer.exe");
        Assert.Equal("Elden Ring", r.GameName);
        Assert.Equal("v1.16", r.Version);
        Assert.Equal(35, r.OptionCount);
        Assert.Equal(NameSource.FileName, r.Source);
    }

    [Fact]
    public void Does_not_take_version_from_unrelated_parent_folder()
    {
        ParsedTrainerName r = Parse("Old Stuff v2 Plus 3 Trainer/Elden Ring Trainer.exe");
        Assert.Equal("Elden Ring", r.GameName);
        Assert.Null(r.Version);
        Assert.Null(r.OptionCount);
    }

    [Fact]
    public void Never_uses_the_scan_root_as_a_name()
    {
        ParsedTrainerName r = Parse("Trainer.exe");
        Assert.Equal(NameSource.Fallback, r.Source);
        Assert.Equal("Trainer", r.GameName);
    }

    [Fact]
    public void Fallback_strips_noise_when_nothing_parses()
    {
        ParsedTrainerName r = Parse("FLiNG Trainer Plus 12.exe");
        Assert.Equal(NameSource.Fallback, r.Source);
        Assert.False(string.IsNullOrWhiteSpace(r.GameName));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".exe")]
    [InlineData("---.exe")]
    [InlineData("v1.0 Plus 5 Trainer.exe")]
    [InlineData("((((((.exe")]
    public void Never_throws_and_always_returns_a_name(string file)
    {
        ParsedTrainerName r = FlingNameParser.Parse(P(file), Root);
        Assert.False(string.IsNullOrWhiteSpace(r.GameName));
    }

    [Fact]
    public void Works_without_a_scan_root()
    {
        string path = Path.Combine(Path.GetTempPath(), "Elden.Ring.Trainer-FLiNG", "Trainer.exe");
        ParsedTrainerName r = FlingNameParser.Parse(path);
        Assert.Equal("Elden Ring", r.GameName);
    }

    [Theory]
    [InlineData("The Witcher 3", "WITCHER 3", 'W')]
    [InlineData("Elden Ring", "ELDEN RING", 'E')]
    [InlineData("7 Days to Die", "7 DAYS TO DIE", '#')]
    [InlineData("Theme Hospital", "THEME HOSPITAL", 'T')]
    [InlineData("Ōkami HD", "ŌKAMI HD", '#')]
    public void Sort_key_and_jump_letter(string name, string sortKey, char letter)
    {
        var r = new ParsedTrainerName(name, null, null, NameSource.FileName);
        Assert.Equal(sortKey, r.SortKey);
        Assert.Equal(letter, r.JumpLetter);
    }
}
