using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace TrainerDeck.Art;

internal static class IconExtractor
{
    public static BitmapSource? Extract(string path, int size = 256)
    {
        var handles = new IntPtr[1];
        var ids = new uint[1];
        uint count = PrivateExtractIcons(path, 0, size, size, handles, ids, 1, 0);
        if (count == 0 || count == uint.MaxValue || handles[0] == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            BitmapSource bmp = Imaging.CreateBitmapSourceFromHIcon(handles[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bmp.Freeze();
            return bmp;
        }
        finally
        {
            DestroyIcon(handles[0]);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint PrivateExtractIcons(string szFileName, int nIconIndex, int cxIcon, int cyIcon,
        IntPtr[] phicon, uint[] piconid, uint nIcons, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
