using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrainerDeck.Core.Storage;


public static class JsonFile
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static T LoadOrDefault<T>(string path, Action<string>? onWarning = null)
        where T : new()
    {
        if (!File.Exists(path))
        {
            return new T();
        }

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, Options) ?? new T();
        }
        catch (JsonException ex)
        {
            string backup = $"{path}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
            try
            {
                File.Move(path, backup, overwrite: true);
            }
            catch (IOException)
            {
            }

            onWarning?.Invoke($"{Path.GetFileName(path)} was unreadable ({ex.Message}); moved to {Path.GetFileName(backup)}.");
            return new T();
        }
    }

    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
        File.Move(tmp, path, overwrite: true);
    }
}
