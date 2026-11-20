using System.IO.Compression;
using TrainerDeck.Core.Library;

namespace TrainerDeck.Tests.Library;

public class IgnoreRulesTests
{
    [Theory]
    [InlineData("unins000.exe", true)]
    [InlineData("Game/unins001.exe", true)]
    [InlineData("setup.exe", true)]
    [InlineData("GameSetup_x64.exe", true)]
    [InlineData("vcredist_x64.exe", true)]
    [InlineData("VC_redist.x64.exe", true)]
    [InlineData("DXSETUP.exe", true)]
    [InlineData("Game/_CommonRedist/anything.exe", true)]
    [InlineData("__MACOSX/Trainer.exe", true)]
    [InlineData("Elden Ring v1.16 Plus 35 Trainer.exe", false)]
    [InlineData("Hades II/Trainer.exe", false)]
    public void Built_in_patterns(string rel, bool ignored) =>
        Assert.Equal(ignored, new IgnoreRules().IsIgnored(rel));

    [Fact]
    public void User_patterns_on_names_and_paths()
    {
        var rules = new IgnoreRules(["*old*", "Archive/*", "# a comment", "  "]);
        Assert.True(rules.IsIgnored("Elden Ring OLD Trainer.exe"));
        Assert.True(rules.IsIgnored(@"Archive\Elden Ring Trainer.exe"));
        Assert.False(rules.IsIgnored("Elden Ring Trainer.exe"));
        Assert.False(rules.IsIgnored("Elden Ring/Archive.exe"));
    }

    [Fact]
    public void Built_ins_can_be_disabled()
    {
        Assert.False(new IgnoreRules(includeBuiltIns: false).IsIgnored("setup.exe"));
    }
}

public class TrainerScannerTests
{
    [Fact]
    public void Finds_trainers_recursively_and_skips_junk()
    {
        using var dir = new TempDir();
        dir.File("Elden Ring v1.16 Plus 35 Trainer.exe");
        dir.File("Elden.Ring.Trainer-FLiNG/Trainer.exe");
        dir.File("A/B/C/Hades II Plus 20 Trainer.exe");
        dir.File("Game/unins000.exe");
        dir.File("vcredist_x64.exe");
        dir.File("readme.txt");

        ScanResult result = new TrainerScanner(new IgnoreRules()).Scan([dir.Path]);

        Assert.Empty(result.Errors);
        Assert.Equal(3, result.Trainers.Count);
        Assert.Equal(["Elden Ring", "Elden Ring", "Hades II"], result.Trainers.Select(t => t.Parsed.GameName).ToArray());
    }

    [Fact]
    public void Ids_are_stable_and_include_size()
    {
        using var dir = new TempDir();
        dir.File("Sub/Game Plus 5 Trainer.exe", bytes: 42);

        TrainerFile t1 = new TrainerScanner(new IgnoreRules()).Scan([dir.Path]).Trainers.Single();
        TrainerFile t2 = new TrainerScanner(new IgnoreRules()).Scan([dir.Path + Path.DirectorySeparatorChar]).Trainers.Single();

        Assert.Equal("sub/game plus 5 trainer.exe|42", t1.Id);
        Assert.Equal(t1.Id, t2.Id);
    }

    [Fact]
    public void Overlapping_and_missing_roots()
    {
        using var dir = new TempDir();
        dir.File("Nested/Game Plus 5 Trainer.exe");
        string missing = Path.Combine(dir.Path, "does-not-exist");

        ScanResult result = new TrainerScanner(new IgnoreRules())
            .Scan([dir.Path, Path.Combine(dir.Path, "Nested"), missing, "  "]);

        Assert.Single(result.Trainers);
        Assert.Single(result.Errors);
        Assert.Contains("not found", result.Errors[0]);
    }

    [Fact]
    public void Reports_zips_that_have_not_been_extracted()
    {
        using var dir = new TempDir();
        dir.File("New Game.zip");
        dir.File("Old Game.zip");
        Directory.CreateDirectory(Path.Combine(dir.Path, "Old Game"));

        ScanResult result = new TrainerScanner(new IgnoreRules()).Scan([dir.Path]);

        Assert.Equal("New Game.zip", Path.GetFileName(Assert.Single(result.PendingZips)));
    }
}

public class ZipImporterTests
{
    [Fact]
    public void Extracts_into_own_subfolder()
    {
        using var dir = new TempDir();
        string zip = Path.Combine(dir.Path, "Elden Ring Trainer.zip");
        using (ZipArchive a = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            a.CreateEntry("Elden Ring v1.16 Plus 35 Trainer.exe");
            a.CreateEntry("docs/readme.txt");
        }

        ZipImportResult r = ZipImporter.Extract(zip);

        Assert.True(r.Success, r.Error);
        Assert.Equal(Path.Combine(dir.Path, "Elden Ring Trainer"), r.TargetFolder);
        Assert.True(File.Exists(Path.Combine(r.TargetFolder!, "Elden Ring v1.16 Plus 35 Trainer.exe")));
        Assert.True(File.Exists(Path.Combine(r.TargetFolder!, "docs", "readme.txt")));
        Assert.True(File.Exists(zip));
    }

    [Fact]
    public void Picks_unique_folder_and_can_delete_zip()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(Path.Combine(dir.Path, "Game"));
        string zip = Path.Combine(dir.Path, "Game.zip");
        using (ZipArchive a = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            a.CreateEntry("Trainer.exe");
        }

        ZipImportResult r = ZipImporter.Extract(zip, deleteZipAfter: true);

        Assert.True(r.Success, r.Error);
        Assert.Equal(Path.Combine(dir.Path, "Game (2)"), r.TargetFolder);
        Assert.False(File.Exists(zip));
    }

    [Fact]
    public void Rejects_zip_slip_and_cleans_up()
    {
        using var dir = new TempDir();
        string zip = Path.Combine(dir.Path, "Evil.zip");
        using (ZipArchive a = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            a.CreateEntry("../../escaped.exe");
        }

        ZipImportResult r = ZipImporter.Extract(zip);

        Assert.False(r.Success);
        Assert.False(Directory.Exists(Path.Combine(dir.Path, "Evil")));
    }

    [Fact]
    public void Corrupt_zip_fails_gracefully()
    {
        using var dir = new TempDir();
        string zip = dir.File("Broken.zip", bytes: 100);

        ZipImportResult r = ZipImporter.Extract(zip);

        Assert.False(r.Success);
        Assert.NotNull(r.Error);
    }
}
