using System.Windows.Threading;
using TrainerDeck.Cheats;
using TrainerDeck.Core.Cheats;
using TrainerDeck.Core.Input;
using TrainerDeck.UI.Input;

namespace TrainerDeck.UI.ViewModels;


public sealed class CheatsViewModel : RowListPageViewModel
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly AppServices _s;
    private readonly CheatService _cheats;
    private readonly TrainerCardViewModel _card;
    private readonly DispatcherTimer _poll;
    private readonly EventHandler _onRunning;
    private SessionSnapshot _snapshot;
    private List<CheatRow> _cheatRows = new();
    private bool _polling;
    private bool _hadCheats;

    public CheatsViewModel(MainViewModel main, TrainerCardViewModel card) : base(main)
    {
        _s = main.Services;
        _cheats = _s.Cheats;
        _card = card;
        Title = card.Title;
        _poll = new DispatcherTimer(PollInterval, DispatcherPriority.Background, (_, _) => _ = PollStates(), main.Dispatcher) { IsEnabled = false };
        _onRunning = (_, _) => main.Dispatcher.BeginInvoke(OnRunningChanged);
        _s.Launcher.Running.Changed += _onRunning;

        _snapshot = _cheats.CachedSnapshot(card.Id);
        Build();
        _ = Connect();
    }

    public override IReadOnlyList<FooterHint> Hints
    {
        get
        {
            var hints = new List<FooterHint>();
            if (SelectedRow is CheatRow row)
            {
                hints.Add(new(InputAction.Activate, row.ActivateLabel));
                if (row.CanAdjust)
                {
                    hints.Add(new(InputAction.Left, "Adjust"));
                }

                if (row.CanEditValue && _snapshot.Status == SessionStatus.Connected)
                {
                    hints.Add(new(InputAction.Details, row.Option.Kind == CheatKind.Choice ? "Choose" : "Edit value"));
                }

                hints.Add(new(InputAction.Favourite, "Shortcut"));
            }
            else if (SelectedRow is { } other)
            {
                hints.Add(new(InputAction.Activate, other.ActivateLabel));
            }

            hints.Add(new(InputAction.Back, "Back"));
            return hints;
        }
    }

    protected override bool HandlePage(InputAction action)
    {
        if (action == InputAction.Details && SelectedRow is CheatRow row)
        {
            EditValue(row);
            return true;
        }

        return false;
    }

    public override void OnClosed()
    {
        _poll.Stop();
        _s.Launcher.Running.Changed -= _onRunning;
    }

    public override void OnResumed()
    {
        Build();
        if (_snapshot.Status == SessionStatus.Connected)
        {
            _poll.Start();
        }
    }

    private async Task Connect()
    {
        Subtitle = "Reading the trainer…";
        SessionSnapshot snapshot = await _cheats.ConnectAsync(_card.Id);
        _snapshot = snapshot;
        Build();
        if (snapshot.Status == SessionStatus.Connected)
        {
            _poll.Start();
            await PollStates();
        }
    }

    private void Build()
    {
        bool running = _s.Launcher.Running.IsRunning(_card.Id);
        IReadOnlyDictionary<string, string> shortcuts = _cheats.Shortcuts(_card.Id);
        Dictionary<string, string> byCheat = shortcuts.GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.First().Key);
        bool blind = _snapshot.Status != SessionStatus.Connected;

        var rows = new List<RowViewModel>();
        if (running)
        {
            rows.Add(new ActionRow("Show trainer window", Glyphs.View, SwitchToTrainer, "Switch to", "Brings the trainer itself to the front"));
        }

        if (_snapshot.Status == SessionStatus.NeedsAdministrator && !_cheats.IsElevated)
        {
            rows.Add(new ActionRow("Restart Trainer Deck as administrator", Glyphs.Shield, Main.RequestRestartElevated, "Restart",
                "Needed to read this trainer's switches and values. Turns on “Run Trainer Deck as administrator”."));
        }

        _cheatRows = new List<CheatRow>();
        foreach (CheatOption o in _snapshot.Options)
        {
            if (o.Kind == CheatKind.Section)
            {
                rows.Add(new HeaderRow(o.Name));
                continue;
            }

            var row = new CheatRow(o, Trigger, Step, PickShortcut)
            {
                IsBlind = blind,
                Detail = DetailFor(o, blind),
                ShortcutText = byCheat.TryGetValue(o.Key, out string? b) ? ShortcutLabel(CheatService.ParseButtons(b)) : null,
                IsEnabled = running && (o.Hotkey is not null || !blind),
            };
            _cheatRows.Add(row);
            rows.Add(row);
        }

        Subtitle = StatusText(running);
        Body = BodyText(running);
        bool firstCheats = !_hadCheats && _cheatRows.Count > 0;
        SetRows(rows);
        if (firstCheats && _cheatRows.FirstOrDefault(r => r.IsSelectable) is CheatRow first)
        {
            SelectedRow = first;
        }

        _hadCheats |= _cheatRows.Count > 0;

        HintsChanged();
    }

    private static string? DetailFor(CheatOption o, bool blind) => o.Kind switch
    {
        CheatKind.Action or CheatKind.ActionWithValue => "One-off action",
        _ when blind && o.Hotkey is null => "No hotkey, needs the trainer window",
        _ => null,
    };

    private string StatusText(bool running)
    {
        if (!running)
        {
            return "Not running";
        }

        string count = $"{_cheatRows.Count} cheats";
        return _snapshot.Status switch
        {
            SessionStatus.Connecting => _cheatRows.Count > 0 ? $"Reading the trainer…  ·  {count} from last time" : "Reading the trainer…",
            SessionStatus.Connected => _snapshot.GameDetected switch
            {
                true => $"{count}  ·  game detected",
                false => $"{count}  ·  waiting for the game",
                _ => count,
            },
            SessionStatus.HotkeysOnly => $"{count}  ·  using hotkeys",
            _ => _cheatRows.Count > 0 ? $"{count}  ·  using hotkeys" : "Couldn't read the trainer",
        };
    }

    private string? BodyText(bool running)
    {
        if (!running)
        {
            return "Launch the trainer to use its cheats from here.";
        }

        return _snapshot.Status switch
        {
            SessionStatus.HotkeysOnly =>
                "This is an older trainer: Trainer Deck presses its hotkeys for you, but can't see whether a cheat is on. Listen for the trainer's beep.",
            SessionStatus.NeedsAdministrator when _cheatRows.Count > 0 =>
                "The trainer runs as administrator, so Trainer Deck can only press its hotkeys and can't see what's on. Restart Trainer Deck as administrator for the full panel.",
            SessionStatus.NeedsAdministrator =>
                "The trainer runs as administrator, so Trainer Deck can't read its cheats. Restart Trainer Deck as administrator to use this panel.",
            SessionStatus.Unreadable when _cheatRows.Count == 0 =>
                "This trainer's window couldn't be read. Use “Show trainer window” to control it directly.",
            _ => null,
        };
    }

    private void OnRunningChanged()
    {
        bool running = _s.Launcher.Running.IsRunning(_card.Id);
        if (!running)
        {
            _poll.Stop();
            Main.ShowMessage($"{_card.Title} closed");
            Build();
        }
        else if (_snapshot.Status != SessionStatus.Connected)
        {
            _ = Connect();
        }
    }

    private async Task PollStates()
    {
        if (_polling || _snapshot.Status != SessionStatus.Connected || _cheats.ExistingSession(_card.Id) is not TrainerSession session)
        {
            return;
        }

        _polling = true;
        try
        {
            var (states, game) = await session.ReadStatesAsync();
            if (states is null)
            {
                return;
            }

            for (int i = 0; i < _cheatRows.Count; i++)
            {
                CheatRow row = _cheatRows[i];
                if (row.Option.Index < states.Count)
                {
                    CheatState st = states[row.Option.Index];
                    row.IsOn = st.IsOn;
                    if (!string.IsNullOrEmpty(st.Value))
                    {
                        row.Value = st.Value;
                    }
                }
            }

            if (game != _snapshot.GameDetected)
            {
                _snapshot = _snapshot with { GameDetected = game };
                Subtitle = StatusText(true);
            }

            HintsChanged();
        }
        finally
        {
            _polling = false;
        }
    }

    private async void Trigger(CheatRow row)
    {
        TriggerResult result = await _cheats.TriggerAsync(_card.Id, row.Option);
        switch (result)
        {
            case TriggerResult.Toggled:
                await Task.Delay(120);
                await PollStates();
                if (!row.Option.IsToggle)
                {
                    Main.ShowMessage($"{row.Label} applied");
                }

                break;
            case TriggerResult.SentHotkey:
                Main.ShowMessage($"Pressed {row.HotkeyText} for {row.Label}");
                break;
            default:
                Main.ShowError($"Couldn't trigger {row.Label}.");
                break;
        }
    }

    private async void Step(CheatRow row, int direction)
    {
        if (row.Value is not string current || _cheats.ExistingSession(_card.Id) is not TrainerSession session)
        {
            return;
        }

        string next = CheatValue.Step(current, direction);
        if (next == current)
        {
            return;
        }

        row.Value = next;
        if (!await session.SetValueAsync(row.Option.Index, next) && row.Value == next)
        {
            row.Value = current;
        }
    }

    private void EditValue(CheatRow row)
    {
        if (!row.CanEditValue || _snapshot.Status != SessionStatus.Connected || _cheats.ExistingSession(_card.Id) is not TrainerSession session)
        {
            return;
        }

        if (row.Option.Kind == CheatKind.Choice)
        {
            _ = PickChoice(row, session);
            return;
        }

        Main.Push(new TextEntryViewModel(Main, row.Label, "Type a value, then press Enter (A). The trainer uses it the next time this cheat is turned on or applied.",
            row.Value ?? "", async value =>
            {
                if (await session.SetValueAsync(row.Option.Index, value.Trim()))
                {
                    row.Value = value.Trim();
                    Main.ShowMessage($"{row.Label}: {value.Trim()}");
                }
                else
                {
                    Main.ShowError("The trainer didn't accept that value.");
                }
            }));
    }

    private async Task PickChoice(CheatRow row, TrainerSession session)
    {
        IReadOnlyList<string> choices = await session.ReadChoicesAsync(row.Option.Index);
        if (choices is null || choices.Count == 0)
        {
            Main.ShowMessage(_snapshot.GameDetected == true
                ? "The trainer hasn't filled this list yet."
                : "This list fills in once the game is running.");
            return;
        }

        Main.Push(new PickerViewModel(Main, row.Label, "Pick an entry, then turn the cheat on.",
            choices.Select(c => new PickerItem(c, c == row.Value ? Glyphs.Check : null, null, async () =>
            {
                if (await session.SelectChoiceAsync(row.Option.Index, c))
                {
                    row.Value = c;
                }
            })).ToList()));
    }

    private void PickShortcut(CheatRow row)
    {
        GamepadButtons modifier = _cheats.ConfiguredModifier;
        ControllerKind kind = Main.LastControllerKind;
        string mod = ControllerGlyphs.Describe(modifier, kind);
        IReadOnlyDictionary<string, string> current = _cheats.Shortcuts(_card.Id);

        var items = new List<PickerItem>
        {
            new("No shortcut", Glyphs.Close, null, () => SetShortcut(row, GamepadButtons.None)),
        };
        foreach (GamepadButtons b in ShortcutButtons.Where(b => (b & modifier) == 0))
        {
            string? usedBy = current.TryGetValue(b.ToString(), out string? key) && key != row.Option.Key
                ? _cheatRows.FirstOrDefault(r => r.Option.Key == key)?.Label ?? key
                : null;
            items.Add(new PickerItem($"{mod} + {ControllerGlyphs.Describe(b, kind)}", null, usedBy is null ? null : $"Replaces {usedBy}",
                () => SetShortcut(row, b)));
        }

        string note = Main.Settings.Current.CheatShortcutsEnabled
            ? $"While you play, hold {mod} and press the button to trigger {row.Label}. The game also sees the button, so pick one it doesn't need."
            : $"While you play, hold {mod} and press the button to trigger {row.Label}. In-game shortcuts are off: turn them on in Settings › Cheats.";
        Main.Push(new PickerViewModel(Main, $"Shortcut for {row.Label}", note, items));
    }

    private void SetShortcut(CheatRow row, GamepadButtons button)
    {
        if (button == GamepadButtons.None)
        {
            _cheats.ClearShortcutFor(_card.Id, row.Option.Key);
            Main.ShowMessage($"Removed the shortcut for {row.Label}");
        }
        else
        {
            _cheats.SetShortcut(_card.Id, button, row.Option.Key);
            Main.ShowMessage($"{ShortcutLabel(button)} now triggers {row.Label}");
        }

        Build();
    }

    private string ShortcutLabel(GamepadButtons button) =>
        $"{ControllerGlyphs.Describe(_cheats.ConfiguredModifier, Main.LastControllerKind)} + {ControllerGlyphs.Describe(button, Main.LastControllerKind)}";

    private void SwitchToTrainer()
    {
        if (_s.Launcher.Running.TryGet(_card.Id) is { } running && _s.Launcher.Tracker.BringToFront(running.ProcessId))
        {
            return;
        }

        Main.ShowError("Couldn't bring the trainer window to the front.");
    }

    private static readonly GamepadButtons[] ShortcutButtons =
    [
        GamepadButtons.A, GamepadButtons.B, GamepadButtons.X, GamepadButtons.Y,
        GamepadButtons.DPadUp, GamepadButtons.DPadDown, GamepadButtons.DPadLeft, GamepadButtons.DPadRight,
        GamepadButtons.LeftShoulder, GamepadButtons.RightShoulder, GamepadButtons.LeftTrigger, GamepadButtons.RightTrigger,
        GamepadButtons.LeftThumb, GamepadButtons.RightThumb, GamepadButtons.Start, GamepadButtons.Back,
    ];
}
