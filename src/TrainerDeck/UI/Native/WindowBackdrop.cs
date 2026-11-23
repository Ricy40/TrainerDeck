using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using TrainerDeck.Core.Theming;

namespace TrainerDeck.UI.Native;


internal static class WindowBackdrop
{
    private const int DwmwaSystemBackdropType = 38;
    private const int BackdropNone = 1;
    private const int BackdropMica = 2;

    public static bool MicaSupported => Environment.OSVersion.Version.Build >= 22621;

    public static bool WantsMica(AppearanceMode mode, bool fullScreen) =>
        MicaSupported && !fullScreen && mode != AppearanceMode.TrainerDeck;

    public static void Apply(Window window, ResolvedTheme theme, bool mica)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        HwndSource? source = HwndSource.FromHwnd(hwnd);
        if (mica)
        {
            window.Background = Brushes.Transparent;
            if (source?.CompositionTarget is { } target)
            {
                target.BackgroundColor = Colors.Transparent;
            }

            var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            _ = DwmExtendFrameIntoClientArea(hwnd, ref margins);
            int type = BackdropMica;
            _ = DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref type, sizeof(int));
            WindowInterop.ApplyTitleBar(window, theme.IsDark, null);
        }
        else
        {
            window.SetResourceReference(Control.BackgroundProperty, "BackgroundBrush");
            if (source?.CompositionTarget is { } target)
            {
                target.BackgroundColor = Color.FromRgb(theme.Background.R, theme.Background.G, theme.Background.B);
            }

            var margins = new Margins();
            _ = DwmExtendFrameIntoClientArea(hwnd, ref margins);
            if (MicaSupported)
            {
                int type = BackdropNone;
                _ = DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref type, sizeof(int));
            }

            WindowInterop.ApplyTitleBar(window, theme.IsDark, Color.FromRgb(theme.Background.R, theme.Background.G, theme.Background.B));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
