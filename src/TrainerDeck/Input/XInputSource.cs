using System.Runtime.InteropServices;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Input;

namespace TrainerDeck.Input;


public sealed class XInputSource : IGamepadSource
{
    private const int MaxControllers = 4;
    private const uint ErrorSuccess = 0;
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(1);

    private readonly bool[] _connected = new bool[MaxControllers];
    private TimeSpan _nextProbe;
    private bool _available = true;

    public event Action<string, bool>? ConnectionChanged;

    public void Poll(List<GamepadState> states, TimeSpan now)
    {
        if (!_available)
        {
            return;
        }

        bool probe = now >= _nextProbe;
        if (probe)
        {
            _nextProbe = now + ProbeInterval;
        }

        for (uint i = 0; i < MaxControllers; i++)
        {
            if (!_connected[i] && !probe)
            {
                continue;
            }

            uint result;
            XInputState raw;
            try
            {
                result = XInputGetState(i, out raw);
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                Log.Warn($"XInput unavailable: {ex.Message}");
                _available = false;
                return;
            }

            bool connected = result == ErrorSuccess;
            if (connected != _connected[i])
            {
                _connected[i] = connected;
                Log.Info($"XInput controller {i + 1} {(connected ? "connected" : "disconnected")}");
                ConnectionChanged?.Invoke("Xbox controller", connected);
            }

            if (connected)
            {
                states.Add(ToState(raw.Gamepad));
            }
        }
    }

    public void Suspend()
    {
    }

    public void Resume() => _nextProbe = TimeSpan.Zero;

    public void Dispose()
    {
    }

    private static GamepadState ToState(XInputGamepad g) => new(
        (GamepadButtons)(g.Buttons & 0xF3FF),
        g.ThumbLX / 32767f,
        g.ThumbLY / 32767f,
        g.LeftTrigger / 255f,
        g.RightTrigger / 255f,
        ControllerKind.Xbox);

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState(uint userIndex, out XInputState state);
}
