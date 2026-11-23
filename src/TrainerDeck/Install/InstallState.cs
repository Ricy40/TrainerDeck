using System.IO;
using System.Text.Json;
using TrainerDeck.Core.Install;
using TrainerDeck.Core.Settings;

namespace TrainerDeck.Install;

public static class InstallState
{
    public static InstallInfo? Read(PortablePaths paths)
    {
        try
        {
            string marker = Path.Combine(paths.AppDirectory, PortablePaths.InstallMarkerFile);
            return File.Exists(marker) ? JsonSerializer.Deserialize<InstallInfo>(File.ReadAllText(marker)) : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static bool HasElevatedTask(PortablePaths paths) => Read(paths)?.ElevatedTask == true;
}
