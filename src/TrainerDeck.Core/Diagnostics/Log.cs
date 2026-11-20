namespace TrainerDeck.Core.Diagnostics;

public static class Log
{
    private static readonly object Gate = new();
    private static string? _path;

    internal static long MaxBytes { get; set; } = 1_000_000;

    internal static int KeepFiles { get; set; } = 3;

    public static string? FilePath => _path;

    public static void Initialize(string path)
    {
        lock (Gate)
        {
            _path = path;
        }

        Info($"=== Trainer Deck {typeof(Log).Assembly.GetName().Version} starting, pid {Environment.ProcessId}, {Environment.OSVersion} ===");
    }

    public static void Info(string message) => Write("INF", message);

    public static void Warn(string message) => Write("WRN", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERR", ex is null ? message : $"{message}{Environment.NewLine}{ex}");

    private static void Write(string level, string message)
    {
        lock (Gate)
        {
            if (_path is null)
            {
                return;
            }

            try
            {
                RollIfNeeded(_path);
                File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] [{Environment.CurrentManagedThreadId}] {message}{Environment.NewLine}");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Logging must never take the app down.
            }
        }
    }

    private static void RollIfNeeded(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < MaxBytes)
        {
            return;
        }

        string Rolled(int i) => Path.ChangeExtension(path, $"{i}.log");

        File.Delete(Rolled(KeepFiles));
        for (int i = KeepFiles - 1; i >= 1; i--)
        {
            if (File.Exists(Rolled(i)))
            {
                File.Move(Rolled(i), Rolled(i + 1));
            }
        }

        File.Move(path, Rolled(1));
    }
}
