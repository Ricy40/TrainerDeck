using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using TrainerDeck.Core.Cheats;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Input;
using TrainerDeck.Core.Launch;
using TrainerDeck.Input;
using TrainerDeck.UI.ViewModels;
using Forms = System.Windows.Forms;

namespace TrainerDeck.UI;

public sealed class OverlayController : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0x7D01;

    private readonly MainViewModel _main;
    private readonly Window _mainWindow;
    private OverlayWindow? _window;
    private MainViewModel? _vm;
    private IntPtr _previousForeground;
    private string? _trainerId;
    private HwndSource? _hwndSource;
    private Hotkey? _hotkey;

    public OverlayController(MainViewModel main, Window mainWindow)
    {
        _main = main;
        _mainWindow = mainWindow;
        _main.Settings.Changed += (_, _) => _mainWindow.Dispatcher.BeginInvoke(RegisterHotkey);
    }

    public ControllerInput? Controllers { get; set; }

    public bool IsOpen => _window?.IsVisible == true;

    public bool IsActive => IsOpen && Controllers?.IsOverlayActive == true;

    public bool HotkeyRegistered { get; private set; }

    public void SuspendHotkey(bool suspend)
    {
        if (suspend && _hwndSource is not null)
        {
            UnregisterHotKey(_hwndSource.Handle, HotkeyId);
        }
        else if (!suspend)
        {
            RegisterHotkey();
        }
    }

    public void Attach()
    {
        IntPtr hwnd = new WindowInteropHelper(_mainWindow).EnsureHandle();
        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(WndProc);
        RegisterHotkey();
    }

    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void Open() => _mainWindow.Dispatcher.BeginInvoke(() =>
    {
        if (!_main.Settings.Current.OverlayEnabled)
        {
            return;
        }

        IntPtr fg = GetForegroundWindow();
        if (_window is null || fg != _window.Handle)
        {
            _previousForeground = fg;
        }

        List<RunningProcess> running = RunningNewestFirst();
        if (_trainerId is null || running.All(r => r.TrainerId != _trainerId))
        {
            _trainerId = running.FirstOrDefault()?.TrainerId;
        }

        _window ??= new OverlayWindow(this);
        Build();
        if (Controllers is not null)
        {
            Controllers.OverlayHandle = _window.Handle;
        }

        _window.PlaceOn(MonitorOf(_previousForeground));
        _window.ShowSwitchHint = running.Count > 1;
        bool wasOpen = _window.IsVisible;
        _window.Show();
        Cheats.ForegroundGuard.GrantForeground();
        _window.Activate();
        SetForegroundWindow(_window.Handle);
        if (!wasOpen)
        {
            _window.AnimateIn();
        }

        _window.FocusPage();
        Controllers?.Wake();
        Log.Info("Overlay opened.");
    });

    public void Close()
    {
        if (_window is null || !_window.IsVisible)
        {
            return;
        }

        _window.Hide();
        _vm?.CloseAll();
        _vm = null;
        if (_previousForeground != IntPtr.Zero)
        {
            SetForegroundWindow(_previousForeground);
        }

        Controllers?.Wake();
    }

    public async void OnDeactivated()
    {
        await Task.Delay(600);
        if (!IsOpen || (_window is not null && GetForegroundWindow() == _window.Handle))
        {
            return;
        }

        _previousForeground = IntPtr.Zero;
        Close();
    }

    public bool Handle(InputAction action, InputSource source, ControllerKind kind = ControllerKind.Xbox)
    {
        if (_vm is null)
        {
            return false;
        }

        if (action is InputAction.PrevTab or InputAction.NextTab && _vm.Depth == 1)
        {
            List<RunningProcess> running = RunningNewestFirst();
            if (running.Count > 1)
            {
                int i = running.FindIndex(r => r.TrainerId == _trainerId);
                int next = (i + (action == InputAction.NextTab ? 1 : running.Count - 1)) % running.Count;
                _trainerId = running[next].TrainerId;
                Build();
                _window?.FocusPage();
                return true;
            }
        }

        if (_vm.Handle(action, source, kind))
        {
            return true;
        }

        if (action is InputAction.Back or InputAction.Settings)
        {
            Close();
            return true;
        }

        return false;
    }

    public bool IsToggleHotkey(Key key, ModifierKeys mods)
    {
        if (_hotkey is null)
        {
            return false;
        }

        var m = HotkeyModifiers.None;
        if (mods.HasFlag(ModifierKeys.Control))
        {
            m |= HotkeyModifiers.Ctrl;
        }

        if (mods.HasFlag(ModifierKeys.Alt))
        {
            m |= HotkeyModifiers.Alt;
        }

        if (mods.HasFlag(ModifierKeys.Shift))
        {
            m |= HotkeyModifiers.Shift;
        }

        return KeyInterop.VirtualKeyFromKey(key) == _hotkey.Key && m == _hotkey.Modifiers;
    }

    private void Build()
    {
        _vm?.CloseAll();
        TrainerCardViewModel? card = _trainerId is null ? null : _main.Library.CardFor(_trainerId);
        _vm = new MainViewModel(_main, nav => card is null
            ? new OverlayEmptyViewModel(nav, OpenMainWindow, Close)
            : new CheatsViewModel(nav, card));
        if (_window is not null)
        {
            _window.DataContext = _vm;
        }
    }

    private void OpenMainWindow()
    {
        Close();
        if (_mainWindow is MainWindow mw)
        {
            mw.Show();
            mw.BringToFront();
        }
    }

    private List<RunningProcess> RunningNewestFirst() =>
        _main.Services.Launcher.Running.Snapshot().OrderByDescending(r => r.StartedUtc).ToList();

    private void RegisterHotkey()
    {
        IntPtr hwnd = _hwndSource?.Handle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        UnregisterHotKey(hwnd, HotkeyId);
        _hotkey = _main.Settings.Current.OverlayEnabled ? Hotkey.Parse(_main.Settings.Current.OverlayHotkey) : null;
        HotkeyRegistered = false;
        if (_hotkey is null)
        {
            return;
        }

        uint mods = 0x4000;
        if (_hotkey.Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            mods |= 0x1;
        }

        if (_hotkey.Modifiers.HasFlag(HotkeyModifiers.Ctrl))
        {
            mods |= 0x2;
        }

        if (_hotkey.Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            mods |= 0x4;
        }

        HotkeyRegistered = RegisterHotKey(hwnd, HotkeyId, mods, (uint)_hotkey.Key);
        Log.Info(HotkeyRegistered ? $"Overlay shortcut {_hotkey} registered." : $"Overlay shortcut {_hotkey} is already used by another app.");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            Toggle();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private Rect MonitorOf(IntPtr hwnd)
    {
        Forms.Screen screen = hwnd != IntPtr.Zero ? Forms.Screen.FromHandle(hwnd) : Forms.Screen.PrimaryScreen!;
        System.Drawing.Rectangle b = screen.Bounds;
        uint dpi = hwnd != IntPtr.Zero ? GetDpiForWindow(hwnd) : 96;
        double scale = (dpi == 0 ? 96 : dpi) / 96.0;
        return new Rect(b.Left / scale, b.Top / scale, b.Width / scale, b.Height / scale);
    }

    public void Dispose()
    {
        if (_hwndSource is not null)
        {
            UnregisterHotKey(_hwndSource.Handle, HotkeyId);
            _hwndSource.RemoveHook(WndProc);
        }

        _window?.Close();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
