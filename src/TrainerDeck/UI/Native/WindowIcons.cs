using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Drawing = System.Drawing;

namespace TrainerDeck.UI.Native;

internal static class WindowIcons
{
    private const int WmSetIcon = 0x0080;
    private const int IconSmall = 0;
    private const int IconBig = 1;
    private const int SmCxIcon = 11;
    private const int SmCxSmIcon = 49;

    private static readonly Uri IconUri = new("pack://application:,,,/Assets/TrainerDeck.ico");
    private static readonly Dictionary<IntPtr, (Drawing.Icon Big, Drawing.Icon Small)> Held = new();

    public static void Attach(Window window)
    {
        window.SourceInitialized += (_, _) => Apply(window);
        window.DpiChanged += (_, _) => Apply(window);
        window.Closed += (_, _) => Release(new WindowInteropHelper(window).Handle);
    }

    private static void Apply(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        uint dpi = GetDpiForWindow(hwnd);
        int big = GetSystemMetricsForDpi(SmCxIcon, dpi);
        int small = (int)Math.Round(GetSystemMetricsForDpi(SmCxSmIcon, dpi) * 1.5);
        try
        {
            Drawing.Icon bigIcon = Load(big);
            Drawing.Icon smallIcon = Load(small);
            SendMessage(hwnd, WmSetIcon, IconBig, bigIcon.Handle);
            SendMessage(hwnd, WmSetIcon, IconSmall, smallIcon.Handle);
            Release(hwnd);
            Held[hwnd] = (bigIcon, smallIcon);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
            Core.Diagnostics.Log.Warn($"Couldn't set the window icon: {ex.Message}");
        }
    }

    private static Drawing.Icon Load(int size)
    {
        using Stream stream = Application.GetResourceStream(IconUri)!.Stream;
        return new Drawing.Icon(stream, size, size);
    }

    private static void Release(IntPtr hwnd)
    {
        if (Held.Remove(hwnd, out var icons))
        {
            icons.Big.Dispose();
            icons.Small.Dispose();
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int message, int wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetricsForDpi(int index, uint dpi);
}
