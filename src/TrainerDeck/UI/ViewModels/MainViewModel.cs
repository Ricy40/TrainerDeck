using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using TrainerDeck.Art;
using TrainerDeck.Core.Input;
using TrainerDeck.Core.Library;
using TrainerDeck.Core.Settings;
using TrainerDeck.Launch;
using TrainerDeck.UI.Input;

namespace TrainerDeck.UI.ViewModels;

public sealed record HintViewModel(string Key, string Label, bool Round, System.Windows.Media.Brush KeyBrush);

public sealed partial class MainViewModel : ObservableObject
{
    private static readonly TimeSpan MessageDuration = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan ErrorDuration = TimeSpan.FromSeconds(7);

    private readonly Stack<PageViewModel> _pages = new();
    private readonly DispatcherTimer _messageTimer;

    public MainViewModel(TrainerLibrary library, ArtService art, SettingsStore settings, LaunchService launcher, Dispatcher dispatcher)
    {
        Settings = settings;
        Library = new LibraryViewModel(library, art, launcher, dispatcher, this);
        _messageTimer = new DispatcherTimer(MessageDuration, DispatcherPriority.Normal, (_, _) => ClearMessage(), dispatcher) { IsEnabled = false };
        Push(Library);
    }

    public SettingsStore Settings { get; }

    public LibraryViewModel Library { get; }

    [ObservableProperty]
    private PageViewModel? currentPage;

    [ObservableProperty]
    private InputSource lastDevice = InputSource.Keyboard;

    [ObservableProperty]
    private string? message;

    [ObservableProperty]
    private bool messageIsError;

    public ObservableCollection<HintViewModel> Hints { get; } = new();

    public event EventHandler? ToggleFullScreenRequested;

    public event Action<AfterLaunchBehavior>? AfterLaunchRequested;

    public void RequestAfterLaunch(AfterLaunchBehavior behavior) => AfterLaunchRequested?.Invoke(behavior);

    [ObservableProperty]
    private ControllerKind lastControllerKind = ControllerKind.Xbox;

    partial void OnLastDeviceChanged(InputSource value) => RebuildHints();

    partial void OnLastControllerKindChanged(ControllerKind value) => RebuildHints();

    public bool Handle(InputAction action, InputSource device, ControllerKind kind = ControllerKind.Xbox)
    {
        if (device == InputSource.Controller)
        {
            LastControllerKind = kind;
        }

        LastDevice = device;
        if (action == InputAction.ToggleFullScreen)
        {
            ToggleFullScreenRequested?.Invoke(this, EventArgs.Empty);
            return true;
        }

        if (CurrentPage?.Handle(action) == true)
        {
            return true;
        }

        if (action == InputAction.Back && _pages.Count > 1)
        {
            Pop();
            return true;
        }

        return false;
    }

    public void Push(PageViewModel page)
    {
        if (CurrentPage is not null)
        {
            CurrentPage.PropertyChanged -= OnPagePropertyChanged;
        }

        _pages.Push(page);
        SetCurrent(page);
    }

    public void Pop()
    {
        if (_pages.Count <= 1)
        {
            return;
        }

        _pages.Pop().PropertyChanged -= OnPagePropertyChanged;
        SetCurrent(_pages.Peek());
    }

    private void SetCurrent(PageViewModel page)
    {
        page.PropertyChanged += OnPagePropertyChanged;
        CurrentPage = page;
        RebuildHints();
    }

    private void OnPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PageViewModel.Hints))
        {
            RebuildHints();
        }
    }

    private void RebuildHints()
    {
        Hints.Clear();
        if (CurrentPage is null)
        {
            return;
        }

        foreach (FooterHint hint in CurrentPage.Hints)
        {
            if (LastDevice == InputSource.Controller)
            {
                ButtonGlyph g = ControllerGlyphs.For(hint.Action, LastControllerKind, ControllerMapping.Default);
                Hints.Add(new HintViewModel(g.Text, hint.Label, g.Round, Frozen(g.Color)));
            }
            else
            {
                Hints.Add(new HintViewModel(KeyboardMap.Label(hint.Action), hint.Label, false, Frozen(KeyCapText)));
            }
        }
    }

    private static readonly System.Windows.Media.Color KeyCapText = System.Windows.Media.Color.FromRgb(0xF3, 0xF5, 0xF9);

    private static System.Windows.Media.Brush Frozen(System.Windows.Media.Color c)
    {
        var b = new System.Windows.Media.SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    public void OnControllerConnectionChanged(string name, bool connected) =>
        ShowMessage(connected ? $"{name} connected" : $"{name} disconnected");

    public void ShowMessage(string text) => ShowMessage(text, false);

    public void ShowError(string text) => ShowMessage(text, true);

    private void ShowMessage(string text, bool error)
    {
        Message = text;
        MessageIsError = error;
        _messageTimer.Stop();
        _messageTimer.Interval = error ? ErrorDuration : MessageDuration;
        _messageTimer.Start();
    }

    public void ClearMessage()
    {
        _messageTimer.Stop();
        Message = null;
    }
}
