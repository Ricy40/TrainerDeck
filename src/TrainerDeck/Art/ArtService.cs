using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Library;
using TrainerDeck.Core.Settings;
using TrainerDeck.Core.Steam;
using TrainerDeck.Steam;

namespace TrainerDeck.Art;

public sealed record CardArt(BitmapSource Image, Color Accent, string Fingerprint);


public sealed class ArtService
{
    public static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tif", ".tiff"];

    private readonly string _iconDir;
    private readonly string _coverDir;
    private readonly string _dataDir;
    private readonly SteamService _steam;
    private readonly SemaphoreSlim _throttle = new(4);
    private readonly Dictionary<string, HashSet<string>> _iconUsers = new();
    private readonly HashSet<string> _genericIcons = new() { "0000027FFE7E3C08" };

    public ArtService(PortablePaths paths, SteamService steam)
    {
        _iconDir = Path.Combine(paths.CacheDirectory, "icons");
        _coverDir = Path.Combine(paths.CacheDirectory, "covers");
        _dataDir = paths.DataDirectory;
        _steam = steam;
    }

    public SteamService Steam => _steam;

    public event EventHandler? GenericIconsChanged;

    public bool IsGenericIcon(string fingerprint)
    {
        lock (_genericIcons)
        {
            return _genericIcons.Any(g => SameIcon(g, fingerprint));
        }
    }

    public void NoteIcon(string fingerprint, string trainerId)
    {
        bool added;
        lock (_genericIcons)
        {
            string key = _iconUsers.Keys.FirstOrDefault(k => SameIcon(k, fingerprint)) ?? fingerprint;
            if (!_iconUsers.TryGetValue(key, out HashSet<string>? users))
            {
                _iconUsers[key] = users = new HashSet<string>();
            }

            users.Add(trainerId);
            added = users.Count >= GenericIconThreshold && !_genericIcons.Any(g => SameIcon(g, key)) && _genericIcons.Add(key);
        }

        if (added)
        {
            Log.Info($"Icon {fingerprint} is shared by {GenericIconThreshold}+ trainers; showing name tiles instead.");
            GenericIconsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private const int GenericIconThreshold = 3;

    public string? ResolveCustomArt(TrainerEntry entry) =>
        string.IsNullOrWhiteSpace(entry.Meta.ArtPath) ? null : Path.GetFullPath(Path.Combine(_dataDir, entry.Meta.ArtPath));

    public async Task<BitmapSource?> GetCoverAsync(TrainerEntry entry)
    {
        string? path = ResolveCustomArt(entry);
        if (path is null && entry.Meta.SteamAppId is int appId)
        {
            path = await _steam.GetArtPathAsync(appId, SteamArtKind.Portrait).ConfigureAwait(false);
        }

        if (path is null && HasTrainerCover(entry.Id))
        {
            path = TrainerCoverPath(entry.Id);
        }

        return path is null ? null : await LoadImageAsync(path, 480).ConfigureAwait(false);
    }

    public async Task<BitmapSource?> GetHeroAsync(TrainerEntry entry)
    {
        if (entry.Meta.SteamAppId is not int appId)
        {
            return null;
        }

        string? path = await _steam.GetArtPathAsync(appId, SteamArtKind.Hero).ConfigureAwait(false);
        return path is null ? null : await LoadImageAsync(path, 1920).ConfigureAwait(false);
    }

    public string TrainerCoverPath(string trainerId) =>
        Path.Combine(_coverDir, Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(trainerId)))[..20] + ".png");

    public bool HasTrainerCover(string trainerId) => File.Exists(TrainerCoverPath(trainerId));

    public void SaveTrainerCover(string trainerId, BitmapSource image) => TrySavePng(image, TrainerCoverPath(trainerId));

    public string ImportCustomArt(string sourcePath, string trainerId)
    {
        string dir = Path.Combine(_dataDir, "art");
        Directory.CreateDirectory(dir);
        string name = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(trainerId)))[..16]
            + "-" + DateTime.UtcNow.Ticks.ToString("x") + Path.GetExtension(sourcePath).ToLowerInvariant();
        File.Copy(sourcePath, Path.Combine(dir, name), overwrite: true);
        return Path.Combine("art", name);
    }

    public void DeleteCustomArt(TrainerEntry entry)
    {
        string? path = ResolveCustomArt(entry);
        if (path is not null && path.StartsWith(Path.Combine(_dataDir, "art"), StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    public static Task<BitmapSource?> LoadImageAsync(string path, int decodeWidth) => Task.Run(() =>
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bmp.DecodePixelWidth = decodeWidth;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            return (BitmapSource?)bmp;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException or UnauthorizedAccessException or ArgumentException)
        {
            Log.Warn($"Could not load image {path}: {ex.Message}");
            return null;
        }
    });

    public async Task<CardArt?> GetIconAsync(TrainerEntry entry)
    {
        await _throttle.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(() => LoadIcon(entry.File)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warn($"Icon load failed for {entry.File.FullPath}: {ex.Message}");
            return null;
        }
        finally
        {
            _throttle.Release();
        }
    }

    private CardArt? LoadIcon(TrainerFile file)
    {
        string cached = Path.Combine(_iconDir, CacheKey(file) + ".png");
        BitmapSource? image = File.Exists(cached) ? LoadPng(cached) : null;
        if (image is null)
        {
            image = IconExtractor.Extract(file.FullPath);
            if (image is null)
            {
                return null;
            }

            TrySavePng(image, cached);
        }

        return new CardArt(image, ColorTools.DominantColor(image), Fingerprint(image));
    }

    private static string Fingerprint(BitmapSource image)
    {
        var small = new TransformedBitmap(image, new ScaleTransform(8.0 / image.PixelWidth, 8.0 / image.PixelHeight));
        var bgra = new FormatConvertedBitmap(small, PixelFormats.Bgra32, null, 0);
        byte[] px = new byte[8 * 8 * 4];
        bgra.CopyPixels(px, 8 * 4, 0);
        var lum = new double[64];
        for (int i = 0; i < 64; i++)
        {
            double a = px[(i * 4) + 3] / 255.0;
            lum[i] = a * ((0.299 * px[(i * 4) + 2]) + (0.587 * px[(i * 4) + 1]) + (0.114 * px[i * 4]));
        }

        double mean = lum.Average();
        ulong bits = 0;
        for (int i = 0; i < 64; i++)
        {
            if (lum[i] > mean)
            {
                bits |= 1UL << i;
            }
        }

        return bits.ToString("X16");
    }

    private static bool SameIcon(string a, string b) =>
        ulong.TryParse(a, System.Globalization.NumberStyles.HexNumber, null, out ulong x)
        && ulong.TryParse(b, System.Globalization.NumberStyles.HexNumber, null, out ulong y)
        && System.Numerics.BitOperations.PopCount(x ^ y) <= 6;

    private static string CacheKey(TrainerFile file)
    {
        string raw = $"{file.FullPath.ToLowerInvariant()}|{file.Size}|{file.ModifiedUtc.Ticks}";
        return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(raw)))[..20];
    }

    private static BitmapSource? LoadPng(string path)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException)
        {
            return null;
        }
    }

    private static void TrySavePng(BitmapSource image, string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var fs = File.Create(path);
            encoder.Save(fs);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"Could not cache icon {path}: {ex.Message}");
        }
    }
}
