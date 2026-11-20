namespace TrainerDeck.Core.Settings;

public enum AfterLaunchBehavior
{
    StayOpen,
    Minimize,
    Close,
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

    public IReadOnlyList<string> EffectiveTrainerFolders(PortablePaths paths) =>
        TrainerFolders.Count > 0 ? TrainerFolders : [paths.DefaultTrainersDirectory];
}
