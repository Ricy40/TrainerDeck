using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TrainerDeck.Core.Input;
using TrainerDeck.UI.Input;
using TrainerDeck.UI.Native;
using TrainerDeck.UI.ViewModels;
using TrainerDeck.UI.Views;

namespace TrainerDeck.UI;

public partial class OverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;

    private readonly OverlayController _controller;

    public OverlayWindow(OverlayController controller)
    {
        InitializeComponent();
        _controller = controller;
        SourceInitialized += (_, _) =>
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd, GwlExStyle, GetWindowLong(hwnd, GwlExStyle) | WsExToolWindow);
        };
        PreviewKeyDown += OnPreviewKeyDown;
        Deactivated += (_, _) => _controller.OnDeactivated();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is MainViewModel old)
            {
                old.PropertyChanged -= OnVmPropertyChanged;
            }

            if (e.NewValue is MainViewModel vm)
            {
                vm.PropertyChanged += OnVmPropertyChanged;
            }
        };
    }

    public IntPtr Handle => new WindowInteropHelper(this).EnsureHandle();

    public bool ShowSwitchHint
    {
        set => SwitchHint.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    public void PlaceOn(Rect monitorDips)
    {
        Left = monitorDips.Left;
        Top = monitorDips.Top;
        Width = monitorDips.Width;
        Height = monitorDips.Height;
        Panel.Width = Math.Min(620, monitorDips.Width);
    }

    public void AnimateIn()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Slide.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, new DoubleAnimation(80, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
        Panel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
    }

    public void FocusPage() => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
    {
        if (WindowInterop.FindDescendant<UserControl>(PageHost) is IPageView view)
        {
            view.FocusDefault();
        }
    });

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentPage))
        {
            FocusPage();
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        ModifierKeys mods = Keyboard.Modifiers;
        if (Keyboard.FocusedElement is TextBox box)
        {
            if (key == Key.Escape)
            {
                e.Handled = _controller.Handle(InputAction.Back, InputSource.Keyboard);
            }
            else if (key == Key.Enter && !box.AcceptsReturn)
            {
                e.Handled = _controller.Handle(InputAction.Activate, InputSource.Keyboard);
            }

            return;
        }

        if (_controller.IsToggleHotkey(key, mods))
        {
            _controller.Close();
            e.Handled = true;
            return;
        }

        if (KeyboardMap.Map(key, mods) is InputAction action)
        {
            e.Handled = _controller.Handle(action, InputSource.Keyboard);
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}
