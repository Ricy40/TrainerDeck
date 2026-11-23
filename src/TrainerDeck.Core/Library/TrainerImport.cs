using TrainerDeck.Core.Install;

namespace TrainerDeck.Core.Library;

public sealed record TrainerImportResult(bool Success, string? Destination, string? Error, bool AccessDenied = false);

public static class TrainerImport
{
    public static readonly string[] Extensions = [".exe", ".zip"];

    public static bool IsSupported(string path) =>
        Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    public static TrainerImportResult Import(string source, string destinationFolder)
    {
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
            Directory.CreateDirectory(destinationFolder);
            string target = InstallLayout.UniqueDestination(destinationFolder, Path.GetFileName(source));
            File.Copy(source, target);
            if (!Path.GetExtension(source).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return new TrainerImportResult(true, target, null);
            }

            ZipImportResult zip = ZipImporter.Extract(target, deleteZipAfter: true);
            if (!zip.Success)
            {
                TryDelete(target);
            }

            return zip.Success
                ? new TrainerImportResult(true, zip.TargetFolder, null)
                : new TrainerImportResult(false, null, zip.Error);
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
