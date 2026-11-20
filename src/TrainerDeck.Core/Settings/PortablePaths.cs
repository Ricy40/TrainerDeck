namespace TrainerDeck.Core.Settings;


public sealed class PortablePaths
{
    private PortablePaths(string appDirectory, string dataDirectory, bool isPortable)
    {
        AppDirectory = appDirectory;
        DataDirectory = dataDirectory;
        IsPortable = isPortable;
    }

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

        if (IsWritable(appDirectory))
        {
            return new PortablePaths(appDirectory, appDirectory, true);
        }

        Directory.CreateDirectory(fallbackDirectory);
        return new PortablePaths(appDirectory, fallbackDirectory, false);
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
