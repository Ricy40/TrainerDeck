using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using TrainerDeck.Core.Input;
using TrainerDeck.Input.Hid;

namespace TrainerDeck.Input;


public sealed class ControllerInput : IDisposable
{
    private const int WmDeviceChange = 0x0219;
    private const uint GaRootOwner = 3;

    private readonly Window _window;
    private readonly HidGamepadSource _hid = new();
    private readonly XInputSource _xinput = new();
    private readonly GamepadPoller _poller;
    private HwndSource? _hwndSource;
    private IntPtr _hwnd;

    public ControllerInput(Window window, Action<InputAction, ControllerKind> onAction, Action<string, bool> onConnectionChanged)
    {
        _window = window;
        _xinput.ConnectionChanged += onConnectionChanged;
        _hid.ConnectionChanged += onConnectionChanged;
        _poller = new GamepadPoller([_xinput, _hid], ControllerMapping.Default, IsWindowActive, onAction);
    }

    public void Start()
    {
        _hwnd = new WindowInteropHelper(_window).EnsureHandle();
        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource?.AddHook(WndProc);
        _window.Activated += OnActivated;
        _window.StateChanged += OnActivated;
        _poller.Start();
    }

    private void OnActivated(object? sender, EventArgs e) => _poller.Wake();

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmDeviceChange)
        {
            _hid.RequestRescan();
            _xinput.Resume();
        }

        return IntPtr.Zero;
    }

    private bool IsWindowActive()
    {
        IntPtr hwnd = _hwnd;
        if (hwnd == IntPtr.Zero || IsIconic(hwnd))
        {
            return false;
        }

        IntPtr fg = GetForegroundWindow();
        return fg != IntPtr.Zero && (fg == hwnd || GetAncestor(fg, GaRootOwner) == hwnd);
    }

    public void Dispose()
    {
        _window.Activated -= OnActivated;
        _window.StateChanged -= OnActivated;
        _hwndSource?.RemoveHook(WndProc);
        _poller.Dispose();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
}
