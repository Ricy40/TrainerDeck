using System.Windows.Controls;

namespace TrainerDeck.UI.Views;

public partial class IgnorePatternsView : UserControl, IPageView
{
    public IgnorePatternsView() => InitializeComponent();

    public void FocusDefault()
    {
        Editor.Focus();
        Editor.CaretIndex = Editor.Text.Length;
    }
}
