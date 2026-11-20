namespace TrainerDeck.Core.Library;


public sealed class TrainerScanner
{
    private const int MaxDepth = 8;
    private readonly IgnoreRules _rules;

    public TrainerScanner(IgnoreRules rules) => _rules = rules;

    public ScanResult Scan(IEnumerable<string> roots, CancellationToken cancellationToken = default)
    {
        var trainers = new List<TrainerFile>();
        var zips = new List<string>();
        var errors = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive,
            MaxRecursionDepth = MaxDepth,
            AttributesToSkip = FileAttributes.System | FileAttributes.Hidden,
        };

        foreach (string rawRoot in roots.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string root;
            try
            {
                root = Path.GetFullPath(rawRoot);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                errors.Add($"Invalid folder '{rawRoot}': {ex.Message}");
                continue;
            }

            if (!Directory.Exists(root))
            {
                errors.Add($"Folder not found: {root}");
                continue;
            }

            try
            {
                foreach (string path in Directory.EnumerateFiles(root, "*.exe", options))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!seen.Add(path))
                    {
                        continue; // overlapping roots
                    }

                    string rel = Path.GetRelativePath(root, path);
                    if (_rules.IsIgnored(rel))
                    {
                        continue;
                    }

                    try
                    {
                        var info = new FileInfo(path);
                        trainers.Add(new TrainerFile(
                            TrainerId.Create(rel, info.Length),
                            path,
                            root,
                            rel,
                            info.Length,
                            info.CreationTimeUtc,
                            info.LastWriteTimeUtc,
                            FlingNameParser.Parse(path, root)));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        errors.Add($"{path}: {ex.Message}");
                    }
                }

                foreach (string zip in Directory.EnumerateFiles(root, "*.zip", options))
                {
                    if (!Directory.Exists(ZipImporter.DefaultTargetFolder(zip)))
                    {
                        zips.Add(zip);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add($"{root}: {ex.Message}");
            }
        }

        trainers.Sort((a, b) => string.Compare(a.Parsed.SortKey, b.Parsed.SortKey, StringComparison.OrdinalIgnoreCase));
        return new ScanResult(trainers, zips, errors);
    }
}
