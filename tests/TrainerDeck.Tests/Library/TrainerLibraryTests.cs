using TrainerDeck.Core.Library;
using TrainerDeck.Core.Settings;

namespace TrainerDeck.Tests.Library;

public class TrainerLibraryTests
{
    private static (TrainerLibrary Library, SettingsStore Settings) Create(TempDir dir)
    {
        PortablePaths paths = PortablePaths.Resolve(dir.Path);
        var settings = new SettingsStore(paths.SettingsFile);
        var store = LibraryStore.Load(paths.LibraryFile);
        return (new TrainerLibrary(settings, paths, store), settings);
    }

    [Fact]
    public async Task Creates_default_folder_and_scans_it()
    {
        using var dir = new TempDir();
        var (library, _) = Create(dir);
        using var _l = library;

        await library.RescanAsync();
        Assert.True(Directory.Exists(Path.Combine(dir.Path, "Trainers")));
        Assert.Empty(library.Entries);

        dir.File("Trainers/Elden Ring v1.16 Plus 35 Trainer.exe");
        await library.RescanAsync();

        TrainerEntry e = Assert.Single(library.Entries);
        Assert.Equal("Elden Ring", e.DisplayName);
        Assert.True(File.Exists(Path.Combine(dir.Path, "library.json")));
    }

    [Fact]
    public async Task Metadata_survives_rescans()
    {
        using var dir = new TempDir();
        var (library, _) = Create(dir);
        using var _l = library;
        dir.File("Trainers/Game Plus 5 Trainer.exe");

        await library.RescanAsync();
        library.Entries[0].Meta.Favourite = true;
        library.SaveMetadata();
        await library.RescanAsync();

        Assert.True(library.Entries[0].Meta.Favourite);
    }

    [Fact]
    public async Task Same_relative_path_in_two_roots_keeps_separate_metadata()
    {
        using var dir = new TempDir();
        var (library, settings) = Create(dir);
        using var _l = library;
        dir.File("A/Trainer.exe", bytes: 10);
        dir.File("B/Trainer.exe", bytes: 20);
        settings.Current.TrainerFolders.AddRange([Path.Combine(dir.Path, "A"), Path.Combine(dir.Path, "B")]);

        await library.RescanAsync();
        library.Entries[0].Meta.DisplayName = "First";
        library.Entries[1].Meta.DisplayName = "Second";
        await library.RescanAsync();

        Assert.Equal(["First", "Second"], library.Entries.Select(e => e.Meta.DisplayName!).Order().ToArray());
    }

    [Fact]
    public async Task Watcher_picks_up_new_trainers()
    {
        using var dir = new TempDir();
        var (library, _) = Create(dir);
        using var _l = library;
        await library.RescanAsync();

        var changed = new TaskCompletionSource();
        library.Changed += (_, _) =>
        {
            if (!library.IsScanning && library.Entries.Count == 1)
            {
                changed.TrySetResult();
            }
        };
        library.StartWatching();
        dir.File("Trainers/Hades II Plus 20 Trainer.exe");

        Task done = await Task.WhenAny(changed.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.Same(changed.Task, done);
    }
}
