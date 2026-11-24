using CommunityToolkit.Mvvm.ComponentModel;
using TrainerDeck.Core.Input;

namespace TrainerDeck.UI.ViewModels;

public sealed record FooterHint(InputAction Action, string Label, string? ControllerLabel = null, bool Primary = false);

public abstract class PageViewModel : ObservableObject
{
    public abstract bool Handle(InputAction action);

    public abstract IReadOnlyList<FooterHint> Hints { get; }


    public virtual void OnClosed()
    {
    }


    public virtual void OnResumed()
    {
    }

    protected void HintsChanged() => OnPropertyChanged(nameof(Hints));
}
