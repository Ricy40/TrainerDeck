using System.Windows;

namespace TrainerDeck.UI;

internal sealed class WindowModeController
{
    private readonly Window _window;
    private Rect? _restoreBounds;
    private bool _wasMaximized;

    public WindowModeController(Window window) => _window = window;

    public bool IsFullScreen { get; private set; }

    public void SetFullScreen(bool fullScreen)
    {
        if (fullScreen)
        {
            if (_window.IsLoaded && !IsFullScreen)
            {
                _wasMaximized = _window.WindowState == WindowState.Maximized;
                Rect normal = _window.WindowState == WindowState.Normal
                    ? new Rect(_window.Left, _window.Top, _window.Width, _window.Height)
                    : _window.RestoreBounds;
                _restoreBounds = normal.IsEmpty ? null : normal;
            }

            _window.WindowState = WindowState.Normal;
            _window.WindowStyle = WindowStyle.None;
            _window.ResizeMode = ResizeMode.NoResize;
            _window.WindowState = WindowState.Maximized;
        }
        else
        {
            _window.WindowState = WindowState.Normal;
            _window.WindowStyle = WindowStyle.SingleBorderWindow;
            _window.ResizeMode = ResizeMode.CanResize;
            if (_restoreBounds is Rect r)
            {
                _window.Left = r.Left;
                _window.Top = r.Top;
                _window.Width = r.Width;
                _window.Height = r.Height;
            }

            if (_wasMaximized)
            {
                _window.WindowState = WindowState.Maximized;
            }
        }

        IsFullScreen = fullScreen;
    }
}
