using System.IO.Compression;

namespace TrainerDeck.Core.Library;

public sealed record ZipImportResult(bool Success, string? TargetFolder, string? Error);


public static class ZipImporter
{
    public static string DefaultTargetFolder(string zipPath) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(zipPath))!, Path.GetFileNameWithoutExtension(zipPath).Trim());

    public static ZipImportResult Extract(string zipPath, bool deleteZipAfter = false)
    {
        string? target = null;
        try
        {
            target = UniqueFolder(DefaultTargetFolder(zipPath));
            string targetFull = Path.GetFullPath(target) + Path.DirectorySeparatorChar;

            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                Directory.CreateDirectory(target);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string dest = Path.GetFullPath(Path.Combine(target, entry.FullName));

                    if (!dest.StartsWith(targetFull, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException($"Archive entry escapes target folder: {entry.FullName}");
                    }

                    if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                    {
                        Directory.CreateDirectory(dest);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    entry.ExtractToFile(dest, overwrite: false);
                }
            }

            if (deleteZipAfter)
            {
                File.Delete(zipPath);
            }

            return new ZipImportResult(true, target, null);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException)
        {
            TryCleanup(target);
            return new ZipImportResult(false, null, ex.Message);
        }
    }

    private static string UniqueFolder(string folder)
    {
        if (!Directory.Exists(folder) && !File.Exists(folder))
        {
            return folder;
        }

        for (int i = 2; ; i++)
        {
            string candidate = $"{folder} ({i})";
            if (!Directory.Exists(candidate) && !File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private static void TryCleanup(string? folder)
    {
        try
        {
            if (folder is not null && Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
