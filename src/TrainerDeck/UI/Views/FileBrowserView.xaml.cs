using System.Windows.Controls;

namespace TrainerDeck.UI.Views;

public partial class FileBrowserView : UserControl, IPageView
{
    public FileBrowserView() => InitializeComponent();

    public void FocusDefault() => Focus();

    public void FocusPath()
    {
        PathBox.Focus();
        PathBox.SelectAll();
    }
}
