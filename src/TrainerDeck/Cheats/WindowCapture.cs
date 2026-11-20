using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace TrainerDeck.Cheats;


public static class WindowCapture
{
    private const uint PwRenderFullContent = 2;

    public static BitmapSource? CaptureRegion(IntPtr hwnd, Rect screenRegion)
    {
        if (hwnd == IntPtr.Zero || IsIconic(hwnd) || !GetWindowRect(hwnd, out RECT wr))
        {
            return null;
        }

        int w = wr.Right - wr.Left, h = wr.Bottom - wr.Top;
        if (w <= 0 || h <= 0)
        {
            return null;
        }

        IntPtr screen = GetDC(IntPtr.Zero);
        IntPtr mem = CreateCompatibleDC(screen);
        IntPtr bmp = CreateCompatibleBitmap(screen, w, h);
        IntPtr old = SelectObject(mem, bmp);
        bool selected = true;
        try
        {
            bool printed = PrintWindow(hwnd, mem, PwRenderFullContent);
            SelectObject(mem, old);
            selected = false;
            if (!printed)
            {
                return null;
            }

            BitmapSource full = Imaging.CreateBitmapSourceFromHBitmap(bmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var crop = new Int32Rect(
                (int)Math.Round(screenRegion.X - wr.Left),
                (int)Math.Round(screenRegion.Y - wr.Top),
                (int)Math.Round(screenRegion.Width),
                (int)Math.Round(screenRegion.Height));
            crop.X = Math.Max(0, crop.X);
            crop.Y = Math.Max(0, crop.Y);
            crop.Width = Math.Min(crop.Width, w - crop.X);
            crop.Height = Math.Min(crop.Height, h - crop.Y);
            if (crop.Width < 32 || crop.Height < 32)
            {
                return null;
            }

            var result = new CroppedBitmap(full, crop);
            result.Freeze();
            return LooksBlank(result) ? null : result;
        }
        finally
        {
            if (selected)
            {
                SelectObject(mem, old);
            }

            DeleteObject(bmp);
            DeleteDC(mem);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    private static bool LooksBlank(BitmapSource image)
    {
        var bgra = new FormatConvertedBitmap(image, System.Windows.Media.PixelFormats.Bgra32, null, 0);
        int stride = bgra.PixelWidth * 4;
        byte[] px = new byte[stride * bgra.PixelHeight];
        bgra.CopyPixels(px, stride, 0);
        double sum = 0, sumSq = 0;
        int n = 0;
        for (int i = 0; i < px.Length; i += 4 * 7)
        {
            double lum = (0.299 * px[i + 2]) + (0.587 * px[i + 1]) + (0.114 * px[i]);
            sum += lum;
            sumSq += lum * lum;
            n++;
        }

        double mean = sum / n;
        return (sumSq / n) - (mean * mean) < 40;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr hdc);
}
