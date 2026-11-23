using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace TrainerDeck.Install;

public static class ShellLink
{
    public static void Create(string linkPath, string target, string? arguments = null, string? description = null)
    {
        var link = (IShellLinkW)new CShellLink();
        try
        {
            link.SetPath(target);
            link.SetWorkingDirectory(Path.GetDirectoryName(target) ?? "");
            link.SetIconLocation(target, 0);
            if (arguments is not null)
            {
                link.SetArguments(arguments);
            }

            if (description is not null)
            {
                link.SetDescription(description);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);
            ((IPersistFile)link).Save(linkPath, true);
        }
        finally
        {
            Marshal.ReleaseComObject(link);
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class CShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int max, IntPtr findData, uint flags);

        void GetIDList(out IntPtr idl);

        void SetIDList(IntPtr idl);

        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int max);

        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);

        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int max);

        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);

        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int max);

        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);

        void GetHotkey(out short hotkey);

        void SetHotkey(short hotkey);

        void GetShowCmd(out int showCmd);

        void SetShowCmd(int showCmd);

        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int max, out int index);

        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);

        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);

        void Resolve(IntPtr hwnd, uint flags);

        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }
}
