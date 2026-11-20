using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainerDeck.Art;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Input;
using TrainerDeck.Core.Library;

namespace TrainerDeck.UI.ViewModels;

public sealed partial class LibraryViewModel : PageViewModel
{
    private readonly TrainerLibrary _library;
    private readonly ArtService _art;
    private readonly Dispatcher _dispatcher;
    private readonly Action<string> _showMessage;
    private readonly Dictionary<string, TrainerCardViewModel> _cards = new(StringComparer.Ordinal);

    public LibraryViewModel(TrainerLibrary library, ArtService art, Dispatcher dispatcher, Action<string> showMessage)
    {
        _library = library;
        _art = art;
        _dispatcher = dispatcher;
        _showMessage = showMessage;
        _library.Changed += (_, _) => _dispatcher.BeginInvoke(OnLibraryChanged);
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

    public override IReadOnlyList<FooterHint> Hints =>
    [
        new(InputAction.Activate, "Launch"),
        new(InputAction.Favourite, Selected?.IsFavourite == true ? "Unfavourite" : "Favourite"),
        new(InputAction.Details, "Details"),
        new(InputAction.NextTab, "Switch tab"),
        new(InputAction.Sort, "Sort"),
        new(InputAction.Back, HasSearch ? "Clear search" : "Back"),
    ];

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
        switch (action)
        {
            case InputAction.Up or InputAction.Down or InputAction.Left or InputAction.Right
                or InputAction.PageUp or InputAction.PageDown or InputAction.Home or InputAction.End:
                MoveSelection(action);
                return true;
            case InputAction.Activate:
                Activate(Selected);
                return true;
            case InputAction.Favourite:
                ToggleFavourite(Selected);
                return true;
            case InputAction.Details:
                if (Selected is not null)
                {
                    _showMessage($"Details page arrives in milestone 5 ({Selected.Title}).");
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
            case InputAction.Settings:
                _showMessage("Settings arrive in milestone 5.");
                return true;
            case InputAction.Rescan:
                RescanCommand.Execute(null);
                return true;
            case InputAction.Back when HasSearch:
                SearchText = "";
                return true;
            default:
                return false;
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
            _showMessage("Couldn't open the folder.");
        }
    }

    [RelayCommand]
    public void Activate(TrainerCardViewModel? card)
    {
        if (card is null)
        {
            return;
        }

        Selected = card;
        _showMessage($"Launching arrives in milestone 4 — selected {card.Title}.");
    }

    [RelayCommand]
    public void ToggleFavourite(TrainerCardViewModel? card)
    {
        if (card is null)
        {
            return;
        }

        bool fav = !card.Entry.Meta.Favourite;
        _library.Store.Update(card.Id, m => m.Favourite = fav);
        _library.SaveMetadata();
        card.Refresh(card.Entry);
        _showMessage(fav ? $"Added {card.Title} to favourites" : $"Removed {card.Title} from favourites");
        if (Tab == LibraryTab.Favourites)
        {
            Refresh();
        }

        HintsChanged();
    }

    private void OnLibraryChanged()
    {
        IsScanning = _library.IsScanning;
        if (!IsScanning)
        {
            Refresh();
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

            card.EnsureArt(_art, _dispatcher);
            list.Add(card);
        }

        Items = new ObservableCollection<TrainerCardViewModel>(list);
        Selected = list.FirstOrDefault(c => c.Id == keepId) ?? list.FirstOrDefault();
        UpdateEmptyState(all.Count, visible.Count);
        Summary = all.Count == 1 ? "1 trainer" : $"{all.Count} trainers";
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
