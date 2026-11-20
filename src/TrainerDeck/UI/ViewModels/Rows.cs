using CommunityToolkit.Mvvm.ComponentModel;

namespace TrainerDeck.UI.ViewModels;

public abstract partial class RowViewModel : ObservableObject
{
    [ObservableProperty]
    private string label = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetail))]
    private string? detail;

    [ObservableProperty]
    private string? glyph;

    [ObservableProperty]
    private bool isEnabled = true;

    public bool HasDetail => !string.IsNullOrEmpty(Detail);

    public virtual bool IsSelectable => IsEnabled;

    public virtual string ActivateLabel => "Select";

    public virtual string? SecondaryLabel => null;

    public virtual bool CanAdjust => false;

    public virtual void Activate()
    {
    }

    public virtual void Adjust(int delta)
    {
    }

    public virtual void Secondary()
    {
    }
}

public sealed class HeaderRow : RowViewModel
{
    public HeaderRow(string label) => Label = label;

    public override bool IsSelectable => false;
}

public sealed class ActionRow : RowViewModel
{
    private readonly Action _activate;
    private readonly Action? _secondary;
    private readonly string _activateLabel;
    private readonly string? _secondaryLabel;

    public ActionRow(string label, string? glyph, Action activate, string activateLabel = "Select", string? detail = null,
        Action? secondary = null, string? secondaryLabel = null)
    {
        Label = label;
        Glyph = glyph;
        Detail = detail;
        _activate = activate;
        _activateLabel = activateLabel;
        _secondary = secondary;
        _secondaryLabel = secondary is null ? null : secondaryLabel;
    }

    public bool IsDanger { get; init; }

    public override string ActivateLabel => _activateLabel;

    public override string? SecondaryLabel => _secondaryLabel;

    public override void Activate() => _activate();

    public override void Secondary() => _secondary?.Invoke();
}

public sealed partial class ToggleRow : RowViewModel
{
    private readonly Action<bool> _changed;

    public ToggleRow(string label, bool value, Action<bool> changed, string? detail = null)
    {
        Label = label;
        Detail = detail;
        this.value = value;
        _changed = changed;
    }

    [ObservableProperty]
    private bool value;

    public override string ActivateLabel => Value ? "Turn off" : "Turn on";

    public override bool CanAdjust => true;

    public override void Activate() => Set(!Value);

    public override void Adjust(int delta) => Set(delta > 0);

    private void Set(bool v)
    {
        if (v == Value)
        {
            return;
        }

        Value = v;
        _changed(v);
    }
}

public sealed partial class ChoiceRow : RowViewModel
{
    private readonly string[] _options;
    private readonly Action<int> _changed;

    public ChoiceRow(string label, string[] options, int index, Action<int> changed, string? detail = null)
    {
        Label = label;
        Detail = detail;
        _options = options;
        this.index = Math.Clamp(index, 0, options.Length - 1);
        _changed = changed;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValueText))]
    private int index;

    public string ValueText => _options[Index];

    public override string ActivateLabel => "Change";

    public override bool CanAdjust => true;

    public override void Activate() => Set((Index + 1) % _options.Length);

    public override void Adjust(int delta) => Set(Math.Clamp(Index + delta, 0, _options.Length - 1));

    private void Set(int i)
    {
        if (i == Index)
        {
            return;
        }

        Index = i;
        _changed(i);
    }
}
