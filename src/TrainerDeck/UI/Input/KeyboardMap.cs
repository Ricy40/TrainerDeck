using System.Windows.Input;
using TrainerDeck.Core.Input;

namespace TrainerDeck.UI.Input;

public static class KeyboardMap
{
    public static InputAction? Map(Key key, ModifierKeys mods)
    {
        bool ctrl = mods.HasFlag(ModifierKeys.Control);
        bool shift = mods.HasFlag(ModifierKeys.Shift);
        bool alt = mods.HasFlag(ModifierKeys.Alt);

        return key switch
        {
            Key.Up => InputAction.Up,
            Key.Down => InputAction.Down,
            Key.Left => InputAction.Left,
            Key.Right => InputAction.Right,
            Key.Tab when ctrl => shift ? InputAction.PrevTab : InputAction.NextTab,
            Key.Tab => shift ? InputAction.Left : InputAction.Right,
            Key.PageUp => InputAction.PageUp,
            Key.PageDown => InputAction.PageDown,
            Key.Home => InputAction.Home,
            Key.End => InputAction.End,
            Key.Enter when alt => InputAction.ToggleFullScreen,
            Key.Enter => InputAction.Activate,
            Key.Escape => InputAction.Back,
            Key.BrowserBack => InputAction.Back,
            Key.D when ctrl => InputAction.Favourite,
            Key.I when ctrl => InputAction.Details,
            Key.Apps => InputAction.Details,
            Key.S when ctrl => InputAction.Sort,
            Key.OemComma when ctrl => InputAction.Settings,
            Key.F5 => InputAction.Rescan,
            Key.R when ctrl => InputAction.Rescan,
            Key.F11 => InputAction.ToggleFullScreen,
            _ => null,
        };
    }

    public static string Label(InputAction action) => action switch
    {
        InputAction.Activate => "Enter",
        InputAction.Back => "Esc",
        InputAction.Favourite => "Ctrl+D",
        InputAction.Details => "Ctrl+I",
        InputAction.NextTab or InputAction.PrevTab => "Ctrl+Tab",
        InputAction.Sort => "Ctrl+S",
        InputAction.Settings => "Ctrl+,",
        InputAction.Rescan => "F5",
        InputAction.ToggleFullScreen => "F11",
        InputAction.LetterRail => "Release",
        InputAction.PageUp => "PgUp",
        InputAction.PageDown => "PgDn",
        InputAction.Up or InputAction.Down or InputAction.Left or InputAction.Right => "Arrows",
        _ => action.ToString(),
    };
}
