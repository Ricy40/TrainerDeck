using System.Windows.Controls;

namespace TrainerDeck.UI.Views;

public partial class AccentPickerView : UserControl, IPageView
{
    public AccentPickerView() => InitializeComponent();

    public void FocusDefault() => Focus();
}
