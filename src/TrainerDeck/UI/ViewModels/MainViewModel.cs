using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
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

    public MainViewModel(AppServices services, Dispatcher dispatcher)
    {
        Services = services;
        Dispatcher = dispatcher;
        Settings = services.Settings;
        Library = new LibraryViewModel(services.Library, services.Art, services.Launcher, dispatcher, this);
        _messageTimer = new DispatcherTimer(MessageDuration, DispatcherPriority.Normal, (_, _) => ClearMessage(), dispatcher) { IsEnabled = false };
        Push(Library);
    }

    public MainViewModel(MainViewModel main, Func<MainViewModel, PageViewModel> root)
    {
        Services = main.Services;
        Dispatcher = main.Dispatcher;
        Settings = main.Settings;
        Library = main.Library;
        IsOverlay = true;
        LastDevice = main.LastDevice;
        LastControllerKind = main.LastControllerKind;
        _messageTimer = new DispatcherTimer(MessageDuration, DispatcherPriority.Normal, (_, _) => ClearMessage(), Dispatcher) { IsEnabled = false };
        Push(root(this));
    }

    public bool IsOverlay { get; }

    public int Depth => _pages.Count;

    public void CloseAll()
    {
        while (_pages.Count > 0)
        {
            PageViewModel page = _pages.Pop();
            page.PropertyChanged -= OnPagePropertyChanged;
            page.OnClosed();
        }
    }

    public AppServices Services { get; }

    public Dispatcher Dispatcher { get; }

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

    [ObservableProperty]
    private bool isMessageVisible;

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

        if (action == InputAction.Settings && !IsOverlay)
        {
            OpenSettings();
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

        PageViewModel closed = _pages.Pop();
        closed.PropertyChanged -= OnPagePropertyChanged;
        closed.OnClosed();
        PageViewModel top = _pages.Peek();
        SetCurrent(top);
        top.OnResumed();
    }

    public void PopTo(PageViewModel page)
    {
        while (_pages.Count > 1 && CurrentPage != page)
        {
            Pop();
        }
    }

    public void OpenDetails(TrainerCardViewModel card) => Push(new DetailsViewModel(this, card));

    public void OpenCheats(TrainerCardViewModel card) => Push(new CheatsViewModel(this, card));

    public event Action? RestartElevatedRequested;

    public Action? OpenOverlay { get; set; }

    public Action<bool>? SuspendOverlayHotkey { get; set; }

    public void RequestRestartElevated()
    {
        Settings.Update(s => s.RunAsAdministrator = true);
        RestartElevatedRequested?.Invoke();
    }

    public void OpenSettings()
    {
        if (CurrentPage is not SettingsViewModel)
        {
            Push(new SettingsViewModel(this));
        }
    }

    public void OpenZipImport()
    {
        if (Services.Library.PendingZips.Count > 0)
        {
            Push(new ZipImportViewModel(this));
        }
        else
        {
            ShowMessage("There are no zip files waiting to be extracted.");
        }
    }

    public void OpenAntivirusNotice() => Push(new NoticeViewModel(this));

    public void PickFolder(string title, string? start, Action<string> picked) =>
        Push(new FileBrowserViewModel(this, title, start, folderMode: true, picked));

    public void PickImage(string title, string? start, Action<string> picked) =>
        Push(new FileBrowserViewModel(this, title, start, folderMode: false, picked));

    public static string DownloadsFolder => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    public string TrainerFolder => Services.Library.Roots.FirstOrDefault() ?? Services.Paths.DefaultTrainersDirectory;

    public void AddTrainer()
    {
        string dest = TrainerFolder;
        Push(new FileBrowserViewModel(this, "Add a trainer", DownloadsFolder, folderMode: false,
            path => _ = ImportTrainerAsync(path, dest), TrainerImport.Extensions)
        {
            Body = $"Pick a trainer .exe or .zip. It's copied to {dest}; zips are extracted.",
        });
    }

    private async Task ImportTrainerAsync(string source, string destination)
    {
        string name = System.IO.Path.GetFileName(source);
        ShowMessage($"Adding {name}…");
        TrainerImportResult result = await Task.Run(() => TrainerImport.Import(source, destination));
        if (!result.Success && result.AccessDenied && !Services.IsElevated)
        {
            result = await ImportElevatedAsync(source, destination);
        }

        if (result.Success)
        {
            ShowMessage($"Added {name}");
            _ = Services.Library.RescanAsync();
        }
        else
        {
            ShowError($"Couldn't add {name}: {result.Error}");
        }
    }

    private static async Task<TrainerImportResult> ImportElevatedAsync(string source, string destination)
    {
        string? exe = Environment.ProcessPath;
        if (exe is null)
        {
            return new TrainerImportResult(false, null, "access denied");
        }

        ShellResult r = await ShellLauncher.RunAsync(exe, $"{App.ImportTrainerFlag} \"{source}\" \"{destination}\"", null, "runas", IntPtr.Zero);
        if (r.Error == Core.Launch.LaunchErrors.Cancelled)
        {
            return new TrainerImportResult(false, null, "administrator permission was declined");
        }

        if (r.Process is not { } process)
        {
            return new TrainerImportResult(false, null, $"error {r.Error}");
        }

        using (process)
        {
            int? code = await LaunchNative.WaitForExitAsync(process, TimeSpan.FromMinutes(2));
            return code == 0 ? new TrainerImportResult(true, destination, null) : new TrainerImportResult(false, null, "the copy failed");
        }
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
                Hints.Add(new HintViewModel(g.Text, hint.ControllerLabel ?? hint.Label, g.Round, Frozen(g.Color)));
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
        IsMessageVisible = true;
        MessageIsError = error;
        _messageTimer.Stop();
        _messageTimer.Interval = error ? ErrorDuration : MessageDuration;
        _messageTimer.Start();
    }

    public void ClearMessage()
    {
        _messageTimer.Stop();
        IsMessageVisible = false;
    }
}
