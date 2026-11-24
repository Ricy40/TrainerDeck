using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Input;
using TrainerDeck.Core.Library;
using TrainerDeck.Core.Text;
using TrainerDeck.UI.Input;

namespace TrainerDeck.UI.ViewModels;

public sealed record InfoLine(string Label, string Value);

public sealed partial class DetailsViewModel : RowListPageViewModel
{
    private readonly AppServices _s;
    private readonly EventHandler _onRunning;
    private readonly EventHandler _onLibrary;

    public DetailsViewModel(MainViewModel main, TrainerCardViewModel card) : base(main)
    {
        _s = main.Services;
        Card = card;
        _onRunning = (_, _) => main.Dispatcher.BeginInvoke(Rebuild);
        _onLibrary = (_, _) => main.Dispatcher.BeginInvoke(OnLibraryChanged);
        _s.Launcher.Running.Changed += _onRunning;
        _s.Library.Changed += _onLibrary;
        Rebuild();
        LoadHero();
    }

    public TrainerCardViewModel Card { get; }

    private TrainerEntry Entry => Card.Entry;

    [ObservableProperty]
    private ImageSource? hero;

    [ObservableProperty]
    private IReadOnlyList<InfoLine> info = [];

    [ObservableProperty]
    private bool isRenaming;

    [ObservableProperty]
    private string renameText = "";

    public event EventHandler? RenameStarted;

    public override IReadOnlyList<FooterHint> Hints => IsRenaming
        ? [new(InputAction.Activate, "Save name"), new(InputAction.Back, "Cancel")]
        : base.Hints;

    protected override bool HandlePage(InputAction action)
    {
        if (!IsRenaming)
        {
            return false;
        }

        if (action == InputAction.Activate)
        {
            CommitRename();
        }
        else if (action == InputAction.Back)
        {
            IsRenaming = false;
            HintsChanged();
        }

        return true;
    }

    public override void OnClosed()
    {
        _s.Launcher.Running.Changed -= _onRunning;
        _s.Library.Changed -= _onLibrary;
    }

    private void OnLibraryChanged()
    {
        if (_s.Library.IsScanning)
        {
            return;
        }

        if (!_s.Library.Entries.Any(e => GameGroup.KeyFor(e) == Card.Group.Key && !e.Meta.Hidden))
        {
            Main.ShowMessage($"{Card.Title} is no longer in your trainer folders.");
            Main.PopTo(Main.Library);
            return;
        }

        Main.Library.Regroup(Card);
        Rebuild();
    }

    private void Rebuild()
    {
        TrainerMetadata m = Entry.Meta;
        bool running = _s.Launcher.Running.IsRunning(Card.Id);
        Card.IsRunning = running;
        int? appId = m.SteamAppId;
        bool steamInstalled = appId is int id && _s.Steam.IsInstalled(id);

        var rows = new List<RowViewModel>();
        if (running)
        {
            rows.Add(new ActionRow("Cheats", Glyphs.Game, () => Main.OpenCheats(Card), "Open", "Turn cheats on and off with the controller"));
            rows.Add(new ActionRow("Show trainer window", Glyphs.View, () => Main.Services.Launcher.Tracker.BringToFront(Main.Services.Launcher.Running.TryGet(Card.Id)?.ProcessId ?? 0), "Switch to"));
        }
        else
        {
            rows.Add(new ActionRow("Launch trainer", Glyphs.Play, () => _ = Main.Library.Launch(Card), "Launch",
                Card.HasVersions ? $"Default version: {GameGroup.Describe(Card.Group.Default)}" : null));
        }

        if (steamInstalled)
        {
            string game = _s.Steam.InstalledName(appId!.Value) ?? "game";
            rows.Add(new ActionRow($"Play {game}", Glyphs.Game, () => PlayGame(appId.Value), "Play", "Starts the game through Steam"));
        }

        if (running)
        {
            rows.Add(new ActionRow("Close trainer", Glyphs.Close, () => _ = Main.Library.CloseTrainer(Card), "Close") { IsDanger = true });
        }

        if (Card.HasVersions)
        {
            rows.Add(new HeaderRow("Versions"));
            foreach (TrainerEntry v in Card.Versions)
            {
                bool isDefault = v.Id == Card.Group.Default.Id;
                bool isRunning = _s.Launcher.Running.IsRunning(v.Id);
                string detail = string.Join("  ·  ", new[] { isDefault ? "Default" : null, isRunning ? "Running" : null, v.File.FileName }
                    .Where(x => x is not null));
                TrainerEntry version = v;
                rows.Add(new ActionRow(GameGroup.Describe(v), isDefault ? Glyphs.Check : Glyphs.Document,
                    () => MakeDefault(version), isDefault ? "Default" : "Use by default", detail,
                    () => ConfirmDelete(version), "Delete"));
            }

            rows.Add(new HeaderRow("Game"));
        }

        rows.Add(new ActionRow(m.Favourite ? "Remove from favourites" : "Add to favourites", m.Favourite ? Glyphs.StarFill : Glyphs.Star,
            () => Main.Library.ToggleFavourite(Card), m.Favourite ? "Unfavourite" : "Favourite"));
        rows.Add(new ActionRow("Rename", Glyphs.Rename, StartRename, "Rename",
            m.DisplayName is null ? null : $"Detected name: {Entry.File.Parsed.GameName}"));
        rows.Add(new ActionRow("Change art", Glyphs.Image, PickArt, "Choose",
            m.ArtPath is not null ? "Using your own image"
                : appId is not null ? "Using Steam art"
                : _s.Art.HasTrainerCover(Card.Id) ? "Using the cover from the trainer window"
                : "No game art yet: run the trainer once, or choose an image",
            m.ArtPath is null ? null : ResetArt, "Use default art"));
        rows.Add(new ActionRow("Show in folder", Glyphs.Folder, () => Main.Library.ShowInFolder(Card), "Open", Entry.File.Directory));
        if (appId is int linked)
        {
            rows.Add(new ActionRow("Unlink Steam game", Glyphs.Unlink, () => Unlink(), "Unlink",
                $"Matched to {(_s.Steam.InstalledName(linked) ?? $"Steam app {linked}")}. Unlink it if that's the wrong game."));
        }

        rows.Add(new ActionRow(m.Hidden ? "Show in library" : "Hide from library", m.Hidden ? Glyphs.View : Glyphs.Hide, ToggleHidden,
            m.Hidden ? "Unhide" : "Hide") { IsDanger = !m.Hidden });
        rows.Add(new ActionRow(Card.HasVersions ? "Delete this version" : "Delete trainer", Glyphs.Delete, () => ConfirmDelete(Entry), "Delete",
            $"{Entry.File.FileName}  ·  moves it to the Recycle Bin") { IsDanger = true });

        Title = Entry.DisplayName;
        Subtitle = string.Join("  ·  ", new[]
            {
                Entry.File.Parsed.Version,
                Card.OptionsBadge is string o ? $"{o} options" : null,
                Card.HasVersions ? $"{Card.Versions.Count} versions" : null,
            }.Where(x => !string.IsNullOrEmpty(x)));
        Info = BuildInfo(m);
        SetRows(rows);
        HintsChanged();
    }

    private List<InfoLine> BuildInfo(TrainerMetadata m)
    {
        DateTime now = DateTime.UtcNow;
        var lines = new List<InfoLine>
        {
            new("File", Entry.File.FileName),
            new("Last launched", m.LastLaunchedUtc is DateTime t ? Humanize.Ago(t, now) : "Never"),
            new("Launches", m.LaunchCount.ToString()),
            new("Added", Humanize.Ago(m.FirstSeenUtc, now)),
        };
        if (m.SteamAppId is int id)
        {
            lines.Add(new("Steam", _s.Steam.InstalledName(id) is string n ? $"{n} (installed)" : $"App {id}"));
        }

        return lines;
    }

    private async void LoadHero()
    {
        ImageSource? image = await _s.Art.GetHeroAsync(Entry);
        Hero = image;
    }

    private void MakeDefault(TrainerEntry version)
    {
        if (version.Id == Card.Group.Default.Id)
        {
            return;
        }

        Main.Library.MakeDefault(Card, version);
        Main.ShowMessage($"{GameGroup.Describe(version)} is now the default");
        Rebuild();
    }

    private void ConfirmDelete(TrainerEntry version)
    {
        string what = Card.HasVersions ? $"this version of {Card.Title}" : Card.Title;
        Main.Push(new ConfirmViewModel(Main, $"Delete {what}?",
            $"{version.File.FileName} goes to the Recycle Bin, so you can restore it from there.",
            "Delete", Glyphs.Delete, () => _ = Main.Library.DeleteTrainer(version)));
    }

    private void PlayGame(int appId)
    {
        if (_s.Steam.LaunchGame(appId))
        {
            Main.ShowMessage("Starting the game through Steam…");
        }
        else
        {
            Main.ShowError("Couldn't ask Steam to start the game.");
        }
    }

    private void StartRename()
    {
        RenameText = Entry.DisplayName;
        IsRenaming = true;
        HintsChanged();
        RenameStarted?.Invoke(this, EventArgs.Empty);
        if (Main.LastDevice == InputSource.Controller)
        {
            TouchKeyboard.Show();
        }
    }

    private void CommitRename()
    {
        string name = RenameText.Trim();
        IsRenaming = false;
        string? value = name.Length == 0 || name == Entry.File.Parsed.GameName ? null : name;
        foreach (TrainerEntry v in Card.Versions)
        {
            _s.Library.UpdateMetadata(v.Id, m => m.DisplayName = value);
        }
        Main.ShowMessage(value is null ? "Name reset to the detected name" : $"Renamed to {value}");
        _ = _s.Steam.RefreshAsync();
        HintsChanged();
    }

    private void PickArt()
    {
        string start = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        Main.PickImage($"Choose art for {Entry.DisplayName}", start, path =>
        {
            try
            {
                string relative = _s.Art.ImportCustomArt(path, Card.Id);
                _s.Art.DeleteCustomArt(Entry);
                foreach (TrainerEntry v in Card.Versions)
                {
                    _s.Library.UpdateMetadata(v.Id, m => m.ArtPath = relative);
                }
                Card.ReloadCover(_s.Art, Main.Dispatcher);
                Main.ShowMessage("Art updated");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warn($"Could not import art {path}: {ex.Message}");
                Main.ShowError("Couldn't copy that image.");
            }
        });
    }

    private void ResetArt()
    {
        _s.Art.DeleteCustomArt(Entry);
        foreach (TrainerEntry v in Card.Versions)
        {
            _s.Library.UpdateMetadata(v.Id, m => m.ArtPath = null);
        }
        Card.ReloadCover(_s.Art, Main.Dispatcher);
        Main.ShowMessage("Using the default art again");
    }

    private void Unlink()
    {
        _s.Steam.SetAppId(Card.Id, null);
        Card.ReloadCover(_s.Art, Main.Dispatcher);
        Hero = null;
        Rebuild();
        Main.ShowMessage("Unlinked from Steam");
    }

    private void ToggleHidden()
    {
        bool hide = !Entry.Meta.Hidden;
        foreach (TrainerEntry v in Card.Versions)
        {
            _s.Library.UpdateMetadata(v.Id, m => m.Hidden = hide);
        }
        if (hide)
        {
            Main.ShowMessage($"{Entry.DisplayName} is hidden. Unhide it from Settings › Hidden trainers.");
            Main.PopTo(Main.Library);
        }
        else
        {
            Main.ShowMessage($"{Entry.DisplayName} is back in your library");
        }
    }
}
