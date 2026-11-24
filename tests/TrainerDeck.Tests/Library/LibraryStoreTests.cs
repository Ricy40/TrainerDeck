using TrainerDeck.Core.Library;
using TrainerDeck.Core.Settings;
using TrainerDeck.Core.Storage;

namespace TrainerDeck.Tests.Library;

public class LibraryStoreTests
{
    private static TrainerFile Trainer(string rel, long size) =>
        new(TrainerId.Create(rel, size), Path.Combine("X:", rel), "X:", rel, size, DateTime.UtcNow, DateTime.UtcNow,
            new ParsedTrainerName("Game", null, null, NameSource.FileName));

    [Fact]
    public void Overrides_survive_save_and_reload()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "library.json");
        var store = LibraryStore.Load(path);
        TrainerFile t = Trainer(@"Elden Ring\Trainer.exe", 100);

        TrainerMetadata m = store.GetOrCreate(t, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        m.DisplayName = "My Elden Ring";
        m.Favourite = true;
        m.SteamAppId = 1245620;
        store.Save();

        TrainerMetadata reloaded = LibraryStore.Load(path).TryGet(t.Id)!;
        Assert.Equal("My Elden Ring", reloaded.DisplayName);
        Assert.True(reloaded.Favourite);
        Assert.Equal(1245620, reloaded.SteamAppId);
        Assert.Equal("elden ring/trainer.exe", reloaded.RelativePath);
    }

    [Fact]
    public void Entry_follows_a_trainer_updated_in_place()
    {
        using var dir = new TempDir();
        var store = LibraryStore.Load(Path.Combine(dir.Path, "library.json"));
        store.GetOrCreate(Trainer("Game/Trainer.exe", 100), DateTime.UtcNow).Favourite = true;

        TrainerMetadata updated = store.GetOrCreate(Trainer("Game/Trainer.exe", 200), DateTime.UtcNow);

        Assert.True(updated.Favourite);
        Assert.Equal(1, store.Count);
        Assert.Null(store.TryGet(TrainerId.Create("Game/Trainer.exe", 100)));
    }

    [Fact]
    public void Corrupt_file_is_backed_up_and_store_starts_empty()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "library.json");
        File.WriteAllText(path, "{ not json");
        string? warning = null;

        var store = LibraryStore.Load(path, w => warning = w);

        Assert.Equal(0, store.Count);
        Assert.NotNull(warning);
        Assert.Single(Directory.GetFiles(dir.Path, "library.json.corrupt-*"));
    }

    [Fact]
    public void Settings_round_trip_with_defaults()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "settings.json");
        AppSettings defaults = JsonFile.LoadOrDefault<AppSettings>(path);
        Assert.False(defaults.StartFullScreen);
        Assert.Equal(CloseButtonBehavior.Ask, defaults.CloseButton);

        JsonFile.Save(path, new AppSettings { AfterLaunch = AfterLaunchBehavior.Close, TrainerFolders = [@"D:\Trainers"] });
        AppSettings s = JsonFile.LoadOrDefault<AppSettings>(path);

        Assert.Equal(AfterLaunchBehavior.Close, s.AfterLaunch);
        Assert.Equal([@"D:\Trainers"], s.TrainerFolders);
        Assert.Contains("\"close\"", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Portable_paths_use_app_folder_when_writable()
    {
        using var dir = new TempDir();
        PortablePaths p = PortablePaths.Resolve(dir.Path, Path.Combine(dir.Path, "fallback"));
        Assert.True(p.IsPortable);
        Assert.Equal(Path.Combine(dir.Path, "settings.json"), p.SettingsFile);
        Assert.Equal(Path.Combine(dir.Path, "Trainers"), p.DefaultTrainersDirectory);
        Assert.Equal([p.DefaultTrainersDirectory], new AppSettings().EffectiveTrainerFolders(p));
    }
}
