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

    public MainWindow(MainViewModel vm, SettingsStore settings)
    {
        InitializeComponent();
        _vm = vm;
        _settings = settings;
        _mode = new WindowModeController(this);
        DataContext = vm;

        vm.ToggleFullScreenRequested += (_, _) => ToggleFullScreen();
        vm.AfterLaunchRequested += OnAfterLaunch;
        SourceInitialized += (_, _) => WindowInterop.ApplyDarkTitleBar(this, (Color)FindResource("BackgroundColor"));
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewTextInput += OnPreviewTextInput;

        if (settings.Current.StartFullScreen)
        {
            _mode.SetFullScreen(true);
        }
    }

    private LibraryView? LibraryView => WindowInterop.FindDescendant<LibraryView>(this);

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

    private void OnAfterLaunch(AfterLaunchBehavior behavior)
    {
        switch (behavior)
        {
            case AfterLaunchBehavior.Minimize:
                WindowState = WindowState.Minimized;
                break;
            case AfterLaunchBehavior.Close:
                Close();
                break;
        }
    }

    private void ToggleFullScreen()
    {
        bool full = !_mode.IsFullScreen;
        _mode.SetFullScreen(full);
        _settings.Update(s => s.StartFullScreen = full);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        ModifierKeys mods = Keyboard.Modifiers;

        if (Keyboard.FocusedElement is TextBox box)
        {
            e.Handled = HandleSearchBoxKey(box, key, mods);
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
