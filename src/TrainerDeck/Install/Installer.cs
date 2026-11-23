using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Install;
using TrainerDeck.Core.Settings;

namespace TrainerDeck.Install;

public sealed record InstallOptions(
    string TargetDirectory,
    bool ElevatedTask,
    bool DesktopShortcut,
    string UserSid,
    bool SystemIntegration = true,
    bool GameBarWidget = false);


public static class Installer
{
    public static string Version => typeof(Installer).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public static string StartMenuShortcut =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), InstallLayout.AppName + ".lnk");

    public static string DesktopShortcut =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), InstallLayout.AppName + ".lnk");

    public static string? ExistingInstallDirectory()
    {
        using RegistryKey? key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(InstallLayout.UninstallKey);
        return key?.GetValue("InstallLocation") as string is { Length: > 0 } dir && Directory.Exists(dir) ? dir : null;
    }

    public static string CurrentUserSid()
    {
        using WindowsIdentity id = WindowsIdentity.GetCurrent();
        return id.User?.Value ?? "";
    }

    // Returns a note for the user when an optional part (the Game Bar widget) couldn't be installed.
    public static string? Install(InstallOptions o, Action<string> progress)
    {
        string source = Environment.ProcessPath ?? throw new InvalidOperationException("Can't find the setup file.");
        string target = Path.GetFullPath(o.TargetDirectory);
        string exe = Path.Combine(target, InstallLayout.ExeName);
        Log.Info($"Install: {source} -> {target} (task: {o.ElevatedTask}, desktop: {o.DesktopShortcut})");

        progress("Closing Trainer Deck if it's running…");
        StopRunningCopies(exe);

        progress("Copying files…");
        Directory.CreateDirectory(target);
        CopyWithRetry(source, exe);
        Directory.CreateDirectory(Path.Combine(target, "Trainers"));
        var info = new InstallInfo(Version, DateTime.UtcNow, o.ElevatedTask);
        File.WriteAllText(Path.Combine(target, PortablePaths.InstallMarkerFile), JsonSerializer.Serialize(info));

        if (!o.SystemIntegration)
        {
            return null;
        }

        progress("Adding shortcuts…");
        ShellLink.Create(StartMenuShortcut, exe, description: "Launch FLiNG trainers with a controller");
        if (o.DesktopShortcut)
        {
            ShellLink.Create(DesktopShortcut, exe, description: "Launch FLiNG trainers with a controller");
        }
        else
        {
            TryDelete(DesktopShortcut);
        }

        progress("Registering with Windows…");
        long sizeKb = new FileInfo(exe).Length / 1024;
        using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).CreateSubKey(InstallLayout.UninstallKey))
        {
            foreach (var (name, value) in InstallLayout.UninstallValues(target, Version, sizeKb))
            {
                key.SetValue(name, value, value is int ? RegistryValueKind.DWord : RegistryValueKind.String);
            }
        }

        WidgetInstaller.RegisterProtocol(exe);

        if (o.ElevatedTask)
        {
            progress("Setting up start without a UAC prompt…");
            CreateTask(exe, o.UserSid);
        }
        else
        {
            DeleteTask();
        }

        string? note = null;
        if (o.GameBarWidget && WidgetInstaller.Available)
        {
            progress("Installing the Xbox Game Bar widget…");
            try
            {
                WidgetInstaller.Install(o.UserSid);
            }
            catch (Exception ex)
            {
                Log.Error("Game Bar widget install failed", ex);
                note = "The Xbox Game Bar widget couldn't be installed: " + ex.Message;
            }
        }
        else if (!o.GameBarWidget && WidgetInstaller.Available)
        {
            WidgetInstaller.Uninstall();
        }

        Log.Info("Install finished.");
        return note;
    }

    public static void Uninstall(string target, bool keepTrainers)
    {
        string exe = Path.Combine(target, InstallLayout.ExeName);
        Log.Info($"Uninstall from {target} (keep trainers: {keepTrainers})");
        StopRunningCopies(exe);
        DeleteTask();
        RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).DeleteSubKeyTree(InstallLayout.UninstallKey, throwOnMissingSubKey: false);
        TryDelete(StartMenuShortcut);
        TryDelete(DesktopShortcut);
        TryDelete(Path.Combine(target, PortablePaths.InstallMarkerFile));
        WidgetInstaller.UnregisterProtocol();
        WidgetInstaller.Uninstall();

        string tail = keepTrainers
            ? $"del /f /q \"{exe}\" & rmdir \"{target}\""
            : $"rmdir /s /q \"{target}\"";
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c ping 127.0.0.1 -n 3 > nul & {tail}")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetTempPath(),
        });
    }

    public static bool TaskExists() => RunSchtasks($"/Query /TN \"{InstallLayout.TaskName}\"") == 0;

    public static bool StartViaTask() => RunSchtasks($"/Run /TN \"{InstallLayout.TaskName}\"") == 0;

    private static void CreateTask(string exe, string userSid)
    {
        string xmlPath = Path.Combine(Path.GetTempPath(), $"trainerdeck-task-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(xmlPath, InstallLayout.ElevatedTaskXml(exe, userSid), Encoding.Unicode);
            int code = RunSchtasks($"/Create /TN \"{InstallLayout.TaskName}\" /XML \"{xmlPath}\" /F");
            if (code != 0)
            {
                throw new InvalidOperationException($"Task Scheduler refused the task (code {code}).");
            }
        }
        finally
        {
            TryDelete(xmlPath);
        }
    }

    private static void DeleteTask() => RunSchtasks($"/Delete /TN \"{InstallLayout.TaskName}\" /F");

    private static int RunSchtasks(string args)
    {
        try
        {
            using Process? p = Process.Start(new ProcessStartInfo("schtasks.exe", args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (p is null)
            {
                return -1;
            }

            p.StandardOutput.ReadToEnd();
            string err = p.StandardError.ReadToEnd();
            p.WaitForExit(15000);
            if (p.ExitCode != 0 && !args.StartsWith("/Query", StringComparison.Ordinal) && !args.StartsWith("/Delete", StringComparison.Ordinal))
            {
                Log.Warn($"schtasks {args.Split(' ')[0]} failed ({p.ExitCode}): {err.Trim()}");
            }

            return p.ExitCode;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Log.Warn($"schtasks failed: {ex.Message}");
            return -1;
        }
    }

    private static void StopRunningCopies(string exe)
    {
        foreach (Process p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(InstallLayout.ExeName)))
        {
            using (p)
            {
                try
                {
                    if (p.Id != Environment.ProcessId && string.Equals(p.MainModule?.FileName, exe, StringComparison.OrdinalIgnoreCase))
                    {
                        p.CloseMainWindow();
                        if (!p.WaitForExit(3000))
                        {
                            p.Kill();
                        }
                    }
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
                {
                }
            }
        }
    }

    private static void CopyWithRetry(string source, string dest)
    {
        if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                File.Copy(source, dest, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < 10)
            {
                Thread.Sleep(500);
            }
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
