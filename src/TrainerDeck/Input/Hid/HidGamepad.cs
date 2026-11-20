using System.IO;
using Microsoft.Win32.SafeHandles;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Core.Input;
using TrainerDeck.Core.Input.Hid;

namespace TrainerDeck.Input.Hid;


internal sealed class HidGamepad : IDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly SonyModel? _sony;
    private readonly bool _bluetooth;
    private readonly IntPtr _preparsed;
    private readonly HidNative.ValueCaps? _x;
    private readonly HidNative.ValueCaps? _y;
    private readonly HidNative.ValueCaps? _hat;
    private readonly int _reportLength;
    private readonly ushort[] _usageBuffer = new ushort[128];
    private SafeFileHandle? _handle;
    private GamepadState _state;
    private bool _disposed;

    private HidGamepad(string path, string name, SonyModel? sony, bool bluetooth, IntPtr preparsed, HidNative.Caps caps, HidNative.ValueCaps[] values)
    {
        Path = path;
        Name = name;
        _sony = sony;
        _bluetooth = bluetooth;
        _preparsed = preparsed;
        _reportLength = caps.InputReportByteLength;
        _x = Find(values, HidNative.UsageX);
        _y = Find(values, HidNative.UsageY);
        _hat = Find(values, HidNative.UsageHat);
    }

    public string Path { get; }

    public string Name { get; }

    public bool IsConnected { get; private set; } = true;

    public GamepadState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public static HidGamepad? TryOpen(string path)
    {
        if (path.Contains("ig_", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        using SafeFileHandle query = HidNative.OpenForQuery(path);
        if (query.IsInvalid || !HidNative.HidD_GetPreparsedData(query, out IntPtr preparsed))
        {
            return null;
        }

        bool keep = false;
        try
        {
            if (HidNative.HidP_GetCaps(preparsed, out HidNative.Caps caps) != HidNative.HidpStatusSuccess
                || caps.UsagePage != HidNative.GenericDesktopPage
                || (caps.Usage != HidNative.UsageGamepad && caps.Usage != HidNative.UsageJoystick))
            {
                return null;
            }

            var attrs = new HidNative.Attributes { Size = System.Runtime.InteropServices.Marshal.SizeOf<HidNative.Attributes>() };
            HidNative.HidD_GetAttributes(query, ref attrs);
            SonyModel? sony = SonyReportParser.Identify(attrs.VendorId, attrs.ProductId);
            bool bluetooth = path.Contains("{00001124-0000-1000-8000-00805f9b34fb}", StringComparison.OrdinalIgnoreCase)
                || path.Contains("bthenum", StringComparison.OrdinalIgnoreCase)
                || (sony == SonyModel.DualSense && caps.InputReportByteLength > 64);

            ushort count = caps.NumberInputValueCaps;
            var values = new HidNative.ValueCaps[Math.Max((int)count, 1)];
            if (count > 0)
            {
                HidNative.HidP_GetValueCaps(HidNative.ReportType.Input, values, ref count, preparsed);
                Array.Resize(ref values, count);
            }
            else
            {
                values = [];
            }

            string name = HidNative.GetProductName(query) is { Length: > 0 } product
                ? product
                : sony is not null ? sony.ToString()! : $"Gamepad {attrs.VendorId:X4}:{attrs.ProductId:X4}";

            var pad = new HidGamepad(path, name, sony, bluetooth, preparsed, caps, values);
            keep = true; // pad owns preparsed data from here
            if (!pad.Start())
            {
                pad.Dispose();
                return null;
            }

            return pad;
        }
        finally
        {
            if (!keep)
            {
                HidNative.HidD_FreePreparsedData(preparsed);
            }
        }
    }

    private bool Start()
    {
        _handle = HidNative.OpenForRead(Path);
        if (_handle.IsInvalid)
        {
            Log.Warn($"HID: cannot open {Name} for reading.");
            return false;
        }

        _ = Task.Run(ReadLoop);
        return true;
    }

    private async Task ReadLoop()
    {
        var buffer = new byte[Math.Max(_reportLength, 1)];
        try
        {
            using var stream = new FileStream(_handle!, FileAccess.Read, 0, isAsync: true);
            while (!_cts.IsCancellationRequested)
            {
                int read = await stream.ReadAsync(buffer, _cts.Token).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                if (TryParse(buffer, read, out GamepadState state))
                {
                    lock (_gate)
                    {
                        _state = state;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or UnauthorizedAccessException)
        {
            if (!_disposed)
            {
                Log.Info($"HID: {Name} stopped ({ex.Message}).");
            }
        }

        IsConnected = false;
    }

    private bool TryParse(byte[] report, int length, out GamepadState state)
    {
        if (_sony is SonyModel model)
        {
            return SonyReportParser.TryParse(model, report.AsSpan(0, length), _bluetooth, out state);
        }

        uint usageCount = (uint)_usageBuffer.Length;
        var buttons = new List<int>();
        if (HidNative.HidP_GetUsages(HidNative.ReportType.Input, HidNative.ButtonPage, 0, _usageBuffer, ref usageCount, _preparsed, report, (uint)length)
            == HidNative.HidpStatusSuccess)
        {
            for (int i = 0; i < usageCount; i++)
            {
                buttons.Add(_usageBuffer[i]);
            }
        }

        float? x = ReadAxis(_x, report, length);
        float? y = ReadAxis(_y, report, length);
        int? hat = null;
        if (_hat is HidNative.ValueCaps hc && ReadRaw(hc, report, length) is uint raw)
        {
            hat = GenericGamepadMapper.NormalizeHat(raw, hc.LogicalMin, hc.LogicalMax);
        }

        state = GenericGamepadMapper.Map(buttons, x, y, hat);
        return true;
    }

    private float? ReadAxis(HidNative.ValueCaps? caps, byte[] report, int length)
    {
        if (caps is not HidNative.ValueCaps c || ReadRaw(c, report, length) is not uint raw)
        {
            return null;
        }

        long min = c.LogicalMin, max = c.LogicalMax;
        if (max < min && c.BitSize is > 0 and < 32)
        {
            max = (1L << c.BitSize) - 1;
            min = 0;
        }

        return GamepadState.NormalizeAxis(raw, min, max);
    }

    private uint? ReadRaw(HidNative.ValueCaps c, byte[] report, int length) =>
        HidNative.HidP_GetUsageValue(HidNative.ReportType.Input, c.UsagePage, c.LinkCollection, c.UsageMin, out uint value, _preparsed, report, (uint)length)
            == HidNative.HidpStatusSuccess ? value : null;

    private static HidNative.ValueCaps? Find(HidNative.ValueCaps[] values, ushort usage)
    {
        foreach (var v in values)
        {
            if (v.UsagePage == HidNative.GenericDesktopPage && v.IsRange == 0 && v.UsageMin == usage)
            {
                return v;
            }
        }

        return null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        _handle?.Dispose();
        HidNative.HidD_FreePreparsedData(_preparsed);
        _cts.Dispose();
    }
}
