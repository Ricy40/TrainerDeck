using System.Windows.Controls;

namespace TrainerDeck.UI.Views;

public partial class TextEntryView : UserControl, IPageView
{
    public TextEntryView() => InitializeComponent();

    public void FocusDefault()
    {
        Editor.Focus();
        Editor.SelectAll();
    }
}
