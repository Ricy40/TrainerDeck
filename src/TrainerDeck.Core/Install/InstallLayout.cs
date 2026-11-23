using System.Security;
using System.Text;

namespace TrainerDeck.Core.Install;

public sealed record InstallInfo(string Version, DateTime InstalledUtc, bool ElevatedTask);

public static class InstallLayout
{
    public const string AppName = "Trainer Deck";
    public const string ExeName = "TrainerDeck.exe";
    public const string TaskName = "Trainer Deck (administrator)";
    public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\TrainerDeck";
    public const string InstallFlag = "--install";
    public const string UninstallFlag = "--uninstall";
    public const string FromTaskFlag = TrainerDeck.Core.Launch.StartupOptions.FromTaskFlag;

    public static string DefaultTargetDirectory(string programFiles) => Path.Combine(programFiles, AppName);

    public static bool IsSetupInvocation(string exePath, IEnumerable<string> args) =>
        args.Any(a => a.Equals(InstallFlag, StringComparison.OrdinalIgnoreCase))
        || Path.GetFileNameWithoutExtension(exePath).Contains("setup", StringComparison.OrdinalIgnoreCase);

    public static bool IsUninstallInvocation(IEnumerable<string> args) =>
        args.Any(a => a.Equals(UninstallFlag, StringComparison.OrdinalIgnoreCase));

    public static bool IsUnder(string path, string directory)
    {
        string p = Path.GetFullPath(path).TrimEnd('\\') + "\\";
        string d = Path.GetFullPath(directory).TrimEnd('\\') + "\\";
        return p.StartsWith(d, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyDictionary<string, object> UninstallValues(string targetDirectory, string version, long sizeKb)
    {
        string exe = Path.Combine(targetDirectory, ExeName);
        return new Dictionary<string, object>
        {
            ["DisplayName"] = AppName,
            ["DisplayVersion"] = version,
            ["Publisher"] = AppName,
            ["InstallLocation"] = targetDirectory,
            ["DisplayIcon"] = $"\"{exe}\",0",
            ["UninstallString"] = $"\"{exe}\" {UninstallFlag}",
            ["QuietUninstallString"] = $"\"{exe}\" {UninstallFlag} --quiet",
            ["EstimatedSize"] = (int)Math.Min(int.MaxValue, sizeKb),
            ["NoModify"] = 1,
            ["NoRepair"] = 1,
        };
    }

    public static string ElevatedTaskXml(string exePath, string userSid)
    {
        static string X(string s) => SecurityElement.Escape(s) ?? "";
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-16\"?>");
        sb.AppendLine("<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">");
        sb.AppendLine("  <RegistrationInfo>");
        sb.AppendLine("    <Description>Starts Trainer Deck with administrator rights without a UAC prompt. Created by the Trainer Deck installer.</Description>");
        sb.AppendLine("  </RegistrationInfo>");
        sb.AppendLine("  <Principals>");
        sb.AppendLine("    <Principal id=\"Author\">");
        sb.AppendLine($"      <UserId>{X(userSid)}</UserId>");
        sb.AppendLine("      <LogonType>InteractiveToken</LogonType>");
        sb.AppendLine("      <RunLevel>HighestAvailable</RunLevel>");
        sb.AppendLine("    </Principal>");
        sb.AppendLine("  </Principals>");
        sb.AppendLine("  <Settings>");
        sb.AppendLine("    <MultipleInstancesPolicy>Parallel</MultipleInstancesPolicy>");
        sb.AppendLine("    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>");
        sb.AppendLine("    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>");
        sb.AppendLine("    <AllowHardTerminate>true</AllowHardTerminate>");
        sb.AppendLine("    <StartWhenAvailable>false</StartWhenAvailable>");
        sb.AppendLine("    <AllowStartOnDemand>true</AllowStartOnDemand>");
        sb.AppendLine("    <Enabled>true</Enabled>");
        sb.AppendLine("    <Hidden>false</Hidden>");
        sb.AppendLine("    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>");
        sb.AppendLine("    <Priority>5</Priority>");
        sb.AppendLine("  </Settings>");
        sb.AppendLine("  <Actions Context=\"Author\">");
        sb.AppendLine("    <Exec>");
        sb.AppendLine($"      <Command>\"{X(exePath)}\"</Command>");
        sb.AppendLine($"      <Arguments>{FromTaskFlag}</Arguments>");
        sb.AppendLine($"      <WorkingDirectory>{X(Path.GetDirectoryName(exePath) ?? "")}</WorkingDirectory>");
        sb.AppendLine("    </Exec>");
        sb.AppendLine("  </Actions>");
        sb.AppendLine("</Task>");
        return sb.ToString();
    }

    public static string UniqueDestination(string folder, string fileName)
    {
        string candidate = Path.Combine(folder, fileName);
        string stem = Path.GetFileNameWithoutExtension(fileName), ext = Path.GetExtension(fileName);
        for (int i = 2; File.Exists(candidate) || Directory.Exists(candidate); i++)
        {
            candidate = Path.Combine(folder, $"{stem} ({i}){ext}");
        }

        return candidate;
    }
}
