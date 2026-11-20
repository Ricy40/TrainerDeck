using CommunityToolkit.Mvvm.ComponentModel;
using TrainerDeck.Core.Input;
using TrainerDeck.UI.Input;

namespace TrainerDeck.UI.ViewModels;


public sealed partial class TextEntryViewModel : PageViewModel
{
    private readonly MainViewModel _main;
    private readonly Action<string> _save;

    public TextEntryViewModel(MainViewModel main, string title, string prompt, string initial, Action<string> save)
    {
        _main = main;
        _save = save;
        Title = title;
        Prompt = prompt;
        text = initial;
        if (main.LastDevice == InputSource.Controller)
        {
            TouchKeyboard.Show();
        }
    }

    public string Title { get; }

    public string Prompt { get; }

    [ObservableProperty]
    private string text;

    public override IReadOnlyList<FooterHint> Hints => [new(InputAction.Activate, "Save"), new(InputAction.Back, "Cancel")];

    public override bool Handle(InputAction action)
    {
        if (action != InputAction.Activate)
        {
            return false;
        }

        _main.Pop();
        _save(Text);
        return true;
    }
}
