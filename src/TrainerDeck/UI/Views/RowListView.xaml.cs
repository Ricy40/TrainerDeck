using System.Windows.Controls;

namespace TrainerDeck.UI.Views;

public partial class RowListView : UserControl, IPageView
{
    public RowListView() => InitializeComponent();

    public void FocusDefault() => Focus();
}
