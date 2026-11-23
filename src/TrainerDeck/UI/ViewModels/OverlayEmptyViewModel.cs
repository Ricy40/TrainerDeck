namespace TrainerDeck.UI.ViewModels;


public sealed class OverlayEmptyViewModel : RowListPageViewModel
{
    public OverlayEmptyViewModel(MainViewModel main, Action openTrainerDeck, Action close) : base(main)
    {
        Title = "No trainer running";
        Body = "Launch a trainer from Trainer Deck, then open the overlay again to turn its cheats on and off.";
        SetRows(
        [
            new ActionRow("Open Trainer Deck", Glyphs.Play, openTrainerDeck, "Open"),
            new ActionRow("Close overlay", Glyphs.Close, close, "Close"),
        ]);
    }

    protected override string BackLabel => "Close";
}
