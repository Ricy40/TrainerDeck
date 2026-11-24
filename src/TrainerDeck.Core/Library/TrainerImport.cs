using System.Security.Cryptography;
using TrainerDeck.Core.Install;

namespace TrainerDeck.Core.Library;

public sealed record TrainerImportResult(bool Success, string? Destination, string? Error, bool AccessDenied = false, bool Duplicate = false);

public sealed record TrainerImportOptions(bool GameFolder = true, bool DeleteOriginal = false);

public static class TrainerImport
{
    public static readonly string[] Extensions = [".exe", ".zip"];

    public static bool IsSupported(string path) =>
        Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    public const int ExitDuplicate = 2;

    public static TrainerImportResult Import(string source, string destinationFolder, TrainerImportOptions? options = null)
    {
        options ??= new TrainerImportOptions();
        if (!File.Exists(source))
        {
            return new TrainerImportResult(false, null, "The file no longer exists.");
        }

        if (!IsSupported(source))
        {
            return new TrainerImportResult(false, null, "Only .exe and .zip files can be added.");
        }

        try
        {
            bool isZip = Path.GetExtension(source).Equals(".zip", StringComparison.OrdinalIgnoreCase);
            if (!isZip && FindDuplicate(source, destinationFolder) is string existing)
            {
                return new TrainerImportResult(false, existing, "It's already in your trainer folder.", Duplicate: true);
            }

            string folder = !isZip && options.GameFolder ? Path.Combine(destinationFolder, GameFolderName(source)) : destinationFolder;
            Directory.CreateDirectory(folder);
            string target = InstallLayout.UniqueDestination(folder, Path.GetFileName(source));
            File.Copy(source, target);
            TrainerImportResult result;
            if (!isZip)
            {
                result = new TrainerImportResult(true, target, null);
            }
            else
            {
                ZipImportResult zip = ZipImporter.Extract(target, deleteZipAfter: true);
                if (!zip.Success)
                {
                    TryDelete(target);
                }

                result = zip.Success
                    ? new TrainerImportResult(true, zip.TargetFolder, null)
                    : new TrainerImportResult(false, null, zip.Error);
            }

            if (result.Success && options.DeleteOriginal)
            {
                TryDelete(source);
            }

            return result;
        }
        catch (UnauthorizedAccessException ex)
        {
            return new TrainerImportResult(false, null, ex.Message, AccessDenied: true);
        }
        catch (IOException ex)
        {
            return new TrainerImportResult(false, null, ex.Message);
        }
    }

    public static string GameFolderName(string trainerPath)
    {
        string name = FlingNameParser.Parse(trainerPath).GameName;
        char[] invalid = Path.GetInvalidFileNameChars();
        string safe = new string(name.Select(c => invalid.Contains(c) ? ' ' : c).ToArray()).Trim().TrimEnd('.');
        return safe.Length == 0 ? "Other trainers" : safe;
    }

    public static string? FindDuplicate(string source, string trainerFolder)
    {
        if (!Directory.Exists(trainerFolder))
        {
            return null;
        }

        long size = new FileInfo(source).Length;
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        byte[]? sourceHash = null;
        foreach (string candidate in Directory.EnumerateFiles(trainerFolder, "*.exe", options))
        {
            if (new FileInfo(candidate).Length != size
                || string.Equals(Path.GetFullPath(candidate), Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            sourceHash ??= Hash(source);
            if (Hash(candidate).AsSpan().SequenceEqual(sourceHash))
            {
                return candidate;
            }
        }

        return null;
    }

    private static byte[] Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return SHA256.HashData(stream);
    }

    private static void TryDelete(string path)
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
