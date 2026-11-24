using System.IO.Compression;
using TrainerDeck.Core.Library;

namespace TrainerDeck.Tests.Library;

public class TrainerImportTests
{
    [Fact]
    public void Copies_an_exe_without_overwriting()
    {
        using var src = new TempDir();
        using var dest = new TempDir();
        string exe = src.File("Hades Plus 15 Trainer.exe", 100);
        dest.File("Hades Plus 15 Trainer.exe", 5);

        TrainerImportResult r = TrainerImport.Import(exe, dest.Path, new TrainerImportOptions(GameFolder: false));

        Assert.True(r.Success);
        Assert.EndsWith("Hades Plus 15 Trainer (2).exe", r.Destination);
        Assert.Equal(100, new FileInfo(r.Destination!).Length);
        Assert.True(File.Exists(exe));
    }

    [Fact]
    public void Extracts_a_zip_into_its_own_folder_and_removes_the_copy()
    {
        using var src = new TempDir();
        using var dest = new TempDir();
        string zip = Path.Combine(src.Path, "Elden Ring Trainer.zip");
        using (ZipArchive a = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            using var w = new StreamWriter(a.CreateEntry("Elden Ring Plus 35 Trainer.exe").Open());
            w.Write("x");
        }

        TrainerImportResult r = TrainerImport.Import(zip, dest.Path);

        Assert.True(r.Success);
        Assert.True(File.Exists(Path.Combine(dest.Path, "Elden Ring Trainer", "Elden Ring Plus 35 Trainer.exe")));
        Assert.False(File.Exists(Path.Combine(dest.Path, "Elden Ring Trainer.zip")));
    }

    [Fact]
    public void Rejects_other_files_and_missing_files()
    {
        using var src = new TempDir();
        using var dest = new TempDir();
        Assert.False(TrainerImport.Import(src.File("notes.txt"), dest.Path).Success);
        Assert.False(TrainerImport.Import(Path.Combine(src.Path, "gone.exe"), dest.Path).Success);
    }

    [Fact]
    public void Broken_zip_leaves_nothing_behind()
    {
        using var src = new TempDir();
        using var dest = new TempDir();
        string zip = src.File("bad.zip", 64);

        Assert.False(TrainerImport.Import(zip, dest.Path).Success);
        Assert.Empty(Directory.EnumerateFileSystemEntries(dest.Path));
    }

    [Fact]
    public void Puts_an_exe_in_its_games_folder()
    {
        using var src = new TempDir();
        using var dest = new TempDir();
        string exe = src.File("Dead Cells v1.9-v35 Plus 13 Trainer 64 Bit (Epic Games Store, Game Pass).exe", 100);

        TrainerImportResult r = TrainerImport.Import(exe, dest.Path);

        Assert.True(r.Success);
        Assert.Equal(Path.Combine(dest.Path, "Dead Cells", Path.GetFileName(exe)), r.Destination);
    }

    [Fact]
    public void Refuses_an_identical_trainer_already_in_the_folder()
    {
        using var src = new TempDir();
        using var dest = new TempDir();
        string exe = src.File("Hades Plus 15 Trainer.exe", 100);
        Assert.True(TrainerImport.Import(exe, dest.Path).Success);

        string renamed = src.File("Hades copy.exe", 1);
        File.Copy(exe, renamed, overwrite: true);
        TrainerImportResult again = TrainerImport.Import(renamed, dest.Path);

        Assert.False(again.Success);
        Assert.True(again.Duplicate);
        Assert.Single(Directory.GetFiles(dest.Path, "*.exe", SearchOption.AllDirectories));
    }

    [Fact]
    public void Same_size_but_different_content_is_not_a_duplicate()
    {
        using var src = new TempDir();
        using var dest = new TempDir();
        string first = src.File("A Plus 1 Trainer.exe", 100);
        Assert.True(TrainerImport.Import(first, dest.Path).Success);
        string other = Path.Combine(src.Path, "B Plus 1 Trainer.exe");
        File.WriteAllBytes(other, Enumerable.Repeat((byte)7, 100).ToArray());

        Assert.True(TrainerImport.Import(other, dest.Path).Success);
    }

    [Fact]
    public void Can_delete_the_original_after_copying()
    {
        using var src = new TempDir();
        using var dest = new TempDir();
        string exe = src.File("Hades Plus 15 Trainer.exe", 100);

        TrainerImportResult r = TrainerImport.Import(exe, dest.Path, new TrainerImportOptions(DeleteOriginal: true));

        Assert.True(r.Success);
        Assert.False(File.Exists(exe));
        Assert.True(File.Exists(r.Destination));
    }
}
