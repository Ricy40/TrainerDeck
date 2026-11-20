using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using TrainerDeck.Art;
using TrainerDeck.Core.Input;
using TrainerDeck.Core.Library;
using TrainerDeck.Core.Settings;
using TrainerDeck.UI.Input;

namespace TrainerDeck.UI.ViewModels;

public sealed record HintViewModel(string Key, string Label);

public sealed partial class MainViewModel : ObservableObject
{
    private static readonly TimeSpan MessageDuration = TimeSpan.FromSeconds(4);

    private readonly Stack<PageViewModel> _pages = new();
    private readonly DispatcherTimer _messageTimer;

    public MainViewModel(TrainerLibrary library, ArtService art, SettingsStore settings, Dispatcher dispatcher)
    {
        Settings = settings;
        Library = new LibraryViewModel(library, art, dispatcher, ShowMessage);
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

    public ObservableCollection<HintViewModel> Hints { get; } = new();

    public event EventHandler? ToggleFullScreenRequested;

    partial void OnLastDeviceChanged(InputSource value) => RebuildHints();

    public bool Handle(InputAction action, InputSource device)
    {
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
            Hints.Add(new HintViewModel(KeyboardMap.Label(hint.Action), hint.Label));
        }
    }

    public void ShowMessage(string text)
    {
        Message = text;
        _messageTimer.Stop();
        _messageTimer.Start();
    }

    private void ClearMessage()
    {
        _messageTimer.Stop();
        Message = null;
    }
}
