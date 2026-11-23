namespace TrainerDeck.Core.Settings;

public enum AfterLaunchBehavior
{
    StayOpen,
    Minimize,
    Close,
    HideToTray,
}

public enum CloseButtonBehavior
{
    Exit,
    Tray,
}

public sealed class AppSettings
{
    public List<string> TrainerFolders { get; set; } = new();

    public List<string> IgnorePatterns { get; set; } = new();

    public bool StartFullScreen { get; set; } = true;

    public AfterLaunchBehavior AfterLaunch { get; set; } = AfterLaunchBehavior.Minimize;

    public bool RunAsAdministrator { get; set; }

    public bool UseLocalSteamData { get; set; } = true;

    public bool DownloadSteamArt { get; set; }

    public bool AntivirusNoticeDismissed { get; set; }

    public bool CheatShortcutsEnabled { get; set; }

    public string CheatShortcutModifier { get; set; } = "Back";

    public CloseButtonBehavior CloseButton { get; set; } = CloseButtonBehavior.Exit;

    public bool OverlayEnabled { get; set; } = true;

    public string OverlayHotkey { get; set; } = "Ctrl+Shift+F12";

    public string OverlayControllerCombo { get; set; } = "Back|Start";

    public IReadOnlyList<string> EffectiveTrainerFolders(PortablePaths paths) =>
        TrainerFolders.Count > 0 ? TrainerFolders : [paths.DefaultTrainersDirectory];
}
