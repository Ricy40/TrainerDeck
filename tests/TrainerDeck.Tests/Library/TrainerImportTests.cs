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

        TrainerImportResult r = TrainerImport.Import(exe, dest.Path);

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
}
