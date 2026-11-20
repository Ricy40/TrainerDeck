using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Library;

namespace TrainerDeck.Art;

public sealed record CardArt(BitmapSource Image, Color Accent);


public sealed class ArtService
{
    private readonly string _iconDir;
    private readonly SemaphoreSlim _throttle = new(4);

    public ArtService(string cacheDirectory)
    {
        _iconDir = Path.Combine(cacheDirectory, "icons");
    }

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

        return new CardArt(image, ColorTools.DominantColor(image));
    }

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
