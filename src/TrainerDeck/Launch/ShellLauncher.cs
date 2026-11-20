using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TrainerDeck.Launch;

public sealed record ShellResult(int Error, SafeProcessHandle? Process);

public static class ShellLauncher
{
    public static Task<ShellResult> RunAsync(string file, string? arguments, string? directory, string verb, IntPtr owner)
    {
        var tcs = new TaskCompletionSource<ShellResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                tcs.SetResult(Run(file, arguments, directory, verb, owner));
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "ShellExecute",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }

    public static ShellResult Run(string file, string? arguments, string? directory, string verb, IntPtr owner)
    {
        var info = new LaunchNative.ShellExecuteInfo
        {
            cbSize = Marshal.SizeOf<LaunchNative.ShellExecuteInfo>(),
            fMask = LaunchNative.SeeMaskNoCloseProcess | LaunchNative.SeeMaskFlagNoUi | LaunchNative.SeeMaskNoAsync,
            hwnd = owner,
            lpVerb = verb,
            lpFile = file,
            lpParameters = arguments,
            lpDirectory = directory,
            nShow = LaunchNative.SwShowNormal,
        };

        if (!LaunchNative.ShellExecuteExW(ref info))
        {
            return new ShellResult(Marshal.GetLastWin32Error(), null);
        }

        return new ShellResult(0, info.hProcess == IntPtr.Zero ? null : new SafeProcessHandle(info.hProcess, ownsHandle: true));
    }
}
