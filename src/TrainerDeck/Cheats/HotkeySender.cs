using System.Runtime.InteropServices;
using TrainerDeck.Core.Cheats;
using TrainerDeck.Core.Diagnostics;

namespace TrainerDeck.Cheats;


public static class HotkeySender
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint MapVkToVsc = 0;

    public static Task<bool> SendAsync(Hotkey hotkey) => Task.Run(() =>
    {
        var mods = new List<ushort>();
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Ctrl))
        {
            mods.Add(0x11);
        }

        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            mods.Add(0x12);
        }

        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            mods.Add(0x10);
        }

        bool ok = true;
        foreach (ushort m in mods)
        {
            ok &= Send(m, false, false);
        }

        Thread.Sleep(20);
        ok &= Send((ushort)hotkey.Key, false, hotkey.IsExtendedKey);
        Thread.Sleep(80);
        ok &= Send((ushort)hotkey.Key, true, hotkey.IsExtendedKey);
        Thread.Sleep(20);
        foreach (ushort m in Enumerable.Reverse(mods))
        {
            ok &= Send(m, true, false);
        }

        if (!ok)
        {
            Log.Warn($"Cheats: SendInput failed for {hotkey} (error {Marshal.GetLastWin32Error()}).");
        }

        return ok;
    });

    private static bool Send(ushort vk, bool up, bool extended)
    {
        var input = new INPUT
        {
            type = InputKeyboard,
            ki = new KEYBDINPUT
            {
                wVk = vk,
                wScan = (ushort)MapVirtualKeyW(vk, MapVkToVsc),
                dwFlags = (up ? KeyEventKeyUp : 0) | (extended ? KeyEventExtendedKey : 0),
            },
        };
        return SendInput(1, [input], Marshal.SizeOf<INPUT>()) == 1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct INPUT
    {
        [FieldOffset(0)]
        public uint type;

        [FieldOffset(8)]
        public KEYBDINPUT ki;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, INPUT[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKeyW(uint code, uint mapType);
}
