using CommunityToolkit.Mvvm.ComponentModel;
using TrainerDeck.Core.Cheats;

namespace TrainerDeck.UI.ViewModels;

public sealed partial class CheatRow : RowViewModel
{
    private readonly Action<CheatRow> _activate;
    private readonly Action<CheatRow, int> _adjust;
    private readonly Action<CheatRow> _shortcut;

    public CheatRow(CheatOption option, Action<CheatRow> activate, Action<CheatRow, int> adjust, Action<CheatRow> shortcut)
    {
        Option = option;
        Label = option.Name;
        HotkeyText = option.Hotkey?.ToString() ?? option.HotkeyText;
        _activate = activate;
        _adjust = adjust;
        _shortcut = shortcut;
    }

    public CheatOption Option { get; }

    public string? HotkeyText { get; }

    public bool HasHotkey => !string.IsNullOrEmpty(HotkeyText);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateKnown), nameof(ActivateLabel))]
    private bool? isOn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValue))]
    private string? value;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasShortcut))]
    private string? shortcutText;

    [ObservableProperty]
    private bool isBlind;

    public bool StateKnown => IsOn is not null && Option.IsToggle;

    public bool HasValue => !string.IsNullOrEmpty(Value);

    public bool HasShortcut => !string.IsNullOrEmpty(ShortcutText);

    public bool IsAction => !Option.IsToggle;

    public bool CanEditValue => Option.HasValue;

    public override string ActivateLabel => Option.Kind switch
    {
        CheatKind.Action or CheatKind.ActionWithValue => "Apply",
        CheatKind.Choice => IsOn == true ? "Turn off" : "Apply",
        _ when IsBlind => "Press hotkey",
        _ => IsOn == true ? "Turn off" : "Turn on",
    };

    public override string? SecondaryLabel => "Controller shortcut";

    public override bool CanAdjust => Option.Kind is CheatKind.ToggleWithStepper && HasValue;

    public override void Activate() => _activate(this);

    public override void Adjust(int delta) => _adjust(this, delta);

    public override void Secondary() => _shortcut(this);
}
