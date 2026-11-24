using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainerDeck.Art;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Input;
using TrainerDeck.Core.Launch;
using TrainerDeck.Core.Settings;
using TrainerDeck.Core.Library;
using TrainerDeck.Launch;

namespace TrainerDeck.UI.ViewModels;

public sealed partial class LibraryViewModel : PageViewModel
{
    private readonly TrainerLibrary _library;
    private readonly ArtService _art;
    private readonly LaunchService _launcher;
    private readonly Dispatcher _dispatcher;
    private readonly MainViewModel _main;
    private readonly Dictionary<string, TrainerCardViewModel> _cards = new(StringComparer.Ordinal);

    public LibraryViewModel(TrainerLibrary library, ArtService art, LaunchService launcher, Dispatcher dispatcher, MainViewModel main)
    {
        _library = library;
        _art = art;
        _launcher = launcher;
        _dispatcher = dispatcher;
        _main = main;
        _library.Changed += (_, _) => _dispatcher.BeginInvoke(OnLibraryChanged);
        _launcher.Running.Changed += (_, _) => _dispatcher.BeginInvoke(UpdateRunning);
        _art.Steam.Changed += (_, _) => _dispatcher.BeginInvoke(ReloadCovers);
        main.Services.Cheats.CoverCaptured += id => _dispatcher.BeginInvoke(() =>
        {
            if (_cards.TryGetValue(id, out TrainerCardViewModel? card))
            {
                card.ReloadCover(_art, _dispatcher);
            }
        });
        _art.GenericIconsChanged += (_, _) => _dispatcher.BeginInvoke(() =>
        {
            foreach (TrainerCardViewModel card in _cards.Values)
            {
                card.IsGenericIcon = card.IconFingerprint is string fp && _art.IsGenericIcon(fp);
            }
        });
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingZips))]
    private string? pendingZipsLabel;

    public bool HasPendingZips => PendingZipsLabel is not null;

    public int PendingZipCount => _library.PendingZips.Count;

    [RelayCommand]
    private void OpenZips() => _main.OpenZipImport();

    [RelayCommand]
    private void OpenSettings() => _main.OpenSettings();

    [RelayCommand]
    private void AddTrainer() => _main.AddTrainer();

    [RelayCommand]
    private void OpenCheats(TrainerCardViewModel? card)
    {
        if (card is not null)
        {
            Selected = card;
            _main.OpenCheats(card);
        }
    }

    [RelayCommand]
    private void OpenDetails(TrainerCardViewModel? card)
    {
        if (card is not null)
        {
            Selected = card;
            _main.OpenDetails(card);
        }
    }

    public TrainerCardViewModel? CardFor(string id)
    {
        if (_cards.TryGetValue(id, out TrainerCardViewModel? card))
        {
            return card;
        }

        TrainerEntry? entry = _library.Entries.FirstOrDefault(e => e.Id == id);
        if (entry is null)
        {
            return null;
        }

        card = new TrainerCardViewModel(entry);
        _cards[id] = card;
        card.EnsureArt(_art, _dispatcher);
        return card;
    }

    private void ReloadCovers()
    {
        foreach (TrainerCardViewModel card in _cards.Values)
        {
            card.ReloadCover(_art, _dispatcher);
        }
    }

    [ObservableProperty]
    private ObservableCollection<TrainerCardViewModel> items = new();

    [ObservableProperty]
    private TrainerCardViewModel? selected;

    [ObservableProperty]
    private LibraryTab tab;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SortLabel))]
    private SortMode sort;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearch))]
    private string searchText = "";

    [ObservableProperty]
    private bool isScanning;

    [ObservableProperty]
    private bool isEmpty;

    [ObservableProperty]
    private string emptyTitle = "";

    [ObservableProperty]
    private string emptyDetail = "";

    [ObservableProperty]
    private bool showOpenFolder;

    [ObservableProperty]
    private string summary = "";

    public int Columns { get; set; } = 1;

    public int PageRows { get; set; } = 2;

    public string SortLabel => LibraryQuery.Label(Sort);

    public bool HasSearch => SearchText.Length > 0;

    public string PrimaryFolder => _library.Roots.FirstOrDefault() ?? "";

    public ObservableCollection<RailLetterViewModel> RailLetters { get; } =
        new(LetterIndex.Letters.Select(c => new RailLetterViewModel(c)));

    [ObservableProperty]
    private bool isRailOpen;

    [ObservableProperty]
    private string railLetter = "";

    private char _railLetter;

    public override IReadOnlyList<FooterHint> Hints => IsRailOpen
    ? [
        new(InputAction.Down, "Pick a letter"),
        new(InputAction.LetterRail, "Release to jump"),
        new(InputAction.Back, "Cancel"),
    ]
    : LibraryHints();

    private FooterHint[] LibraryHints()
    {
        var hints = new List<FooterHint>
        {
            new(InputAction.Activate, Selected?.IsRunning == true ? "Cheats" : "Launch", Primary: true),
        };
        if (Selected?.IsRunning == true && !HasSearch)
        {
            hints.Add(new(InputAction.CloseTrainer, "Close trainer", Primary: true));
        }

        hints.Add(new(InputAction.Favourite, Selected?.IsFavourite == true ? "Unfavourite" : "Favourite"));
        hints.Add(new(InputAction.Details, "Details"));
        hints.Add(new(InputAction.NextTab, "Switch tab", "Tab · hold for A–Z"));
        hints.Add(new(InputAction.Sort, "Sort"));
        hints.Add(HasSearch ? new(InputAction.Back, "Clear search") : new(InputAction.Settings, "Settings"));
        return hints.ToArray();
    }

    private string? _closeArmedFor;
    private DateTime _closeArmedAt;

    private void RequestCloseSelected()
    {
        if (Selected is not { IsRunning: true } card)
        {
            return;
        }

        if (_closeArmedFor == card.Id && DateTime.UtcNow - _closeArmedAt < TimeSpan.FromSeconds(3))
        {
            _closeArmedFor = null;
            _ = CloseTrainer(card);
            return;
        }

        _closeArmedFor = card.Id;
        _closeArmedAt = DateTime.UtcNow;
        string key = _main.LastDevice == InputSource.Controller ? "B" : "Delete";
        _main.ShowMessage($"Press {key} again to close {card.Title}");
    }

    partial void OnTabChanged(LibraryTab value) => Refresh();

    partial void OnSortChanged(SortMode value) => Refresh();

    partial void OnSearchTextChanged(string value)
    {
        Refresh();
        HintsChanged();
    }

    partial void OnSelectedChanged(TrainerCardViewModel? value) => HintsChanged();

    public override bool Handle(InputAction action)
    {
        if (IsRailOpen || action == InputAction.LetterRail)
        {
            return HandleRail(action);
        }

        switch (action)
        {
            case InputAction.Up or InputAction.Down or InputAction.Left or InputAction.Right
                or InputAction.PageUp or InputAction.PageDown or InputAction.Home or InputAction.End:
                MoveSelection(action);
                return true;
            case InputAction.Activate:
                _ = Activate(Selected);
                return true;
            case InputAction.Favourite:
                ToggleFavourite(Selected);
                return true;
            case InputAction.Details:
                if (Selected is not null)
                {
                    _main.OpenDetails(Selected);
                }

                return true;
            case InputAction.NextTab:
                Tab = (LibraryTab)(((int)Tab + 1) % 3);
                return true;
            case InputAction.PrevTab:
                Tab = (LibraryTab)(((int)Tab + 2) % 3);
                return true;
            case InputAction.Sort:
                CycleSort();
                return true;
            case InputAction.Rescan:
                RescanCommand.Execute(null);
                return true;
            case InputAction.Back when HasSearch:
                SearchText = "";
                return true;
            case InputAction.CloseTrainer:
                RequestCloseSelected();
                return true;
            case InputAction.Back when _main.LastDevice == InputSource.Controller && Selected?.IsRunning == true:
                RequestCloseSelected();
                return true;
            default:
                return false;
        }
    }

    private bool HandleRail(InputAction action)
    {
        switch (action)
        {
            case InputAction.LetterRail:
                OpenRail();
                break;
            case InputAction.Up or InputAction.Left:
                MoveRail(-1);
                break;
            case InputAction.Down or InputAction.Right:
                MoveRail(1);
                break;
            case InputAction.PageUp:
                MoveRail(-5);
                break;
            case InputAction.PageDown:
                MoveRail(5);
                break;
            case InputAction.LetterRailEnd or InputAction.Activate:
                JumpToLetter(_railLetter);
                CloseRail();
                break;
            case InputAction.Back:
                CloseRail();
                break;
        }

        return true;
    }

    private void OpenRail()
    {
        if (IsRailOpen || Items.Count == 0)
        {
            return;
        }

        HashSet<char> available = LetterIndex.Available(Items.Select(c => c.Entry.JumpLetter));
        foreach (RailLetterViewModel l in RailLetters)
        {
            l.IsAvailable = available.Contains(l.Letter);
        }

        char start = Selected?.Entry.JumpLetter ?? Items[0].Entry.JumpLetter;
        SetRailLetter(start);
        IsRailOpen = true;
        HintsChanged();
    }

    private void MoveRail(int delta)
    {
        var available = new HashSet<char>(RailLetters.Where(l => l.IsAvailable).Select(l => l.Letter));
        SetRailLetter(LetterIndex.Step(_railLetter, delta, available));
    }

    private void SetRailLetter(char letter)
    {
        _railLetter = letter;
        RailLetter = letter.ToString();
        foreach (RailLetterViewModel l in RailLetters)
        {
            l.IsCurrent = l.Letter == letter;
        }
    }

    public void JumpToLetter(char letter)
    {
        int index = LetterIndex.FirstIndex(Items.Select(c => c.Entry.JumpLetter).ToList(), letter);
        if (index >= 0)
        {
            Selected = Items[index];
        }
    }

    public void CloseRail()
    {
        if (IsRailOpen)
        {
            IsRailOpen = false;
            HintsChanged();
        }
    }

    public void AppendSearch(string text)
    {
        if (SearchText.Length == 0 && string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        SearchText += text;
    }

    public void BackspaceSearch()
    {
        if (SearchText.Length > 0)
        {
            SearchText = SearchText[..^1];
        }
    }

    private void MoveSelection(InputAction action)
    {
        int index = Selected is null ? -1 : Items.IndexOf(Selected);
        if (index < 0)
        {
            Selected = Items.FirstOrDefault();
            return;
        }

        int next = GridNavigator.Move(index, Items.Count, Columns, action, PageRows);
        if (next >= 0)
        {
            Selected = Items[next];
        }
    }

    [RelayCommand]
    private void SelectTab(LibraryTab value) => Tab = value;

    [RelayCommand]
    private void CycleSort() => Sort = LibraryQuery.Next(Sort);

    [RelayCommand]
    private async Task Rescan() => await _library.RescanAsync();

    [RelayCommand]
    private void ClearSearch() => SearchText = "";

    [RelayCommand]
    private void OpenFolder()
    {
        string folder = PrimaryFolder;
        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warn($"Open folder failed: {ex.Message}");
            _main.ShowError("Couldn't open the folder.");
        }
    }

    [RelayCommand]
    public async Task Activate(TrainerCardViewModel? card)
    {
        if (card is null || _launcher.IsLaunching)
        {
            return;
        }

        Selected = card;
        if (card.IsRunning)
        {
            _main.OpenCheats(card);
            return;
        }

        _main.ShowMessage($"Launching {card.Title}…");

        LaunchResult result = await _launcher.LaunchAsync(card.Entry);
        switch (result.Outcome)
        {
            case LaunchOutcome.AlreadyRunning:
                _main.ShowMessage($"{card.Title} is already running");
                break;
            case LaunchOutcome.Cancelled:
                _main.ClearMessage();
                break;
            case LaunchOutcome.Failed:
                _main.ShowError(result.Message ?? $"Couldn't start {card.Title}.");
                break;
            case LaunchOutcome.Started:
                _main.ShowMessage($"{card.Title} started");
                AfterLaunchBehavior after = _main.Settings.Current.AfterLaunch;
                if (after == AfterLaunchBehavior.StayOpen)
                {
                    await _launcher.WhenWindowReadyAsync(result.ProcessId);
                    await Task.Delay(250);
                }
                else
                {
                    await _launcher.FocusWhenReadyAsync(result.ProcessId);
                }

                _main.RequestAfterLaunch(after);
                break;
        }
    }

    [RelayCommand]
    public async Task CloseTrainer(TrainerCardViewModel? card)
    {
        if (card is null)
        {
            return;
        }

        switch (await _launcher.CloseAsync(card.Id))
        {
            case CloseResult.Closed:
                _main.ShowMessage($"Closed {card.Title}");
                break;
            case CloseResult.NotRunning:
                _main.ShowMessage($"{card.Title} isn't running");
                break;
            case CloseResult.NeedsAdministrator:
                _main.ShowError($"{card.Title} runs as administrator. Close it from its own window, or turn on “Run Trainer Deck as administrator”.");
                break;
            default:
                _main.ShowError($"Couldn't close {card.Title}.");
                break;
        }
    }

    [RelayCommand]
    public void ShowInFolder(TrainerCardViewModel? card)
    {
        if (card is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{card.Entry.File.FullPath}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warn($"Show in folder failed: {ex.Message}");
            _main.ShowError("Couldn't open the folder.");
        }
    }

    private void UpdateRunning()
    {
        foreach (TrainerCardViewModel card in _cards.Values)
        {
            card.IsRunning = _launcher.Running.IsRunning(card.Id);
        }

        HintsChanged();
    }

    [RelayCommand]
    public void ToggleFavourite(TrainerCardViewModel? card)
    {
        if (card is null)
        {
            return;
        }

        bool fav = !card.Entry.Meta.Favourite;
        _library.UpdateMetadata(card.Id, m => m.Favourite = fav);
        card.Refresh(card.Entry);
        _main.ShowMessage(fav ? $"Added {card.Title} to favourites" : $"Removed {card.Title} from favourites");
        HintsChanged();
    }

    private void OnLibraryChanged()
    {
        IsScanning = _library.IsScanning;
        if (!IsScanning)
        {
            Refresh();
            _launcher.ScanRunningInBackground();
        }
    }

    private void Refresh()
    {
        IReadOnlyList<TrainerEntry> all = _library.Entries;
        List<TrainerEntry> visible = LibraryQuery.Apply(all, Tab, Sort, SearchText);

        var live = new HashSet<string>(all.Select(e => e.Id), StringComparer.Ordinal);
        foreach (string stale in _cards.Keys.Where(k => !live.Contains(k)).ToList())
        {
            _cards.Remove(stale);
        }

        string? keepId = Selected?.Id;
        var list = new List<TrainerCardViewModel>(visible.Count);
        foreach (TrainerEntry e in visible)
        {
            if (!_cards.TryGetValue(e.Id, out var card))
            {
                card = new TrainerCardViewModel(e);
                _cards[e.Id] = card;
            }
            else
            {
                card.Refresh(e);
            }

            card.IsRunning = _launcher.Running.IsRunning(e.Id);
            card.EnsureArt(_art, _dispatcher);
            list.Add(card);
        }

        Items = new ObservableCollection<TrainerCardViewModel>(list);
        Selected = list.FirstOrDefault(c => c.Id == keepId) ?? list.FirstOrDefault();
        UpdateEmptyState(all.Count, visible.Count);
        Summary = all.Count == 1 ? "1 trainer" : $"{all.Count} trainers";
        int zips = _library.PendingZips.Count;
        PendingZipsLabel = zips == 0 ? null : zips == 1 ? "1 zip file to extract" : $"{zips} zip files to extract";
        OnPropertyChanged(nameof(PendingZipCount));
    }

    private void UpdateEmptyState(int total, int visible)
    {
        IsEmpty = visible == 0 && !IsScanning;
        ShowOpenFolder = false;
        if (!IsEmpty)
        {
            return;
        }

        if (total == 0)
        {
            EmptyTitle = "No trainers yet";
            EmptyDetail = $"Put your FLiNG trainers (or their .zip files) in\n{PrimaryFolder}\nThey'll show up here automatically.";
            ShowOpenFolder = true;
        }
        else if (HasSearch)
        {
            EmptyTitle = $"No matches for “{SearchText}”";
            EmptyDetail = "Press Esc to clear the search.";
        }
        else if (Tab == LibraryTab.Favourites)
        {
            EmptyTitle = "No favourites yet";
            EmptyDetail = "Favourite a trainer to pin it here.";
        }
        else if (Tab == LibraryTab.Recent)
        {
            EmptyTitle = "Nothing launched yet";
            EmptyDetail = "Trainers you launch will appear here.";
        }
        else
        {
            EmptyTitle = "Everything is hidden";
            EmptyDetail = "Unhide trainers from Settings.";
        }
    }
}
