using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TrainerDeck.Core.Input;
using TrainerDeck.Core.Settings;
using TrainerDeck.UI.Input;
using TrainerDeck.UI.Native;
using TrainerDeck.UI.ViewModels;
using TrainerDeck.UI.Views;

namespace TrainerDeck.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly SettingsStore _settings;
    private readonly WindowModeController _mode;
    private readonly Action? _repaintBackdrop;

    public MainWindow(MainViewModel vm, SettingsStore settings)
    {
        InitializeComponent();
        _vm = vm;
        _settings = settings;
        _mode = new WindowModeController(this);
        DataContext = vm;

        vm.ToggleFullScreenRequested += (_, _) => ToggleFullScreen();
        vm.AfterLaunchRequested += OnAfterLaunch;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentPage))
            {
                AnimatePageIn();
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, FocusPage);
            }
        };
        _repaintBackdrop = Theme.ThemeManager.Instance?.Attach(this, () => _mode.IsFullScreen);
        PreviewKeyDown += OnPreviewKeyDown;
        Deactivated += (_, _) => _vm.Library.CloseRail();
        PreviewTextInput += OnPreviewTextInput;

        if (settings.Current.StartFullScreen)
        {
            _mode.SetFullScreen(true);
            vm.IsFullScreen = true;
        }
    }

    private LibraryView? LibraryView => WindowInterop.FindDescendant<LibraryView>(this);

    public event EventHandler? HideToTrayRequested;

    public event EventHandler? ExitRequested;

    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = _mode.IsFullScreen ? WindowState.Maximized : WindowState.Normal;
        }

        Show();
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void AnimatePageIn()
    {
        var ease = new System.Windows.Media.Animation.QuinticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
        PageHost.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(167)));
        if (PageHost.RenderTransform is TranslateTransform t)
        {
            t.BeginAnimation(TranslateTransform.YProperty, new System.Windows.Media.Animation.DoubleAnimation(28, 0, TimeSpan.FromMilliseconds(300)) { EasingFunction = ease });
        }
    }

    private void FocusPage()
    {
        if (WindowInterop.FindDescendant<UserControl>(PageHost) is IPageView view)
        {
            view.FocusDefault();
        }
    }

    private void OnAfterLaunch(AfterLaunchBehavior behavior)
    {
        switch (behavior)
        {
            case AfterLaunchBehavior.StayOpen:
                Cheats.ForegroundGuard.GrantForeground();
                BringToFront();
                break;
            case AfterLaunchBehavior.Minimize:
                WindowState = WindowState.Minimized;
                break;
            case AfterLaunchBehavior.Close:
                ExitRequested?.Invoke(this, EventArgs.Empty);
                break;
            case AfterLaunchBehavior.HideToTray:
                HideToTrayRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private void ToggleFullScreen()
    {
        bool full = !_mode.IsFullScreen;
        _mode.SetFullScreen(full);
        _vm.IsFullScreen = full;
        _repaintBackdrop?.Invoke();
        _settings.Update(s => s.StartFullScreen = full);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        ModifierKeys mods = Keyboard.Modifiers;

        if (_vm.CurrentPage is KeyCaptureViewModel capture)
        {
            _vm.LastDevice = InputSource.Keyboard;
            e.Handled = capture.OnKey(key, mods);
            return;
        }

        if (Keyboard.FocusedElement is TextBox box)
        {
            e.Handled = box.Name switch
            {
                "SearchBox" => HandleSearchBoxKey(box, key, mods),
                "PathBox" => HandlePathBoxKey(box, key, mods),
                _ => HandleTextBoxKey(box, key, mods),
            };
            return;
        }

        if (key == Key.L && mods == ModifierKeys.Control && WindowInterop.FindDescendant<FileBrowserView>(this) is { } browserView)
        {
            browserView.FocusPath();
            e.Handled = true;
            return;
        }

        if (key == Key.Back && _vm.CurrentPage is LibraryViewModel lib)
        {
            lib.BackspaceSearch();
            _vm.LastDevice = InputSource.Keyboard;
            e.Handled = true;
            return;
        }

        if (KeyboardMap.Map(key, mods) is InputAction action)
        {
            e.Handled = _vm.Handle(action, InputSource.Keyboard);
        }
    }


    private bool HandleTextBoxKey(TextBox box, Key key, ModifierKeys mods)
    {
        switch (key)
        {
            case Key.Escape:
                return _vm.Handle(InputAction.Back, InputSource.Keyboard);
            case Key.Enter when !box.AcceptsReturn && mods == ModifierKeys.None:
                return _vm.Handle(InputAction.Activate, InputSource.Keyboard);
            case Key.F11:
                return _vm.Handle(InputAction.ToggleFullScreen, InputSource.Keyboard);
            default:
                return false;
        }
    }

    private bool HandlePathBoxKey(TextBox box, Key key, ModifierKeys mods)
    {
        switch (key)
        {
            case Key.Enter when _vm.CurrentPage is FileBrowserViewModel browser:
                browser.GoToPath(box.Text);
                FocusPage();
                return true;
            case Key.Escape or Key.Down:
                FocusPage();
                return true;
            case Key.F11:
                return _vm.Handle(InputAction.ToggleFullScreen, InputSource.Keyboard);
            default:
                return false;
        }
    }

    private bool HandleSearchBoxKey(TextBox box, Key key, ModifierKeys mods)
    {
        switch (key)
        {
            case Key.Enter or Key.Down or Key.Tab:
                LibraryView?.FocusGrid();
                return true;
            case Key.Escape:
                if (box.Text.Length > 0)
                {
                    box.Clear();
                }
                else
                {
                    LibraryView?.FocusGrid();
                }

                return true;
            case Key.F5 or Key.F11:
                return KeyboardMap.Map(key, mods) is InputAction a && _vm.Handle(a, InputSource.Keyboard);
            default:
                return false;
        }
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox || _vm.CurrentPage is not LibraryViewModel lib)
        {
            return;
        }

        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != 0 || e.Text.Any(char.IsControl))
        {
            return;
        }

        lib.AppendSearch(e.Text);
        _vm.LastDevice = InputSource.Keyboard;
        e.Handled = true;
    }
}
