using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainerDeck.Core.Input;
using TrainerDeck.Core.Theming;

namespace TrainerDeck.UI.ViewModels;

public sealed partial class AccentSwatch : ObservableObject
{
    public AccentSwatch(Rgb color, bool isCurrent)
    {
        Color = color;
        IsCurrent = isCurrent;
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
        brush.Freeze();
        Brush = brush;
        CheckBrush = Rgb.Contrast(color, new Rgb(0, 0, 0)) >= Rgb.Contrast(color, new Rgb(255, 255, 255)) ? Brushes.Black : Brushes.White;
    }

    public Rgb Color { get; }

    public Brush Brush { get; }

    public Brush CheckBrush { get; }

    public bool IsCurrent { get; }

    [ObservableProperty]
    private bool isSelected;
}

public sealed partial class AccentPickerViewModel : PageViewModel
{
    public const int Columns = 8;

    private readonly MainViewModel _main;
    private readonly Action<string> _save;
    private readonly List<AccentSwatch> _swatches;
    private int _index;

    public AccentPickerViewModel(MainViewModel main, string current, Action<string> save)
    {
        _main = main;
        _save = save;
        Rgb? now = Rgb.TryParse(current, out Rgb c) ? c : null;
        CurrentHex = now?.ToHex() ?? current;
        _swatches = WindowsAccentColors.All.Select(s => new AccentSwatch(s, s == now)).ToList();
        _index = Math.Max(0, _swatches.FindIndex(s => s.IsCurrent));
        _swatches[_index].IsSelected = true;
    }

    public string Title => "Accent colour";

    public string CurrentHex { get; }

    public IReadOnlyList<AccentSwatch> Swatches => _swatches;

    public override IReadOnlyList<FooterHint> Hints =>
        [new(InputAction.Activate, "Choose"), new(InputAction.Favourite, "Custom colour"), new(InputAction.Back, "Cancel")];

    public override bool Handle(InputAction action)
    {
        switch (action)
        {
            case InputAction.Left when _index % Columns > 0:
                Select(_index - 1);
                return true;
            case InputAction.Right when _index % Columns < Columns - 1 && _index + 1 < Swatches.Count:
                Select(_index + 1);
                return true;
            case InputAction.Up when _index >= Columns:
                Select(_index - Columns);
                return true;
            case InputAction.Down when _index + Columns < Swatches.Count:
                Select(_index + Columns);
                return true;
            case InputAction.Left or InputAction.Right or InputAction.Up or InputAction.Down:
                return true;
            case InputAction.Activate:
                Choose(Swatches[_index]);
                return true;
            case InputAction.Favourite:
                EnterCustom();
                return true;
            default:
                return false;
        }
    }

    [RelayCommand]
    private void Choose(AccentSwatch swatch)
    {
        _main.Pop();
        _save(swatch.Color.ToHex());
    }

    [RelayCommand]
    private void EnterCustom()
    {
        _main.Push(new TextEntryViewModel(_main, "Custom accent colour",
            "Type a colour as a hex code, like #0078D4 or 3DDC84. Trainer Deck adjusts the shade if it's hard to read.",
            CurrentHex, text =>
            {
                if (Rgb.TryParse(text.Trim(), out Rgb c))
                {
                    _main.Pop();
                    _save(c.ToHex());
                }
                else
                {
                    _main.ShowMessage($"\"{text.Trim()}\" isn't a colour. Use a hex code like #0078D4.");
                }
            }));
    }

    private void Select(int i)
    {
        _swatches[_index].IsSelected = false;
        _index = i;
        _swatches[_index].IsSelected = true;
    }
}
