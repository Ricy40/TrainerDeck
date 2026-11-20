namespace TrainerDeck.UI.ViewModels;

public sealed record PickerItem(string Label, string? Glyph, string? Detail, Action Picked);


public sealed class PickerViewModel : RowListPageViewModel
{
    public PickerViewModel(MainViewModel main, string title, string? body, IReadOnlyList<PickerItem> items) : base(main)
    {
        Title = title;
        Body = body;
        SetRows(items.Select(i => (RowViewModel)new ActionRow(i.Label, i.Glyph, () =>
        {
            Main.Pop();
            i.Picked();
        }, "Choose", i.Detail)));
    }

    protected override string BackLabel => "Cancel";
}
