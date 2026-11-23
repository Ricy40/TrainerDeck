namespace TrainerDeck.Core.Settings;


public sealed class PortablePaths
{
    public const string InstallMarkerFile = "installed.json";

    private PortablePaths(string appDirectory, string dataDirectory, bool isPortable, bool isInstalled)
    {
        AppDirectory = appDirectory;
        DataDirectory = dataDirectory;
        IsPortable = isPortable;
        IsInstalled = isInstalled;
    }

    public bool IsInstalled { get; }

    public string AppDirectory { get; }

    public string DataDirectory { get; }

    public bool IsPortable { get; }

    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public string LibraryFile => Path.Combine(DataDirectory, "library.json");

    public string LogFile => Path.Combine(DataDirectory, "trainerdeck.log");

    public string CacheDirectory => Path.Combine(DataDirectory, "cache");

    public string DefaultTrainersDirectory => Path.Combine(AppDirectory, "Trainers");

    public static PortablePaths Resolve(string appDirectory, string? fallbackDirectory = null)
    {
        fallbackDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrainerDeck");

        bool installed = File.Exists(Path.Combine(appDirectory, InstallMarkerFile));
        if (!installed && IsWritable(appDirectory))
        {
            return new PortablePaths(appDirectory, appDirectory, true, false);
        }

        Directory.CreateDirectory(fallbackDirectory);
        return new PortablePaths(appDirectory, fallbackDirectory, false, installed);
    }

    internal static bool IsWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            string probe = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
