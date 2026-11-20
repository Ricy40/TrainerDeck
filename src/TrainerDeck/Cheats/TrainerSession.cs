using System.Text.RegularExpressions;
using System.Windows;
using Condition = System.Windows.Automation.Condition;
using System.Windows.Automation;
using TrainerDeck.Core.Cheats;
using TrainerDeck.Core.Diagnostics;
using TrainerDeck.Launch;

namespace TrainerDeck.Cheats;

public enum SessionStatus
{
    Connecting,
    Connected,
    HotkeysOnly,
    NeedsAdministrator,
    Unreadable,
}

public sealed record CheatState(bool? IsOn, string? Value);

public sealed record SessionSnapshot(SessionStatus Status, IReadOnlyList<CheatOption> Options, bool? GameDetected);

public sealed partial class TrainerSession
{
    private static readonly AutomationProperty IdProp = AutomationElement.AutomationIdProperty;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<Live> _live = new();
    private AutomationElement? _root;

    public TrainerSession(int processId) => ProcessId = processId;

    public int ProcessId { get; }

    public SessionStatus Status { get; private set; } = SessionStatus.Connecting;

    public IReadOnlyList<CheatOption> Options => _live.Select(l => l.Option).ToList();

    public Task<SessionSnapshot> ConnectAsync(bool elevated, TimeSpan timeout) => Run(() =>
    {
        if (!elevated && LaunchNative.IsProcessElevated(ProcessId) == true)
        {
            Status = SessionStatus.NeedsAdministrator;
            Log.Info($"Cheats: pid {ProcessId} runs as administrator; Trainer Deck doesn't, so only hotkeys are available.");
            return new SessionSnapshot(Status, [], null);
        }

        DateTime until = DateTime.UtcNow + timeout;
        while (true)
        {
            IntPtr hwnd = LaunchNative.TopLevelWindows(ProcessId).FirstOrDefault();
            if (hwnd != IntPtr.Zero)
            {
                try
                {
                    _root = AutomationElement.FromHandle(hwnd);
                    ReadModern();
                    if (_live.Count == 0)
                    {
                        ReadLegacy();
                    }
                }
                catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or ArgumentException or System.Runtime.InteropServices.COMException)
                {
                    Log.Warn($"Cheats: reading pid {ProcessId} failed: {ex.Message}");
                }
            }

            if (_live.Count > 0 || DateTime.UtcNow >= until)
            {
                break;
            }

            Thread.Sleep(500);
        }

        if (_live.Count == 0)
        {
            Status = SessionStatus.Unreadable;
        }

        Log.Info($"Cheats: pid {ProcessId} {Status}, {_live.Count} option(s).");
        return new SessionSnapshot(Status, Options, GameDetected());
    });

    public Task<(IReadOnlyList<CheatState> States, bool? Game)> ReadStatesAsync() => Run(() =>
    {
        var states = new List<CheatState>(_live.Count);
        foreach (Live l in _live)
        {
            states.Add(new CheatState(ReadToggle(l.Toggle), ReadValue(l)));
        }

        return ((IReadOnlyList<CheatState>)states, GameDetected());
    });

    public Task<bool> ToggleAsync(int index) => Run(() =>
    {
        if (Status != SessionStatus.Connected || Find(index)?.Toggle is not AutomationElement toggle)
        {
            return false;
        }

        if (toggle.TryGetCurrentPattern(TogglePattern.Pattern, out object p))
        {
            ((TogglePattern)p).Toggle();
            return true;
        }

        if (toggle.TryGetCurrentPattern(InvokePattern.Pattern, out object i))
        {
            ((InvokePattern)i).Invoke();
            return true;
        }

        return false;
    });

    public Task<bool> SetValueAsync(int index, string value) => Run(() =>
    {
        if (Find(index)?.ValueBox is not AutomationElement box || !box.TryGetCurrentPattern(ValuePattern.Pattern, out object p))
        {
            return false;
        }

        var vp = (ValuePattern)p;
        if (vp.Current.IsReadOnly)
        {
            return false;
        }

        vp.SetValue(value);
        return true;
    });

    public Task<IReadOnlyList<string>> ReadChoicesAsync(int index) => Run(() =>
    {
        if (Find(index)?.Combo is not AutomationElement combo)
        {
            return (IReadOnlyList<string>)[];
        }

        bool expanded = false;
        if (combo.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out object ec))
        {
            ((ExpandCollapsePattern)ec).Expand();
            expanded = true;
            Thread.Sleep(150);
        }

        List<string> items = combo.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem))
            .Cast<AutomationElement>()
            .Select(ItemText)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        if (expanded)
        {
            ((ExpandCollapsePattern)ec).Collapse();
        }

        return (IReadOnlyList<string>)items;
    });

    public Task<bool> SelectChoiceAsync(int index, string choice) => Run(() =>
    {
        if (Find(index)?.Combo is not AutomationElement combo)
        {
            return false;
        }

        if (combo.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out object ec))
        {
            ((ExpandCollapsePattern)ec).Expand();
            Thread.Sleep(150);
        }

        AutomationElement? item = combo.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem))
            .Cast<AutomationElement>()
            .FirstOrDefault(i => ItemText(i) == choice);
        bool ok = false;
        if (item is not null && item.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object sel))
        {
            ((SelectionItemPattern)sel).Select();
            ok = true;
        }

        if (combo.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out object ec2))
        {
            ((ExpandCollapsePattern)ec2).Collapse();
        }

        return ok;
    });

    private void ReadModern()
    {
        _live.Clear();
        var cond = new PropertyCondition(AutomationElement.ClassNameProperty, "CheatOptionToolTip");
        foreach (AutomationElement e in _root!.FindAll(TreeScope.Descendants, new NotCondition(cond)))
        {
            string cls = e.Current.ClassName;
            if (!cls.StartsWith("CheatOption", StringComparison.Ordinal))
            {
                continue;
            }

            AutomationElementCollection parts = e.FindAll(TreeScope.Descendants, Condition.TrueCondition);
            AutomationElement? Part(string id, string? className = null) => parts.Cast<AutomationElement>()
                .FirstOrDefault(p => p.Current.AutomationId == id && (className is null || p.Current.ClassName == className));

            AutomationElement? valueBox = Part("m_textbox", "TextBox");
            AutomationElement? combo = Part("m_box");
            CheatKind kind = CheatOption.KindFromClass(cls, valueBox is not null);
            string name = (kind == CheatKind.Section ? Part("m_label") : Part("m_label_option_name"))?.Current.Name ?? "";
            if (name.Length == 0)
            {
                continue;
            }

            var option = new CheatOption(_live.Count, Tidy(name), Part("m_label_hotkey")?.Current.Name, kind);
            _live.Add(new Live(option, Part("m_toggle_button"), valueBox, combo));
        }

        if (_live.Count > 0)
        {
            Status = SessionStatus.Connected;
        }
    }

    public Task<System.Windows.Media.Imaging.BitmapSource?> CaptureCoverAsync() => Run(() =>
    {
        IntPtr hwnd = LaunchNative.TopLevelWindows(ProcessId).FirstOrDefault();
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        _root ??= AutomationElement.FromHandle(hwnd);
        Rect window = _root.Current.BoundingRectangle;
        AutomationElement? cover = _root.FindFirst(TreeScope.Descendants, new PropertyCondition(IdProp, "m_picturebox_gamecover"));
        Rect region = cover?.Current.BoundingRectangle ?? Rect.Empty;

        if (region.IsEmpty && Status == SessionStatus.HotkeysOnly)
        {
            region = _root.FindAll(TreeScope.Descendants, Condition.TrueCondition)
                .Cast<AutomationElement>()
                .Select(e => (e.Current.Name, e.Current.BoundingRectangle))
                .Where(e => string.IsNullOrEmpty(e.Name) && !e.BoundingRectangle.IsEmpty
                    && e.BoundingRectangle.Width is >= 150 and <= 420 && e.BoundingRectangle.Height is >= 150 and <= 520
                    && e.BoundingRectangle.Right <= window.X + (window.Width / 2)
                    && e.BoundingRectangle.Height >= e.BoundingRectangle.Width * 0.9)
                .OrderByDescending(e => e.BoundingRectangle.Width * e.BoundingRectangle.Height)
                .Select(e => e.BoundingRectangle)
                .FirstOrDefault();
        }

        return region.IsEmpty || region.Width < 64 ? null : WindowCapture.CaptureRegion(hwnd, region);
    });

    private void ReadLegacy()
    {
        var labels = new List<TextLabel>();
        foreach (AutomationElement e in _root!.FindAll(TreeScope.Descendants, Condition.TrueCondition))
        {
            AutomationElement.AutomationElementInformation c = e.Current;
            if (!string.IsNullOrWhiteSpace(c.Name) && !c.BoundingRectangle.IsEmpty)
            {
                Rect r = c.BoundingRectangle;
                labels.Add(new TextLabel(c.Name, r.X, r.Y, r.Width, r.Height));
            }
        }

        foreach (CheatOption o in LegacyLayout.Build(labels))
        {
            _live.Add(new Live(o with { Name = Tidy(o.Name) }, null, null, null));
        }

        if (_live.Count > 0)
        {
            Status = SessionStatus.HotkeysOnly;
        }
    }

    private bool? GameDetected()
    {
        try
        {
            AutomationElement? label = _root?.FindFirst(TreeScope.Descendants, new PropertyCondition(IdProp, "m_label_processid"))
                ?? _root?.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Process ID: "));
            if (label is null)
            {
                return null;
            }

            return Digits().IsMatch(label.Current.Name);
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
    }

    private static bool? ReadToggle(AutomationElement? toggle)
    {
        try
        {
            if (toggle is not null && toggle.TryGetCurrentPattern(TogglePattern.Pattern, out object p))
            {
                return ((TogglePattern)p).Current.ToggleState == ToggleState.On;
            }
        }
        catch (ElementNotAvailableException)
        {
        }

        return null;
    }

    private static string? ReadValue(Live l)
    {
        try
        {
            AutomationElement? source = l.Combo ?? l.ValueBox;
            if (source is not null && source.TryGetCurrentPattern(ValuePattern.Pattern, out object p))
            {
                return ((ValuePattern)p).Current.Value;
            }
        }
        catch (ElementNotAvailableException)
        {
        }

        return null;
    }

    private static string ItemText(AutomationElement item)
    {
        string name = item.Current.Name;
        if (name.Length > 0 && !name.StartsWith("System.", StringComparison.Ordinal))
        {
            return name;
        }

        AutomationElement? text = item.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));
        return text?.Current.Name ?? name;
    }

    private static string Tidy(string name)
    {
        name = name.Trim();
        if (name.Any(char.IsLower))
        {
            return name;
        }

        var ti = System.Globalization.CultureInfo.InvariantCulture.TextInfo;
        string titled = ti.ToTitleCase(name.ToLowerInvariant());
        return KeepUpper().Replace(titled, m => m.Value.ToUpperInvariant());
    }

    private Live? Find(int index) => index >= 0 && index < _live.Count ? _live[index] : null;

    private async Task<T> Run<T>(Func<T> work)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(work).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            Log.Warn($"Cheats: pid {ProcessId}: {ex.Message}");
            return default!;
        }
        finally
        {
            _gate.Release();
        }
    }

    [GeneratedRegex(@"\d")]
    private static partial Regex Digits();

    [GeneratedRegex(@"\b(Hp|Fp|Xp|Sp|Mp|Ai|Npc|Ui|Ok|Dlc|\d+[Xx])\b")]
    private static partial Regex KeepUpper();

    private sealed record Live(CheatOption Option, AutomationElement? Toggle, AutomationElement? ValueBox, AutomationElement? Combo);
}
