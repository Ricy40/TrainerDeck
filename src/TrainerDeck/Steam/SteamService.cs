using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Microsoft.Win32;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Library;
using TrainerDeck.Core.Settings;
using TrainerDeck.Core.Steam;
using TrainerDeck.Core.Storage;

namespace TrainerDeck.Steam;

public sealed class SteamService
{
    private static readonly HttpClient Http = CreateClient();

    private readonly TrainerLibrary _library;
    private readonly SettingsStore _settings;
    private readonly string _cacheDir;
    private readonly string _searchCacheFile;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly SemaphoreSlim _downloadGate = new(2, 2);
    private Dictionary<string, int> _searchCache;
    private Dictionary<int, SteamApp> _installed = new();

    public SteamService(TrainerLibrary library, SettingsStore settings, PortablePaths paths)
    {
        _library = library;
        _settings = settings;
        _cacheDir = Path.Combine(paths.CacheDirectory, "steam");
        _searchCacheFile = Path.Combine(paths.CacheDirectory, "steam-search.json");
        _searchCache = JsonFile.LoadOrDefault<Dictionary<string, int>>(_searchCacheFile, Log.Warn);
        SteamPath = FindSteam();
        Log.Info(SteamPath is null ? "Steam not found." : $"Steam found at {SteamPath}");
    }

    public string? SteamPath { get; }

    public bool UseLocal => _settings.Current.UseLocalSteamData && SteamPath is not null;

    public bool AllowDownloads => _settings.Current.DownloadSteamArt;

    public event EventHandler? Changed;

    public bool IsInstalled(int appId) => _installed.TryGetValue(appId, out SteamApp? app) && app.Installed;

    public string? InstalledName(int appId) => _installed.GetValueOrDefault(appId)?.Name;

    public async Task RefreshAsync(bool force = false)
    {
        if (!await _refreshGate.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await Task.Run(() => RefreshCore(force)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warn($"Steam refresh failed: {ex.Message}");
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task RefreshCore(bool force)
    {
        _installed = UseLocal
            ? SteamLibrary.InstalledApps(SteamPath!).ToDictionary(a => a.AppId)
            : new Dictionary<int, SteamApp>();

        var candidates = _installed.Values.Where(a => a.Installed).Select(a => (a.AppId, a.Name)).ToList();
        bool changed = false;
        foreach (TrainerEntry entry in _library.Entries)
        {
            if (entry.Meta.SteamAppIdLocked)
            {
                continue;
            }

            int? id = candidates.Count > 0 ? SteamMatcher.Match(entry.DisplayName, candidates) : null;
            if (id is null && AllowDownloads && entry.Meta.SteamAppId is null)
            {
                id = await SearchStoreAsync(entry.DisplayName).ConfigureAwait(false);
            }

            if (id is int found && found != entry.Meta.SteamAppId)
            {
                _library.Store.Update(entry.Id, m => m.SteamAppId = found);
                changed = true;
            }
        }

        if (changed)
        {
            _library.SaveMetadata();
            Log.Info("Steam app ids updated.");
        }

        if (changed || force)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetAppId(string trainerId, int? appId)
    {
        _library.Store.Update(trainerId, m =>
        {
            m.SteamAppId = appId;
            m.SteamAppIdLocked = true;
        });
        _library.SaveMetadata();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task<int?> SearchStoreAsync(string gameName)
    {
        string key = SteamMatcher.Normalize(gameName);
        if (key.Length == 0)
        {
            return null;
        }

        lock (_searchCache)
        {
            if (_searchCache.TryGetValue(key, out int cached))
            {
                return cached == 0 ? null : cached;
            }
        }

        int result = 0;
        try
        {
            string json = await Http.GetStringAsync(SteamLibrary.StoreSearchUrl(gameName)).ConfigureAwait(false);
            result = SteamMatcher.Match(gameName, SteamLibrary.ParseStoreSearch(json)) ?? 0;
            await Task.Delay(250).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Log.Warn($"Steam store search failed for {gameName}: {ex.Message}");
            return null;
        }

        lock (_searchCache)
        {
            _searchCache[key] = result;
            try
            {
                JsonFile.Save(_searchCacheFile, _searchCache);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return result == 0 ? null : result;
    }

    public async Task<string?> GetArtPathAsync(int appId, SteamArtKind kind)
    {
        if (UseLocal && SteamLibrary.FindLocalArt(SteamPath!, appId, kind) is string local)
        {
            return local;
        }

        string cached = Path.Combine(_cacheDir, $"{appId}_{kind.ToString().ToLowerInvariant()}.jpg");
        if (File.Exists(cached))
        {
            return new FileInfo(cached).Length > 0 ? cached : null;
        }

        if (!AllowDownloads)
        {
            return null;
        }

        await _downloadGate.WaitAsync().ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_cacheDir);
            using HttpResponseMessage response = await Http.GetAsync(SteamLibrary.CdnUrl(appId, kind)).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                await File.WriteAllBytesAsync(cached, []).ConfigureAwait(false);
                return null;
            }

            byte[] bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            string temp = cached + ".tmp";
            await File.WriteAllBytesAsync(temp, bytes).ConfigureAwait(false);
            File.Move(temp, cached, overwrite: true);
            return cached;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            Log.Warn($"Steam art download failed for {appId}/{kind}: {ex.Message}");
            return null;
        }
        finally
        {
            _downloadGate.Release();
        }
    }

    public bool LaunchGame(int appId)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"steam://rungameid/{appId}") { UseShellExecute = false });
            Log.Info($"Asked Steam to launch app {appId}.");
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"Steam launch failed: {ex.Message}");
            return false;
        }
    }

    private static string? FindSteam()
    {
        string? path = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string
            ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string;
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        path = Path.GetFullPath(path.Replace('/', '\\'));
        return Directory.Exists(path) ? path : null;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TrainerDeck/0.1");
        return client;
    }
}
