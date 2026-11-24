using System.Xml.Linq;
using TrainerDeck.Core.Input;
using TrainerDeck.Core.Install;
using TrainerDeck.Core.Settings;

namespace TrainerDeck.Tests.Install;

public class InstallLayoutTests
{
    [Theory]
    [InlineData("0.1.0", "0.1.0", 0)]
    [InlineData("0.1.0.0", "0.1.0", 0)]
    [InlineData("0.1.0", "0.2.0", -1)]
    [InlineData("1.0.0", "0.9.5", 1)]
    [InlineData(null, "0.1.0", -1)]
    [InlineData("", "0.1.0", -1)]
    [InlineData("garbage", "0.1.0", -1)]
    public void Compares_installed_and_setup_versions(string? installed, string current, int expected) =>
        Assert.Equal(expected, Math.Sign(InstallLayout.CompareVersions(installed, current)));

    [Theory]
    [InlineData(@"C:\Downloads\TrainerDeck-Setup.exe", new string[0], true)]
    [InlineData(@"C:\Downloads\TrainerDeck-v0.2.0-setup.exe", new string[0], true)]
    [InlineData(@"C:\Downloads\setup.exe", new string[0], true)]
    [InlineData(@"C:\Tools\TrainerDeck.exe", new[] { "--install" }, true)]
    [InlineData(@"C:\Tools\TrainerDeck.exe", new string[0], false)]
    public void Detects_setup_invocations(string exe, string[] args, bool expected) =>
        Assert.Equal(expected, InstallLayout.IsSetupInvocation(exe, args));

    [Fact]
    public void Task_xml_runs_elevated_on_battery_without_time_limit()
    {
        string xml = InstallLayout.ElevatedTaskXml(@"C:\Program Files\Trainer Deck\TrainerDeck.exe", "S-1-5-21-1-2-3-1001");
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        XDocument doc = XDocument.Parse(xml);

        Assert.Equal("HighestAvailable", doc.Descendants(ns + "RunLevel").Single().Value);
        Assert.Equal("S-1-5-21-1-2-3-1001", doc.Descendants(ns + "UserId").Single().Value);
        Assert.Equal("false", doc.Descendants(ns + "DisallowStartIfOnBatteries").Single().Value);
        Assert.Equal("PT0S", doc.Descendants(ns + "ExecutionTimeLimit").Single().Value);
        Assert.Equal("\"C:\\Program Files\\Trainer Deck\\TrainerDeck.exe\"", doc.Descendants(ns + "Command").Single().Value);
        Assert.Equal(InstallLayout.FromTaskFlag, doc.Descendants(ns + "Arguments").Single().Value);
    }

    [Fact]
    public void Task_xml_escapes_paths() =>
        Assert.Contains("Games &amp; Tools", InstallLayout.ElevatedTaskXml(@"D:\Games & Tools\TrainerDeck.exe", "S-1"));

    [Fact]
    public void Uninstall_entry_points_at_the_installed_exe()
    {
        var values = InstallLayout.UninstallValues(@"C:\Program Files\Trainer Deck", "1.2.3", 70_000);
        Assert.Equal("\"C:\\Program Files\\Trainer Deck\\TrainerDeck.exe\" --uninstall", values["UninstallString"]);
        Assert.Equal("Trainer Deck", values["DisplayName"]);
        Assert.Equal(70_000, values["EstimatedSize"]);
    }

    [Fact]
    public void Unique_destination_appends_a_number()
    {
        using var dir = new TempDir();
        dir.File("Hades Trainer.exe");
        dir.File("Hades Trainer (2).exe");
        Assert.EndsWith("Hades Trainer (3).exe", InstallLayout.UniqueDestination(dir.Path, "Hades Trainer.exe"));
        Assert.EndsWith("Other.exe", InstallLayout.UniqueDestination(dir.Path, "Other.exe"));
    }

    [Theory]
    [InlineData(@"C:\Program Files\Trainer Deck\TrainerDeck.exe", @"C:\Program Files\Trainer Deck", true)]
    [InlineData(@"C:\Program Files\Trainer Deck2\TrainerDeck.exe", @"C:\Program Files\Trainer Deck", false)]
    public void Checks_containment(string path, string dir, bool expected) =>
        Assert.Equal(expected, InstallLayout.IsUnder(path, dir));
}

public class InstalledPathsTests
{
    [Fact]
    public void Installed_copy_keeps_data_out_of_the_app_folder_even_if_writable()
    {
        using var app = new TempDir();
        using var data = new TempDir();
        app.File(PortablePaths.InstallMarkerFile, "{}");

        PortablePaths paths = PortablePaths.Resolve(app.Path, data.Path);

        Assert.True(paths.IsInstalled);
        Assert.False(paths.IsPortable);
        Assert.Equal(data.Path, paths.DataDirectory);
        Assert.Equal(Path.Combine(app.Path, "Trainers"), paths.DefaultTrainersDirectory);
    }

    [Fact]
    public void Portable_copy_is_unchanged()
    {
        using var app = new TempDir();
        PortablePaths paths = PortablePaths.Resolve(app.Path);
        Assert.True(paths.IsPortable);
        Assert.False(paths.IsInstalled);
    }
}

public class ComboDetectorTests
{
    [Fact]
    public void Fires_once_when_all_buttons_are_held()
    {
        var c = new ComboDetector { Combo = GamepadButtons.Back | GamepadButtons.Start };
        Assert.False(c.Update(GamepadButtons.Back));
        Assert.True(c.Update(GamepadButtons.Back | GamepadButtons.Start));
        Assert.False(c.Update(GamepadButtons.Back | GamepadButtons.Start | GamepadButtons.A));
        Assert.False(c.Update(GamepadButtons.Start));
        Assert.True(c.Update(GamepadButtons.Back | GamepadButtons.Start));
    }

    [Fact]
    public void Empty_combo_never_fires() =>
        Assert.False(new ComboDetector().Update(GamepadButtons.Back | GamepadButtons.Start));
}
