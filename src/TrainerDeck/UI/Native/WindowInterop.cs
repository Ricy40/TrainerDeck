using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace TrainerDeck.UI.Native;

internal static class WindowInterop
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaCaptionColor = 35;

    public static void ApplyTitleBar(Window window, bool dark, Color? caption)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        int on = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref on, sizeof(int));
        int colorRef = caption is Color c ? c.R | (c.G << 8) | (c.B << 16) : DwmwaColorDefault;
        _ = DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref colorRef, sizeof(int));
    }

    private const int DwmwaColorDefault = unchecked((int)0xFFFFFFFF);

    public static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is T deeper)
            {
                return deeper;
            }
        }

        return null;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
