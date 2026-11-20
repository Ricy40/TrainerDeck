namespace TrainerDeck.Core.Launch;

public enum LaunchOutcome
{
    Started,
    AlreadyRunning,
    Cancelled,
    Failed,
}

public sealed record LaunchResult(LaunchOutcome Outcome, int ProcessId = 0, int ErrorCode = 0, string? Message = null)
{
    public static LaunchResult Started(int pid) => new(LaunchOutcome.Started, pid);

    public static LaunchResult AlreadyRunning(int pid) => new(LaunchOutcome.AlreadyRunning, pid);

    public static LaunchResult Cancelled() => new(LaunchOutcome.Cancelled, ErrorCode: LaunchErrors.Cancelled);

    public static LaunchResult Failed(int code, string name) => new(LaunchOutcome.Failed, ErrorCode: code, Message: LaunchErrors.Describe(code, name));
}

public static class LaunchErrors
{
    public const int FileNotFound = 2;
    public const int PathNotFound = 3;
    public const int AccessDenied = 5;
    public const int BadExeFormat = 193;
    public const int VirusInfected = 225;
    public const int VirusDeleted = 226;
    public const int DisabledByPolicy = 1260;
    public const int Cancelled = 1223;

    public static string Describe(int code, string name) => code switch
    {
        FileNotFound or PathNotFound => $"{name} is missing. Your antivirus may have quarantined it.",
        VirusInfected or VirusDeleted => $"Windows Security blocked {name}. Add your trainers folder to its exclusions, then try again.",
        AccessDenied => $"Windows denied access to {name}. Check your antivirus or the file's permissions.",
        BadExeFormat or 216 => $"{name} isn't a program this PC can run.",
        DisabledByPolicy => $"{name} is blocked by a system policy.",
        _ => $"Couldn't start {name} (error {code}).",
    };

    public static bool SuggestsMissingFile(int code) =>
        code is FileNotFound or PathNotFound or VirusInfected or VirusDeleted;
}
