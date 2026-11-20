using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TrainerDeck.UI.ViewModels;

namespace TrainerDeck.UI.Controls;


public sealed class RowListBox : ListBox
{
    private RowViewModel? _pressed;

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _pressed = RowAt(e);
        if (_pressed is not null)
        {
            e.Handled = true;
        }

        base.OnPreviewMouseLeftButtonDown(e);
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        RowViewModel? row = RowAt(e);
        if (row is not null && row == _pressed && DataContext is RowListPageViewModel page)
        {
            page.ActivateRow(row);
            e.Handled = true;
        }

        _pressed = null;
        base.OnPreviewMouseLeftButtonUp(e);
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        if (SelectedItem is { } item)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                if (ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement c)
                {
                    c.BringIntoView(new Rect(0, -60, c.ActualWidth, c.ActualHeight + 120));
                }
            });
        }
    }

    private RowViewModel? RowAt(MouseButtonEventArgs e) =>
        ContainerFromElement(this, e.OriginalSource as DependencyObject) is ListBoxItem { DataContext: RowViewModel row } ? row : null;
}
