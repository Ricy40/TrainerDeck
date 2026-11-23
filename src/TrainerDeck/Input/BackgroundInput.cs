using TrainerDeck.Cheats;
using TrainerDeck.Core.Input;
using TrainerDeck.Core.Launch;
using TrainerDeck.Core.Settings;

namespace TrainerDeck.Input;


public sealed class BackgroundInput
{
    private readonly CheatService _cheats;
    private readonly SettingsStore _settings;
    private readonly RunningTrainers _running;
    private readonly Action _openOverlay;
    private readonly ComboDetector _overlayCombo = new();
    private readonly ChordDetector _chords = new(GamepadButtons.None);

    public BackgroundInput(CheatService cheats, SettingsStore settings, RunningTrainers running, Action openOverlay)
    {
        _cheats = cheats;
        _settings = settings;
        _running = running;
        _openOverlay = openOverlay;
    }

    private GamepadButtons OverlayCombo => _settings.Current.OverlayEnabled
        ? ChordDetector.ParseButtons(_settings.Current.OverlayControllerCombo)
        : GamepadButtons.None;

    public bool Wanted =>
        _cheats.ActiveShortcutModifier != GamepadButtons.None
        || (OverlayCombo != GamepadButtons.None && _running.Snapshot().Count > 0);

    public void OnButtons(GamepadButtons held)
    {
        _overlayCombo.Combo = _running.Snapshot().Count > 0 ? OverlayCombo : GamepadButtons.None;
        if (_overlayCombo.Update(held))
        {
            _chords.Reset();
            _openOverlay();
            return;
        }

        _chords.Modifier = _cheats.ActiveShortcutModifier;
        foreach (GamepadButtons b in _chords.Update(held))
        {
            _cheats.OnChord(b);
        }
    }
}
