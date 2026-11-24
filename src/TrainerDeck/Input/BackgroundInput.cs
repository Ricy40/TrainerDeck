using TrainerDeck.Cheats;
using TrainerDeck.Core.Input;
using TrainerDeck.Core.Settings;

namespace TrainerDeck.Input;


public sealed class BackgroundInput
{
    private readonly CheatService _cheats;
    private readonly SettingsStore _settings;
    private readonly Action _openOverlay;
    private readonly ComboDetector _overlayCombo = new();
    private readonly ChordDetector _chords = new(GamepadButtons.None);

    public BackgroundInput(CheatService cheats, SettingsStore settings, Action openOverlay)
    {
        _cheats = cheats;
        _settings = settings;
        _openOverlay = openOverlay;
    }

    private GamepadButtons OverlayCombo => _settings.Current.OverlayEnabled
        ? ChordDetector.ParseButtons(_settings.Current.OverlayControllerCombo)
        : GamepadButtons.None;

    public bool Wanted =>
        _cheats.ActiveShortcutModifier != GamepadButtons.None
        || OverlayCombo != GamepadButtons.None;

    public void OnButtons(GamepadButtons held)
    {
        _overlayCombo.Combo = OverlayCombo;
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
