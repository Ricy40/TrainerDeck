using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using TrainerDeck.Art;
using TrainerDeck.Core.Library;

namespace TrainerDeck.UI.ViewModels;

public sealed partial class TrainerCardViewModel : ObservableObject
{
    private readonly Brush _nameBackground;
    private bool _artRequested;
    private int _coverVersion;
    private Brush? _iconBackground;

    public TrainerCardViewModel(TrainerEntry entry) : this(new GameGroup(GameGroup.KeyFor(entry), entry, [entry]))
    {
    }

    public TrainerCardViewModel(GameGroup group)
    {
        TrainerEntry entry = group.Default;
        Group = group;
        Entry = entry;
        Refresh(entry);
        _nameBackground = ColorTools.CardGradient(ColorTools.FromName(entry.DisplayName));
        artBackground = _nameBackground;
    }

    public string? IconFingerprint { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayIcon))]
    private bool isGenericIcon;

    public ImageSource? DisplayIcon => IsGenericIcon ? null : Icon;

    partial void OnIsGenericIconChanged(bool value) => ArtBackground = value || _iconBackground is null ? _nameBackground : _iconBackground;

    public TrainerEntry Entry { get; private set; }

    public GameGroup Group { get; private set; }

    public IReadOnlyList<TrainerEntry> Versions => Group.Versions;

    public bool HasVersions => Group.HasVersions;

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
    [NotifyPropertyChangedFor(nameof(DisplayIcon))]
    private ImageSource? icon;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCover))]
    private ImageSource? cover;

    public bool HasCover => Cover is not null;

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
        Version = Group is { HasVersions: true } g
            ? string.Join("  ·  ", new[] { entry.File.Parsed.Version, $"{g.Versions.Count} versions" }.Where(s => !string.IsNullOrEmpty(s)))
            : entry.File.Parsed.Version;
        OptionsBadge = entry.File.Parsed.OptionCount is int n ? $"+{n}" : null;
        IsFavourite = Group?.Versions.Any(v => v.Meta.Favourite) ?? entry.Meta.Favourite;
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
        ReloadCover(art, dispatcher);
        CardArt? result = await art.GetIconAsync(Entry).ConfigureAwait(false);
        if (result is null)
        {
            return;
        }

        Brush background = ColorTools.CardGradient(result.Accent);
        art.NoteIcon(result.Fingerprint, Id);
        _ = dispatcher.BeginInvoke(() =>
        {
            IconFingerprint = result.Fingerprint;
            _iconBackground = background;
            IsGenericIcon = art.IsGenericIcon(result.Fingerprint);
            Icon = result.Image;
            if (!IsGenericIcon)
            {
                ArtBackground = background;
            }
        });
    }

    public void InvalidateArt() => _artRequested = false;

    public void SetGroup(GameGroup group, Func<string, bool> isRunning, ArtService art, System.Windows.Threading.Dispatcher dispatcher)
    {
        Group = group;
        TrainerEntry? running = group.Versions.FirstOrDefault(v => isRunning(v.Id));
        TrainerEntry chosen = running ?? group.Default;
        bool switched = chosen.Id != Entry.Id;
        Refresh(chosen);
        IsRunning = running is not null;
        OnPropertyChanged(nameof(Versions));
        OnPropertyChanged(nameof(HasVersions));
        if (switched)
        {
            InvalidateArt();
            Icon = null;
            Cover = null;
        }

        EnsureArt(art, dispatcher);
    }

    public async void ReloadCover(ArtService art, System.Windows.Threading.Dispatcher dispatcher)
    {
        int version = ++_coverVersion;
        ImageSource? image = await art.GetCoverAsync(Entry).ConfigureAwait(false);
        _ = dispatcher.BeginInvoke(() =>
        {
            if (version == _coverVersion)
            {
                Cover = image;
            }
        });
    }
}
