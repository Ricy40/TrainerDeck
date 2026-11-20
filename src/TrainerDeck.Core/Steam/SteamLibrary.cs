using System.Text.Json;

namespace TrainerDeck.Core.Steam;

public sealed record SteamApp(int AppId, string Name, string InstallDirectory, bool Installed);

public enum SteamArtKind
{
    Portrait,
    Hero,
    Header,
    Logo,
}

public static class SteamLibrary
{
    private static readonly HashSet<int> IgnoredApps = [228980, 1070560, 1391110, 1493710, 1628350, 2180100, 250820];

    public static IReadOnlyList<string> LibraryFolders(string steamPath)
    {
        var folders = new List<string> { steamPath };
        string vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf))
        {
            vdf = Path.Combine(steamPath, "config", "libraryfolders.vdf");
        }

        if (File.Exists(vdf))
        {
            try
            {
                folders.AddRange(ParseLibraryFolders(File.ReadAllText(vdf)));
            }
            catch (IOException)
            {
            }
        }

        return folders
            .Select(f => f.TrimEnd('\\', '/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IEnumerable<string> ParseLibraryFolders(string vdfText)
    {
        VdfNode root = Vdf.Parse(vdfText);
        VdfNode? folders = root.GetNode("libraryfolders") ?? root.GetNode("LibraryFolders");
        if (folders is null)
        {
            yield break;
        }

        foreach (string key in folders.Keys)
        {
            if (!int.TryParse(key, out _))
            {
                continue;
            }

            string? path = folders[key] switch
            {
                VdfNode n => n.GetString("path"),
                string s => s,
                _ => null,
            };

            if (!string.IsNullOrWhiteSpace(path))
            {
                yield return path;
            }
        }
    }

    public static SteamApp? ParseManifest(string acfText, string libraryFolder)
    {
        VdfNode? state = Vdf.Parse(acfText).GetNode("AppState");
        if (state is null || !int.TryParse(state.GetString("appid"), out int appId) || IgnoredApps.Contains(appId))
        {
            return null;
        }

        string? name = state.GetString("name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        int.TryParse(state.GetString("StateFlags"), out int flags);
        string installDir = Path.Combine(libraryFolder, "steamapps", "common", state.GetString("installdir") ?? "");
        return new SteamApp(appId, name.Trim(), installDir, (flags & 4) != 0);
    }

    public static List<SteamApp> InstalledApps(string steamPath)
    {
        var apps = new Dictionary<int, SteamApp>();
        foreach (string folder in LibraryFolders(steamPath))
        {
            string steamapps = Path.Combine(folder, "steamapps");
            if (!Directory.Exists(steamapps))
            {
                continue;
            }

            IEnumerable<string> manifests;
            try
            {
                manifests = Directory.EnumerateFiles(steamapps, "appmanifest_*.acf").ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (string acf in manifests)
            {
                try
                {
                    if (ParseManifest(File.ReadAllText(acf), folder) is SteamApp app)
                    {
                        apps.TryAdd(app.AppId, app);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        }

        return apps.Values.OrderBy(a => a.AppId).ToList();
    }

    public static string? FindLocalArt(string steamPath, int appId, SteamArtKind kind)
    {
        string cache = Path.Combine(steamPath, "appcache", "librarycache");
        string[] names = kind switch
        {
            SteamArtKind.Portrait => ["library_600x900_2x.jpg", "library_600x900.jpg"],
            SteamArtKind.Hero => ["library_hero_2x.jpg", "library_hero.jpg"],
            SteamArtKind.Header => ["library_header_2x.jpg", "library_header.jpg", "header.jpg"],
            _ => ["logo_2x.png", "logo.png"],
        };

        foreach (string n in names)
        {
            string flat = Path.Combine(cache, $"{appId}_{n}");
            if (File.Exists(flat))
            {
                return flat;
            }
        }

        string dir = Path.Combine(cache, appId.ToString());
        if (!Directory.Exists(dir))
        {
            return null;
        }

        try
        {
            foreach (string n in names)
            {
                string direct = Path.Combine(dir, n);
                if (File.Exists(direct))
                {
                    return direct;
                }

                string? nested = Directory.EnumerateFiles(dir, n, SearchOption.AllDirectories).FirstOrDefault();
                if (nested is not null)
                {
                    return nested;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return null;
    }

    public static string CdnUrl(int appId, SteamArtKind kind) => kind switch
    {
        SteamArtKind.Portrait => $"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{appId}/library_600x900_2x.jpg",
        SteamArtKind.Hero => $"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{appId}/library_hero.jpg",
        SteamArtKind.Header => $"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{appId}/header.jpg",
        _ => $"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{appId}/logo.png",
    };

    public static string StoreSearchUrl(string term) =>
        $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(term)}&l=english&cc=US";

    public static List<(int AppId, string Name)> ParseStoreSearch(string json)
    {
        var results = new List<(int, string)>();
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out JsonElement items) || items.ValueKind != JsonValueKind.Array)
            {
                return results;
            }

            foreach (JsonElement item in items.EnumerateArray())
            {
                if (item.TryGetProperty("type", out JsonElement type) && type.GetString() != "app")
                {
                    continue;
                }

                if (item.TryGetProperty("id", out JsonElement id) && id.TryGetInt32(out int appId)
                    && item.TryGetProperty("name", out JsonElement name) && name.GetString() is { Length: > 0 } n)
                {
                    results.Add((appId, n));
                }
            }
        }
        catch (JsonException)
        {
        }

        return results;
    }
}
