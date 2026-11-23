using System.Runtime.InteropServices;
using TrainerDeck.Core.Diagnostics;

namespace TrainerDeck.Cheats;


public static class ForegroundGuard
{
    public static T Keep<T>(Func<T> action)
    {
        IntPtr before = GetForegroundWindow();
        T result = action();
        if (before == IntPtr.Zero)
        {
            return result;
        }

        for (int i = 0; i < 10; i++)
        {
            Thread.Sleep(50);
            IntPtr now = GetForegroundWindow();
            if (now != before && now != IntPtr.Zero)
            {
                GrantForeground();
                bool ok = SetForegroundWindow(before);
                Log.Info(ok ? "Cheats: trainer took focus; gave it back." : "Cheats: trainer took focus and it couldn't be given back.");
                break;
            }
        }

        return result;
    }

    public static void GrantForeground()
    {
        var input = new MouseInput { Type = 0, Flags = 0x0001 };
        SendInput(1, [input], Marshal.SizeOf<MouseInput>());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public uint Type;
        public uint Padding;
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint count, MouseInput[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hwnd);
}
