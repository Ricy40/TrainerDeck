using TrainerDeck.Core.Launch;
using TrainerDeck.Core.Library;
using TrainerDeck.Core.Settings;

namespace TrainerDeck.Tests.Launch;

public class LaunchErrorsTests
{
    [Theory]
    [InlineData(2, "missing")]
    [InlineData(3, "missing")]
    [InlineData(225, "Windows Security")]
    [InlineData(226, "Windows Security")]
    [InlineData(5, "denied")]
    [InlineData(193, "isn't a program")]
    [InlineData(1260, "policy")]
    [InlineData(31, "error 31")]
    public void Describes_common_failures(int code, string expected) =>
        Assert.Contains(expected, LaunchErrors.Describe(code, "Elden Ring"));

    [Fact]
    public void Message_names_the_trainer() =>
        Assert.StartsWith("Elden Ring", LaunchErrors.Describe(2, "Elden Ring"));

    [Theory]
    [InlineData(2, true)]
    [InlineData(225, true)]
    [InlineData(5, false)]
    [InlineData(1223, false)]
    public void Missing_file_codes_trigger_rescan(int code, bool expected) =>
        Assert.Equal(expected, LaunchErrors.SuggestsMissingFile(code));

    [Fact]
    public void Failed_result_carries_message()
    {
        LaunchResult r = LaunchResult.Failed(225, "Hades");
        Assert.Equal(LaunchOutcome.Failed, r.Outcome);
        Assert.Equal(225, r.ErrorCode);
        Assert.Contains("Hades", r.Message);
        Assert.Null(LaunchResult.Cancelled().Message);
    }
}

public class StartupOptionsTests
{
    [Fact]
    public void Parses_flags_case_insensitively()
    {
        var o = StartupOptions.Parse(["--ELEVATED", "other"]);
        Assert.True(o.ElevatedRelaunch);
        Assert.False(o.NoElevate);
        Assert.True(StartupOptions.Parse(["--no-elevate"]).NoElevate);
    }

    [Theory]
    [InlineData(true, false, new string[0], true)]
    [InlineData(false, false, new string[0], false)]
    [InlineData(true, true, new string[0], false)]
    [InlineData(true, false, new[] { "--elevated" }, false)]
    [InlineData(true, false, new[] { "--no-elevate" }, false)]
    public void Relaunches_only_once_and_only_when_needed(bool setting, bool elevated, string[] args, bool expected) =>
        Assert.Equal(expected, StartupOptions.Parse(args).ShouldRelaunchElevated(setting, elevated));
}

public class RunningTrainersTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Add_and_remove_raise_changed()
    {
        var set = new RunningTrainers();
        int changes = 0;
        set.Changed += (_, _) => changes++;

        Assert.True(set.Add(new RunningProcess("a", 10, T0)));
        Assert.False(set.Add(new RunningProcess("a", 10, T0)));
        Assert.True(set.IsRunning("a"));
        Assert.True(set.IsTracked(10));
        Assert.Equal(1, changes);

        Assert.True(set.Remove("a", 10));
        Assert.False(set.IsRunning("a"));
        Assert.Equal(2, changes);
    }

    [Fact]
    public void Stale_exit_does_not_remove_a_newer_process()
    {
        var set = new RunningTrainers();
        set.Add(new RunningProcess("a", 10, T0));
        set.Add(new RunningProcess("a", 20, T0));

        Assert.False(set.Remove("a", 10));
        Assert.Equal(20, set.TryGet("a")?.ProcessId);
    }

    [Fact]
    public void Matcher_compares_paths_case_insensitively()
    {
        var matches = ProcessMatcher.Match(
            [(100, @"C:\Games\Trainers\ELDEN RING Trainer.exe"), (200, @"C:\Windows\explorer.exe"), (300, "")],
            [("elden", @"c:\games\trainers\elden ring trainer.exe"), ("hades", @"C:\Games\Trainers\Hades.exe")]);

        Assert.Equal(100, Assert.Single(matches).Value);
        Assert.True(matches.ContainsKey("elden"));
    }

    [Theory]
    [InlineData(@"\\?\C:\Trainers\a.exe", @"C:\Trainers\a.exe")]
    [InlineData(@"\\?\UNC\nas\share\a.exe", @"\\nas\share\a.exe")]
    [InlineData("C:/Trainers/a.exe", @"C:\Trainers\a.exe")]
    public void Normalizes_extended_paths(string input, string expected) =>
        Assert.Equal(expected, ProcessMatcher.Normalize(input));
}

public class RecordLaunchTests
{
    [Fact]
    public async Task Records_time_and_count_and_persists()
    {
        using var dir = new TempDir();
        PortablePaths paths = PortablePaths.Resolve(dir.Path);
        var now = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        using var library = new TrainerLibrary(new SettingsStore(paths.SettingsFile), paths, LibraryStore.Load(paths.LibraryFile), () => now);
        dir.File("Trainers/Hades Plus 10 Trainer.exe");
        await library.RescanAsync();
        string id = Assert.Single(library.Entries).Id;

        int changes = 0;
        library.Changed += (_, _) => changes++;
        library.RecordLaunch(id);
        library.RecordLaunch(id);

        Assert.Equal(2, changes);
        TrainerMetadata reloaded = LibraryStore.Load(paths.LibraryFile).TryGet(id)!;
        Assert.Equal(now, reloaded.LastLaunchedUtc);
        Assert.Equal(2, reloaded.LaunchCount);
    }
}
