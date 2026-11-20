using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using TrainerDeck.Art;
using TrainerDeck.Core.Library;

namespace TrainerDeck.UI.ViewModels;

public sealed partial class TrainerCardViewModel : ObservableObject
{
    private bool _artRequested;

    public TrainerCardViewModel(TrainerEntry entry)
    {
        Entry = entry;
        Refresh(entry);
        artBackground = ColorTools.CardGradient(ColorTools.FromName(entry.DisplayName));
    }

    public TrainerEntry Entry { get; private set; }

    public string Id => Entry.Id;

    [ObservableProperty]
    private string title = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVersion))]
    private string? version;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOptions))]
    private string? optionsBadge;

    [ObservableProperty]
    private bool isFavourite;

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private ImageSource? icon;

    [ObservableProperty]
    private Brush artBackground;

    [ObservableProperty]
    private string initial = "";

    public bool HasVersion => !string.IsNullOrEmpty(Version);

    public bool HasOptions => !string.IsNullOrEmpty(OptionsBadge);

    public void Refresh(TrainerEntry entry)
    {
        Entry = entry;
        Title = entry.DisplayName;
        Version = entry.File.Parsed.Version;
        OptionsBadge = entry.File.Parsed.OptionCount is int n ? $"+{n}" : null;
        IsFavourite = entry.Meta.Favourite;
        char letter = entry.JumpLetter;
        Initial = letter == '#' ? entry.DisplayName.Trim().FirstOrDefault(char.IsLetterOrDigit).ToString().ToUpperInvariant() : letter.ToString();
    }

    public async void EnsureArt(ArtService art, System.Windows.Threading.Dispatcher dispatcher)
    {
        if (_artRequested)
        {
            return;
        }

        _artRequested = true;
        CardArt? result = await art.GetIconAsync(Entry).ConfigureAwait(false);
        if (result is null)
        {
            return;
        }

        Brush background = ColorTools.CardGradient(result.Accent);
        _ = dispatcher.BeginInvoke(() =>
        {
            Icon = result.Image;
            ArtBackground = background;
        });
    }

    public void InvalidateArt() => _artRequested = false;
}
