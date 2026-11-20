using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using TrainerDeck.Core.Diagnostics;

namespace TrainerDeck.Launch;


public sealed class SingleInstance : IDisposable
{
    private const string EventName = @"Local\TrainerDeck.Instance";
    private const int ErrorAlreadyExists = 183;
    private const uint EventModifyState = 0x0002;

    private const string Sddl = "D:(A;;GA;;;AU)(A;;GA;;;SY)S:(ML;;NW;;;ME)";

    private readonly EventWaitHandle _event;
    private RegisteredWaitHandle? _wait;

    private SingleInstance(EventWaitHandle handle) => _event = handle;

    public event Action? ActivationRequested;

    public static bool IsElevated
    {
        get
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public static SingleInstance? TryAcquire()
    {
        (SafeWaitHandle handle, int error) = CreateWithSecurity();
        if (handle.IsInvalid)
        {
            Log.Warn($"Single instance: CreateEvent failed ({error}); assuming another copy is running.");
            SignalExisting();
            return null;
        }

        if (error == ErrorAlreadyExists)
        {
            AllowSetForegroundWindow(-1);
            using var existing = new EventWaitHandle(false, EventResetMode.AutoReset) { SafeWaitHandle = handle };
            existing.Set();
            return null;
        }

        var wait = new EventWaitHandle(false, EventResetMode.AutoReset) { SafeWaitHandle = handle };
        var instance = new SingleInstance(wait);
        instance._wait = ThreadPool.RegisterWaitForSingleObject(wait, (_, _) => instance.ActivationRequested?.Invoke(), null, Timeout.Infinite, executeOnlyOnce: false);
        return instance;
    }

    private static void SignalExisting()
    {
        SafeWaitHandle h = OpenEventW(EventModifyState, false, EventName);
        if (!h.IsInvalid)
        {
            AllowSetForegroundWindow(-1);
            SetEvent(h);
        }

        h.Dispose();
    }

    private static (SafeWaitHandle Handle, int Error) CreateWithSecurity()
    {
        IntPtr sd = IntPtr.Zero;
        try
        {
            if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(Sddl, 1, out sd, IntPtr.Zero))
            {
                SafeWaitHandle plain = CreateEventW(IntPtr.Zero, false, false, EventName);
                return (plain, Marshal.GetLastWin32Error());
            }

            var sa = new SecurityAttributes { nLength = Marshal.SizeOf<SecurityAttributes>(), lpSecurityDescriptor = sd };
            IntPtr psa = Marshal.AllocHGlobal(sa.nLength);
            try
            {
                Marshal.StructureToPtr(sa, psa, false);
                SafeWaitHandle h = CreateEventW(psa, false, false, EventName);
                return (h, Marshal.GetLastWin32Error());
            }
            finally
            {
                Marshal.FreeHGlobal(psa);
            }
        }
        finally
        {
            if (sd != IntPtr.Zero)
            {
                LocalFree(sd);
            }
        }
    }

    public void Dispose()
    {
        _wait?.Unregister(null);
        _wait = null;
        _event.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public int bInheritHandle;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeWaitHandle CreateEventW(IntPtr attributes, bool manualReset, bool initialState, string name);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeWaitHandle OpenEventW(uint access, bool inherit, string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetEvent(SafeWaitHandle handle);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string sddl, uint revision, out IntPtr descriptor, IntPtr size);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr mem);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);
}
