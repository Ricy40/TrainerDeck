using TrainerDeck.Art;
using TrainerDeck.Cheats;
using TrainerDeck.Core.Library;
using TrainerDeck.Core.Settings;
using TrainerDeck.Launch;
using TrainerDeck.Steam;

namespace TrainerDeck.UI.ViewModels;

public sealed record AppServices(
    TrainerLibrary Library,
    ArtService Art,
    LaunchService Launcher,
    SteamService Steam,
    SettingsStore Settings,
    PortablePaths Paths,
    bool IsElevated,
    CheatService Cheats);
