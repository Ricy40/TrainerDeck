using System.Diagnostics;
using System.IO;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Library;
using TrainerDeck.Core.Settings;
using TrainerDeck.Core.Text;
using TrainerDeck.Core.Theming;

namespace TrainerDeck.UI.ViewModels;

public sealed class SettingsViewModel : RowListPageViewModel
{
    private static readonly string[] AfterLaunchOptions = ["Stay open", "Minimise", "Close Trainer Deck", "Hide to tray"];

    private static readonly (string Label, string Value)[] ShortcutModifiers =
    [
        ("View", "Back"),
        ("LB + RB", "LeftShoulder|RightShoulder"),
        ("Left stick click", "LeftThumb"),
        ("Right stick click", "RightThumb"),
        ("View + Menu", "Back|Start"),
    ];

    private readonly AppServices _s;
    private readonly EventHandler _onLibrary;

    public SettingsViewModel(MainViewModel main) : base(main)
    {
        _s = main.Services;
        Title = "Settings";
        Subtitle = $"Trainer Deck {typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3)}"
            + (_s.IsElevated ? "  ·  running as administrator" : "");
        _onLibrary = (_, _) => main.Dispatcher.BeginInvoke(() =>
        {
            if (!_s.Library.IsScanning)
            {
                Rebuild();
            }
        });
        _s.Library.Changed += _onLibrary;
        Rebuild();
    }

    private AppSettings Current => _s.Settings.Current;

    public override void OnClosed() => _s.Library.Changed -= _onLibrary;

    public override void OnResumed() => Rebuild();

    private void Rebuild()
    {
        var rows = new List<RowViewModel> { new HeaderRow("Administrator") };
        if (Install.InstallState.HasElevatedTask(_s.Paths))
        {
            rows.Add(new ActionRow("Starts as administrator automatically", Glyphs.Shield, () => { }, "OK",
                (_s.IsElevated ? "Running as administrator now. " : "Not running as administrator right now. ")
                + "Set up by the installer through Task Scheduler, so there's no UAC prompt. Uninstall or reinstall to change it."));
        }
        else
        {
            rows.Add(new ToggleRow("Always run as administrator", Current.RunAsAdministrator, SetRunAsAdministrator,
                (_s.IsElevated ? "Running as administrator now. " : "Not running as administrator right now. ")
                + "Needed for the Cheats panel. Windows asks once each time Trainer Deck starts; trainers then launch without their own prompts. "
                + "Install Trainer Deck to skip that prompt."));
        }
        if (!_s.IsElevated)
        {
            rows.Add(new ActionRow("Restart as administrator now", Glyphs.Shield, Main.RequestRestartElevated, "Restart",
                "Also turns on “Always run as administrator”"));
        }

        rows.Add(new HeaderRow("Library"));
        bool usingDefault = Current.TrainerFolders.Count == 0;
        foreach (string folder in _s.Library.Roots)
        {
            string f = folder;
            bool isDefault = usingDefault || string.Equals(f, _s.Paths.DefaultTrainersDirectory, StringComparison.OrdinalIgnoreCase);
            rows.Add(new ActionRow(f, Glyphs.Folder, () => OpenInExplorer(f), "Open",
                isDefault ? "Trainer folder (next to Trainer Deck)" : "Trainer folder",
                _s.Library.Roots.Count > 1 ? () => RemoveFolder(f) : null, "Remove"));
        }

        rows.Add(new ActionRow("Add a trainer…", Glyphs.Download, Main.AddTrainer, "Choose", "Copies an .exe or .zip from Downloads (or anywhere) into your trainer folder"));
        rows.Add(new ActionRow("Add a trainer folder…", Glyphs.Add, AddFolder, "Add", "Scanned together with the folders above, including subfolders"));
        int zips = _s.Library.PendingZips.Count;
        if (zips > 0)
        {
            rows.Add(new ActionRow("Extract zip files", Glyphs.Zip, Main.OpenZipImport, "Open", $"{Humanize.Plural(zips, "zip file")} waiting"));
        }

        int hidden = _s.Library.Entries.Count(e => e.Meta.Hidden);
        rows.Add(new ActionRow("Hidden trainers", Glyphs.Hide, () => Main.Push(new HiddenTrainersViewModel(Main)), "Open",
            hidden == 0 ? "None hidden" : $"{hidden} hidden"));
        rows.Add(new ActionRow("Ignore patterns", Glyphs.Filter, () => Main.Push(new IgnorePatternsViewModel(Main)), "Edit",
            Current.IgnorePatterns.Count == 0 ? "Skip files or folders by name, e.g. *Setup*.exe" : $"{Humanize.Plural(Current.IgnorePatterns.Count, "pattern")}"));
        rows.Add(new ActionRow("Rescan now", Glyphs.Refresh, Rescan, "Rescan", $"{Humanize.Plural(_s.Library.Entries.Count, "trainer")} found"));

        rows.Add(new HeaderRow("Launching"));
        rows.Add(new ChoiceRow("After launching a trainer", AfterLaunchOptions, (int)Current.AfterLaunch,
            i => _s.Settings.Update(s => s.AfterLaunch = (AfterLaunchBehavior)i)));

        rows.Add(new HeaderRow("Cheats"));
        rows.Add(new ToggleRow("In-game controller shortcuts", Current.CheatShortcutsEnabled, v => _s.Settings.Update(s => s.CheatShortcutsEnabled = v),
            "Trigger cheats while you play: hold the button below and press the one assigned in a trainer's Cheats panel. "
            + "Only active while a trainer with shortcuts is running."));
        int modifierIndex = Array.FindIndex(ShortcutModifiers, m => string.Equals(m.Value, Current.CheatShortcutModifier, StringComparison.OrdinalIgnoreCase));
        rows.Add(new ChoiceRow("Shortcut button to hold", ShortcutModifiers.Select(m => m.Label).ToArray(), Math.Max(0, modifierIndex),
            i => _s.Settings.Update(s => s.CheatShortcutModifier = ShortcutModifiers[i].Value),
            "Hold this, then press the button assigned to a cheat"));

        rows.Add(new HeaderRow("Overlay"));
        rows.Add(new ToggleRow("In-game overlay", Current.OverlayEnabled, v => Change(s => s.OverlayEnabled = v),
            "A panel over your game with the running trainer's cheats, controlled with the pad"));
        if (Current.OverlayEnabled)
        {
            bool hasHotkey = !string.IsNullOrWhiteSpace(Current.OverlayHotkey);
            rows.Add(new ActionRow("Keyboard shortcut", Glyphs.Keyboard, RecordHotkey, "Change",
                hasHotkey ? $"{Current.OverlayHotkey}  ·  works from any app. Tip: map an Ally back paddle to it." : "None",
                hasHotkey ? () => Change(s => s.OverlayHotkey = "") : null, "Clear"));
            int comboIndex = Array.FindIndex(OverlayCombos, c => string.Equals(c.Value, Current.OverlayControllerCombo, StringComparison.OrdinalIgnoreCase));
            rows.Add(new ChoiceRow("Controller combo", OverlayCombos.Select(c => c.Label).ToArray(), Math.Max(0, comboIndex),
                i => Change(s => s.OverlayControllerCombo = OverlayCombos[i].Value),
                "Press these together while a trainer is running"));
            if (Main.OpenOverlay is not null)
            {
                rows.Add(new ActionRow("Open the overlay now", Glyphs.View, Main.OpenOverlay, "Open", "Try it out"));
            }
        }

        rows.Add(new HeaderRow("Appearance"));
        rows.Add(new ChoiceRow("Colours", ["Trainer Deck", "Match Windows", "Custom"], (int)Current.Appearance,
            i => Change(s => s.Appearance = (AppearanceMode)i),
            Current.Appearance switch
            {
                AppearanceMode.Windows => "Follows Windows' light or dark mode and accent colour",
                AppearanceMode.Custom => "Your own choice of light or dark and accent colour",
                _ => "The original dark look in Trainer Deck blue",
            }));
        if (Current.Appearance == AppearanceMode.Custom)
        {
            rows.Add(new ChoiceRow("Mode", ["Dark", "Light", "Follow Windows"], (int)Current.CustomTheme,
                i => Change(s => s.CustomTheme = (BaseTheme)i)));
            rows.Add(new ActionRow("Accent colour", Glyphs.Color, PickAccent, "Choose",
                $"{Current.CustomAccent}  ·  from the Windows colours or any hex code"));
        }

        rows.Add(new HeaderRow("Window"));
        rows.Add(new ToggleRow("Start in full screen", Current.StartFullScreen, v => _s.Settings.Update(s => s.StartFullScreen = v),
            "F11 switches at any time"));
        rows.Add(new ChoiceRow("Close button", ["Exit Trainer Deck", "Keep running in the tray"], (int)Current.CloseButton,
            i => _s.Settings.Update(s => s.CloseButton = (CloseButtonBehavior)i),
            "In the tray, Trainer Deck keeps listening for the overlay and cheat shortcuts while a trainer runs"));

        rows.Add(new HeaderRow("Steam"));
        bool hasSteam = _s.Steam.SteamPath is not null;
        rows.Add(new ToggleRow("Use games installed with Steam", Current.UseLocalSteamData, v => SetSteam(s => s.UseLocalSteamData = v),
            hasSteam ? "Card art and a Play button for installed games. Read from your Steam folder; nothing is downloaded." : "Steam isn't installed on this PC")
        { IsEnabled = hasSteam });
        rows.Add(new ToggleRow("Download art from Steam", Current.DownloadSteamArt, v => SetSteam(s => s.DownloadSteamArt = v),
            "Looks up games you don't have installed on the Steam store and downloads their library art"));

        rows.Add(new HeaderRow("Help"));
        rows.Add(new ActionRow("Antivirus tips", Glyphs.Shield, Main.OpenAntivirusNotice, "Open", "Why trainers get flagged, and how to stop it"));
        rows.Add(new ActionRow("Open data folder", Glyphs.Folder, () => OpenInExplorer(_s.Paths.DataDirectory), "Open",
            _s.Paths.DataDirectory + (_s.Paths.IsPortable ? "" : " (the app folder isn't writable)")));

        SetRows(rows);
        HintsChanged();
    }

    private static readonly (string Label, string Value)[] OverlayCombos =
    [
        ("Off", ""),
        ("View + Menu", "Back|Start"),
        ("L3 + R3 (both sticks)", "LeftThumb|RightThumb"),
        ("LB + RB + View", "LeftShoulder|RightShoulder|Back"),
        ("LB + RB + Menu", "LeftShoulder|RightShoulder|Start"),
    ];

    private void PickAccent() =>
        Main.Push(new AccentPickerViewModel(Main, Current.CustomAccent, hex => Change(s => s.CustomAccent = hex)));

    private void Change(Action<AppSettings> change)
    {
        _s.Settings.Update(change);
        Rebuild();
    }

    private void RecordHotkey()
    {
        Main.Push(new KeyCaptureViewModel(Main, "Overlay keyboard shortcut",
            "Press the key combination you want, e.g. Ctrl+Shift+F12. Esc cancels.",
            hotkey =>
            {
                Change(s => s.OverlayHotkey = hotkey.ToString());
                Main.ShowMessage($"Overlay shortcut: {hotkey}");
            }));
    }

    private void SetRunAsAdministrator(bool value)
    {
        _s.Settings.Update(s => s.RunAsAdministrator = value);
        Main.ShowMessage(value
            ? _s.IsElevated ? "Trainer Deck will always start as administrator." : "Applies the next time Trainer Deck starts, or restart now from here."
            : "Trainer Deck will start without administrator rights next time.");
    }

    private void AddFolder()
    {
        Main.PickFolder("Add a trainer folder", null, path =>
        {
            if (_s.Library.Roots.Any(r => string.Equals(r.TrimEnd('\\'), path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
            {
                Main.ShowMessage("That folder is already in your library.");
                return;
            }

            _s.Settings.Update(s =>
            {
                if (s.TrainerFolders.Count == 0)
                {
                    s.TrainerFolders.Add(_s.Paths.DefaultTrainersDirectory);
                }

                s.TrainerFolders.Add(path);
            });
            Main.ShowMessage($"Added {path}");
            Rebuild();
        });
    }

    private void RemoveFolder(string folder)
    {
        _s.Settings.Update(s =>
        {
            if (s.TrainerFolders.Count == 0)
            {
                s.TrainerFolders.AddRange(_s.Library.Roots);
            }

            s.TrainerFolders.RemoveAll(f => string.Equals(f, folder, StringComparison.OrdinalIgnoreCase));
        });
        Main.ShowMessage($"Removed {folder} (files were not touched)");
        Rebuild();
    }

    private void Rescan()
    {
        _ = _s.Library.RescanAsync();
        Main.ShowMessage("Rescanning…");
    }

    private void SetSteam(Action<AppSettings> change)
    {
        _s.Settings.Update(change);
        _ = _s.Steam.RefreshAsync(force: true);
    }

    private void OpenInExplorer(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warn($"Open folder failed: {ex.Message}");
            Main.ShowError("Couldn't open the folder.");
        }
    }
}

public sealed class HiddenTrainersViewModel : RowListPageViewModel
{
    public HiddenTrainersViewModel(MainViewModel main) : base(main)
    {
        Title = "Hidden trainers";
        Rebuild();
    }

    private void Rebuild()
    {
        List<TrainerEntry> hidden = Main.Services.Library.Entries.Where(e => e.Meta.Hidden).OrderBy(e => e.SortKey).ToList();
        Subtitle = hidden.Count == 0 ? "Nothing is hidden" : "Unhide a trainer to put it back in your library";
        SetRows(hidden.Select(e => (RowViewModel)new ActionRow(e.DisplayName, Glyphs.View, () => Unhide(e), "Unhide", e.File.RelativePath)));
        HintsChanged();
    }

    private void Unhide(TrainerEntry e)
    {
        Main.Services.Library.UpdateMetadata(e.Id, m => m.Hidden = false);
        Main.ShowMessage($"{e.DisplayName} is back in your library");
        Rebuild();
    }
}
