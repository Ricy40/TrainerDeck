namespace TrainerDeck.Core.Library;

public static class TrainerId
{
    public static string Create(string relativePath, long size) => $"{NormalizeRelativePath(relativePath)}|{size}";

    public static string NormalizeRelativePath(string relativePath) =>
        relativePath.Replace('\\', '/').TrimStart('/').ToLowerInvariant();
}

public sealed record TrainerFile(
    string Id,
    string FullPath,
    string RootPath,
    string RelativePath,
    long Size,
    DateTime CreatedUtc,
    DateTime ModifiedUtc,
    ParsedTrainerName Parsed)
{
    public string FileName => Path.GetFileName(FullPath);

    public string Directory => Path.GetDirectoryName(FullPath) ?? RootPath;
}

public sealed record ScanResult(
    IReadOnlyList<TrainerFile> Trainers,
    IReadOnlyList<string> PendingZips,
    IReadOnlyList<string> Errors);
