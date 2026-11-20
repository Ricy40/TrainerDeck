using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Input;
using TrainerDeck.Core.Library;
using TrainerDeck.Core.Text;
using TrainerDeck.UI.Input;

namespace TrainerDeck.UI.ViewModels;


public sealed partial class IgnorePatternsViewModel : PageViewModel
{
    private readonly MainViewModel _main;

    public IgnorePatternsViewModel(MainViewModel main)
    {
        _main = main;
        text = string.Join(Environment.NewLine, main.Settings.Current.IgnorePatterns);
        if (main.LastDevice == InputSource.Controller)
        {
            TouchKeyboard.Show();
        }
    }

    [ObservableProperty]
    private string text;

    public override IReadOnlyList<FooterHint> Hints => [new(InputAction.Back, "Save and go back")];

    public override bool Handle(InputAction action) => false;

    public override void OnClosed()
    {
        List<string> patterns = Text.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (patterns.SequenceEqual(_main.Settings.Current.IgnorePatterns))
        {
            return;
        }

        _main.Settings.Update(s => s.IgnorePatterns = patterns);
        _ = _main.Services.Library.RescanAsync();
        _main.ShowMessage($"Saved {Humanize.Plural(patterns.Count, "ignore pattern")}; rescanning…");
    }
}

public sealed class ZipImportViewModel : RowListPageViewModel
{
    private readonly Dictionary<string, bool> _selected = new(StringComparer.OrdinalIgnoreCase);
    private bool _deleteAfter;
    private bool _busy;

    public ZipImportViewModel(MainViewModel main) : base(main)
    {
        Title = "Extract zip files";
        Body = "Each zip is extracted into a folder with the same name, next to the zip. Trainer Deck only reads .zip files; extract .rar or .7z files yourself.";
        foreach (string zip in main.Services.Library.PendingZips)
        {
            _selected[zip] = true;
        }

        Rebuild();
    }

    private void Rebuild()
    {
        var rows = new List<RowViewModel>();
        int count = _selected.Count(kv => kv.Value);
        rows.Add(new ActionRow(count == 1 ? "Extract 1 zip" : $"Extract {count} zips", Glyphs.Zip, () => _ = Extract(), "Extract") { IsEnabled = count > 0 && !_busy });
        rows.Add(new ToggleRow("Delete zips after extracting", _deleteAfter, v => _deleteAfter = v, "Only after a zip extracts successfully"));
        rows.Add(new HeaderRow("Zip files"));
        foreach (var (zip, on) in _selected.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            string z = zip;
            rows.Add(new ToggleRow(Path.GetFileName(z), on, v =>
            {
                _selected[z] = v;
                Rebuild();
            }, Path.GetDirectoryName(z)));
        }

        SetRows(rows);
        HintsChanged();
    }

    private async Task Extract()
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        List<string> zips = _selected.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
        Main.ShowMessage($"Extracting {Humanize.Plural(zips.Count, "zip")}…");
        Rebuild();
        bool delete = _deleteAfter;
        List<ZipImportResult> results = await Task.Run(() => zips.Select(z => ZipImporter.Extract(z, delete)).ToList());
        int failed = results.Count(r => !r.Success);
        foreach (ZipImportResult r in results.Where(r => !r.Success))
        {
            Log.Warn($"Zip extraction failed: {r.Error}");
        }

        _busy = false;
        _ = Main.Services.Library.RescanAsync();
        if (failed == 0)
        {
            Main.ShowMessage($"Extracted {Humanize.Plural(results.Count, "zip")}");
        }
        else
        {
            Main.ShowError($"{Humanize.Plural(failed, "zip")} couldn't be extracted. Details are in trainerdeck.log.");
        }

        if (Main.CurrentPage == this)
        {
            Main.Pop();
        }
    }
}

public sealed class NoticeViewModel : RowListPageViewModel
{
    public NoticeViewModel(MainViewModel main) : base(main)
    {
        Title = "Before you start: antivirus";
        Body = "Trainers change a game's memory while it runs, which is exactly what antivirus software watches for. "
            + "Windows Security and other antivirus tools often flag or quietly remove FLiNG trainers even when they're clean.\n\n"
            + "To stop trainers disappearing, add your trainer folder to your antivirus exclusions "
            + "(Windows Security › Virus & threat protection › Manage settings › Exclusions). "
            + "Only do this for trainers you downloaded from a source you trust.";
        string folder = main.Services.Library.Roots.FirstOrDefault() ?? main.Services.Paths.DefaultTrainersDirectory;
        SetRows(
        [
            new ActionRow("Got it", Glyphs.Check, Dismiss, "Close"),
            new ActionRow("Open Windows Security", Glyphs.Shield, OpenWindowsSecurity, "Open", "Opens the virus & threat protection settings"),
            new ActionRow("Open trainer folder", Glyphs.Folder, () => OpenFolder(folder), "Open", folder),
        ]);
    }

    protected override string BackLabel => "Close";

    public override void OnClosed()
    {
        if (!Main.Settings.Current.AntivirusNoticeDismissed)
        {
            Main.Settings.Update(s => s.AntivirusNoticeDismissed = true);
        }
    }

    private void Dismiss() => Main.Pop();

    private void OpenWindowsSecurity() => Start("windowsdefender://threatsettings/");

    private void OpenFolder(string folder)
    {
        Directory.CreateDirectory(folder);
        Start(folder);
    }

    private void Start(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{target}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not open {target}: {ex.Message}");
            Main.ShowError("Couldn't open that.");
        }
    }
}
