using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TrainerDeck.Core.Input;

namespace TrainerDeck.UI.ViewModels;

public abstract partial class RowListPageViewModel : PageViewModel
{
    protected RowListPageViewModel(MainViewModel main) => Main = main;

    protected MainViewModel Main { get; }

    [ObservableProperty]
    private string title = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSubtitle))]
    private string? subtitle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBody))]
    private string? body;

    [ObservableProperty]
    private ObservableCollection<RowViewModel> rows = new();

    [ObservableProperty]
    private RowViewModel? selectedRow;

    public bool HasSubtitle => !string.IsNullOrEmpty(Subtitle);

    public bool HasBody => !string.IsNullOrEmpty(Body);

    protected virtual string BackLabel => "Back";

    public override IReadOnlyList<FooterHint> Hints
    {
        get
        {
            var hints = new List<FooterHint>();
            if (SelectedRow is { } row)
            {
                hints.Add(new(InputAction.Activate, row.ActivateLabel));
                if (row.SecondaryLabel is string s)
                {
                    hints.Add(new(InputAction.Favourite, s));
                }
            }

            hints.Add(new(InputAction.Back, BackLabel));
            return hints;
        }
    }

    partial void OnSelectedRowChanged(RowViewModel? value) => HintsChanged();

    protected void SetRows(IEnumerable<RowViewModel> rows, string? selectLabel = null)
    {
        string? keep = selectLabel ?? SelectedRow?.Label;
        int keepIndex = SelectedRow is null ? 0 : Math.Max(0, Rows.IndexOf(SelectedRow));
        Rows = new ObservableCollection<RowViewModel>(rows);
        SelectedRow = Rows.FirstOrDefault(r => r.IsSelectable && r.Label == keep)
            ?? Nearest(Math.Min(keepIndex, Rows.Count - 1));
    }

    public void ActivateRow(RowViewModel row)
    {
        if (!row.IsSelectable)
        {
            return;
        }

        SelectedRow = row;
        row.Activate();
        HintsChanged();
    }

    public override bool Handle(InputAction action)
    {
        if (HandlePage(action))
        {
            return true;
        }

        switch (action)
        {
            case InputAction.Up:
                Move(-1);
                return true;
            case InputAction.Down:
                Move(1);
                return true;
            case InputAction.PageUp:
                Move(-6);
                return true;
            case InputAction.PageDown:
                Move(6);
                return true;
            case InputAction.Home:
                SelectedRow = Nearest(0);
                return true;
            case InputAction.End:
                SelectedRow = Nearest(Rows.Count - 1, -1);
                return true;
            case InputAction.Left or InputAction.Right when SelectedRow?.CanAdjust == true:
                SelectedRow.Adjust(action == InputAction.Right ? 1 : -1);
                HintsChanged();
                return true;
            case InputAction.Activate when SelectedRow is { } row:
                ActivateRow(row);
                return true;
            case InputAction.Favourite when SelectedRow?.SecondaryLabel is not null:
                SelectedRow.Secondary();
                HintsChanged();
                return true;
            default:
                return false;
        }
    }

    protected virtual bool HandlePage(InputAction action) => false;

    private void Move(int delta)
    {
        if (Rows.Count == 0)
        {
            return;
        }

        int index = SelectedRow is null ? -1 : Rows.IndexOf(SelectedRow);
        int step = Math.Sign(delta);
        int target = index;
        int remaining = Math.Abs(delta);
        for (int i = index + step; i >= 0 && i < Rows.Count && remaining > 0; i += step)
        {
            if (Rows[i].IsSelectable)
            {
                target = i;
                remaining--;
            }
        }

        if (target >= 0 && target != index)
        {
            SelectedRow = Rows[target];
        }
    }

    private RowViewModel? Nearest(int index, int direction = 1)
    {
        if (Rows.Count == 0)
        {
            return null;
        }

        index = Math.Clamp(index, 0, Rows.Count - 1);
        for (int i = index; i >= 0 && i < Rows.Count; i += direction)
        {
            if (Rows[i].IsSelectable)
            {
                return Rows[i];
            }
        }

        for (int i = index; i >= 0 && i < Rows.Count; i -= direction)
        {
            if (Rows[i].IsSelectable)
            {
                return Rows[i];
            }
        }

        return null;
    }
}
