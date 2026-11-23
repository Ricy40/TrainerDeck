using System.Windows.Input;
using TrainerDeck.Core.Cheats;
using TrainerDeck.Core.Input;

namespace TrainerDeck.UI.ViewModels;

public sealed class KeyCaptureViewModel : RowListPageViewModel
{
    private readonly Action<Hotkey> _captured;

    public KeyCaptureViewModel(MainViewModel main, string title, string prompt, Action<Hotkey> captured) : base(main)
    {
        _captured = captured;
        Title = title;
        Body = prompt;
        Subtitle = "Waiting for keys…";
        main.SuspendOverlayHotkey?.Invoke(true);
    }

    public override IReadOnlyList<FooterHint> Hints => [new(InputAction.Back, "Cancel")];

    public override void OnClosed() => Main.SuspendOverlayHotkey?.Invoke(false);

    public bool OnKey(Key key, ModifierKeys modifiers)
    {
        if (key == Key.Escape)
        {
            Main.Pop();
            return true;
        }

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return true;
        }

        var mods = HotkeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            mods |= HotkeyModifiers.Ctrl;
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            mods |= HotkeyModifiers.Alt;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            mods |= HotkeyModifiers.Shift;
        }

        Hotkey? hotkey = Hotkey.FromVirtualKey(KeyInterop.VirtualKeyFromKey(key), mods);
        if (hotkey is null)
        {
            Subtitle = "That key can't be used. Try another.";
            return true;
        }

        if (mods == HotkeyModifiers.None && hotkey.Key is not (>= 0x70 and <= 0x87))
        {
            Subtitle = $"{hotkey} on its own would fire while typing. Add Ctrl, Alt or Shift.";
            return true;
        }

        Main.Pop();
        _captured(hotkey);
        return true;
    }
}
