using CommunityToolkit.Mvvm.ComponentModel;
using TrainerDeck.Core.Input;

namespace TrainerDeck.UI.ViewModels;

public sealed record FooterHint(InputAction Action, string Label);

public abstract class PageViewModel : ObservableObject
{
    public abstract bool Handle(InputAction action);

    public abstract IReadOnlyList<FooterHint> Hints { get; }

    protected void HintsChanged() => OnPropertyChanged(nameof(Hints));
}
