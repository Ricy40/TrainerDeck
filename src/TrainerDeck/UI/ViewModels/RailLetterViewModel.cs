using CommunityToolkit.Mvvm.ComponentModel;

namespace TrainerDeck.UI.ViewModels;

public sealed partial class RailLetterViewModel : ObservableObject
{
    public RailLetterViewModel(char letter) => Letter = letter;

    public char Letter { get; }

    public string Text => Letter.ToString();

    [ObservableProperty]
    private bool isAvailable;

    [ObservableProperty]
    private bool isCurrent;
}
