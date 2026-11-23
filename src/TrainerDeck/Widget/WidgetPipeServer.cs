using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Widget;

namespace TrainerDeck.Widget;


internal sealed class WidgetPipeServer : IDisposable
{
    private const int MaxInstances = 4;

    private readonly Func<WidgetRequest, Task<WidgetState>> _handle;
    private readonly CancellationTokenSource _stop = new();
    private readonly string _sddl;

    public WidgetPipeServer(Func<WidgetRequest, Task<WidgetState>> handle)
    {
        _handle = handle;
        _sddl = BuildSddl();
    }

    public void Start() => _ = Task.Run(AcceptLoop);

    private async Task AcceptLoop()
    {
        while (!_stop.IsCancellationRequested)
        {
            NamedPipeServerStream pipe;
            try
            {
                pipe = CreateInstance();
            }
            catch (IOException ex)
            {
                Log.Warn($"Widget pipe unavailable: {ex.Message}");
                return;
            }

            try
            {
                await pipe.WaitForConnectionAsync(_stop.Token);
                _ = Task.Run(() => Serve(pipe));
            }
            catch (OperationCanceledException)
            {
                await pipe.DisposeAsync();
                return;
            }
            catch (IOException ex)
            {
                Log.Warn($"Widget pipe connection failed: {ex.Message}");
                await pipe.DisposeAsync();
            }
        }
    }

    private async Task Serve(NamedPipeServerStream pipe)
    {
        await using (pipe)
        {
            var utf8 = new UTF8Encoding(false);
            using var reader = new StreamReader(pipe, utf8, false, 4096, leaveOpen: true);
            await using var writer = new StreamWriter(pipe, utf8, 4096, leaveOpen: true) { AutoFlush = true, NewLine = "\n" };
            try
            {
                while (!_stop.IsCancellationRequested && pipe.IsConnected)
                {
                    string? line = await reader.ReadLineAsync(_stop.Token);
                    if (line is null)
                    {
                        return;
                    }

                    WidgetRequest request = WidgetProtocol.ParseRequest(line) ?? new WidgetRequest(WidgetOps.State);
                    WidgetState state = await _handle(request);
                    await writer.WriteLineAsync(WidgetProtocol.Serialize(state));
                }
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
            {
            }
        }
    }

    private NamedPipeServerStream CreateInstance()
    {
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(_sddl, 1, out IntPtr descriptor, IntPtr.Zero))
        {
            throw new IOException($"Couldn't build the pipe's security descriptor ({Marshal.GetLastWin32Error()}).");
        }

        try
        {
            var attributes = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), Descriptor = descriptor };
            SafePipeHandle handle = CreateNamedPipeW(@"\\.\pipe\" + WidgetProtocol.PipeName,
                PipeAccessDuplex | FileFlagOverlapped, PipeRejectRemoteClients, MaxInstances, 16384, 4096, 0, ref attributes);
            if (handle.IsInvalid)
            {
                throw new IOException($"Couldn't create the widget pipe ({Marshal.GetLastWin32Error()}).");
            }

            return new NamedPipeServerStream(PipeDirection.InOut, isAsync: true, isConnected: false, handle);
        }
        finally
        {
            LocalFree(descriptor);
        }
    }

    private static string BuildSddl()
    {
        string user = WindowsIdentity.GetCurrent().User?.Value ?? "IU";
        var dacl = new StringBuilder($"D:(A;;GA;;;SY)(A;;GA;;;BA)(A;;GA;;;{user})(A;;GRGW;;;IU)");
        if (WidgetSid() is string widget)
        {
            dacl.Append($"(A;;GRGW;;;{widget})");
        }

        return dacl + "S:(ML;;NW;;;LW)";
    }

    private static string? WidgetSid()
    {
        if (DeriveAppContainerSidFromAppContainerName(PackageIdentity.WidgetFamilyName.ToLowerInvariant(), out IntPtr sid) != 0)
        {
            return null;
        }

        try
        {
            return ConvertSidToStringSidW(sid, out IntPtr text) ? TakeString(text) : null;
        }
        finally
        {
            FreeSid(sid);
        }
    }

    private static string? TakeString(IntPtr text)
    {
        try
        {
            return Marshal.PtrToStringUni(text);
        }
        finally
        {
            LocalFree(text);
        }
    }

    public void Dispose() => _stop.Cancel();

    private const uint PipeAccessDuplex = 0x00000003;
    private const uint FileFlagOverlapped = 0x40000000;
    private const uint PipeRejectRemoteClients = 0x00000008;

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public IntPtr Descriptor;
        public int InheritHandle;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafePipeHandle CreateNamedPipeW(string name, uint openMode, uint pipeMode, uint maxInstances,
        uint outBufferSize, uint inBufferSize, uint defaultTimeout, ref SecurityAttributes attributes);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string sddl, uint revision, out IntPtr descriptor, IntPtr size);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertSidToStringSidW(IntPtr sid, out IntPtr text);

    [DllImport("userenv.dll", CharSet = CharSet.Unicode)]
    private static extern int DeriveAppContainerSidFromAppContainerName(string name, out IntPtr sid);

    [DllImport("advapi32.dll")]
    private static extern IntPtr FreeSid(IntPtr sid);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
