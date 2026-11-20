using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TrainerDeck.UI.Controls;
using TrainerDeck.UI.Native;
using TrainerDeck.UI.ViewModels;

namespace TrainerDeck.UI.Views;

public partial class LibraryView : UserControl, IPageView
{
    private CardGridPanel? _panel;
    private ScrollViewer? _scroller;
    private SmoothScroller? _smooth;
    private LibraryViewModel? _vm;

    public LibraryView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
        SizeChanged += (_, _) => UpdateMetrics();
    }

    public bool IsSearchFocused => SearchBox.IsKeyboardFocusWithin;

    public void FocusDefault() => FocusGrid();

    public void FocusGrid()
    {
        if (_vm?.Selected is { } sel && CardList.ItemContainerGenerator.ContainerFromItem(sel) is ListBoxItem item)
        {
            item.Focus();
        }
        else
        {
            CardList.Focus();
        }
    }

    public void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.CaretIndex = SearchBox.Text.Length;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        CardList.ApplyTemplate();
        _scroller = CardList.Template.FindName("Scroller", CardList) as ScrollViewer;
        if (_scroller is not null && _smooth is null)
        {
            _smooth = new SmoothScroller(_scroller);
            _scroller.PreviewMouseWheel += OnMouseWheel;
        }
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            _panel = WindowInterop.FindDescendant<CardGridPanel>(CardList);
            if (_panel is not null)
            {
                _panel.GridMetricsChanged += (_, _) => UpdateMetrics();
            }

            UpdateMetrics();
            FocusGrid();
        });
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        _vm = DataContext as LibraryViewModel;
        if (_vm is not null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LibraryViewModel.Selected))
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, BringSelectedIntoView);
        }
    }

    private void BringSelectedIntoView()
    {
        if (_vm?.Selected is not { } sel || CardList.ItemContainerGenerator.ContainerFromItem(sel) is not FrameworkElement item)
        {
            return;
        }

        if (_scroller is not null && _smooth is not null)
        {
            const double margin = 30;
            Rect r = item.TransformToAncestor(_scroller).TransformBounds(new Rect(0, 0, item.ActualWidth, item.ActualHeight));
            double offset = _scroller.VerticalOffset;
            if (r.Top - margin < 0)
            {
                _smooth.ScrollTo(offset + r.Top - margin);
            }
            else if (r.Bottom + margin > _scroller.ViewportHeight)
            {
                _smooth.ScrollTo(offset + r.Bottom + margin - _scroller.ViewportHeight);
            }
        }
        else
        {
            item.BringIntoView(new Rect(-24, -28, item.ActualWidth + 48, item.ActualHeight + 56));
        }

        if (!IsSearchFocused && IsKeyboardFocusWithin)
        {
            item.Focus();
        }
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_smooth is null)
        {
            return;
        }

        _smooth.ScrollTo(_smooth.Target - (e.Delta * 1.4));
        e.Handled = true;
    }

    private void UpdateMetrics()
    {
        if (_vm is null || _panel is null)
        {
            return;
        }

        _vm.Columns = _panel.Columns;
        double viewport = _scroller?.ViewportHeight ?? ActualHeight;
        _vm.PageRows = _panel.RowPitch > 0 ? Math.Max(1, (int)(viewport / _panel.RowPitch)) : 1;
    }

    private void OnCardRightClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null
            || ItemsControl.ContainerFromElement(CardList, e.OriginalSource as DependencyObject) is not ListBoxItem { DataContext: TrainerCardViewModel card } item)
        {
            return;
        }

        _vm.Selected = card;
        var menu = new ContextMenu { PlacementTarget = item, Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint };
        menu.Items.Add(MenuItem(card.IsRunning ? "Switch to" : "Launch", "", _vm.ActivateCommand, card));
        if (card.IsRunning)
        {
            menu.Items.Add(MenuItem("Close trainer", "", _vm.CloseTrainerCommand, card));
        }

        menu.Items.Add(MenuItem(card.IsFavourite ? "Remove from favourites" : "Add to favourites", card.IsFavourite ? "" : "", _vm.ToggleFavouriteCommand, card));
        menu.Items.Add(MenuItem("Details", "", _vm.OpenDetailsCommand, card));
        menu.Items.Add(MenuItem("Show in folder", "", _vm.ShowInFolderCommand, card));
        menu.IsOpen = true;
        e.Handled = true;
    }

    private static MenuItem MenuItem(string header, string glyph, ICommand command, object parameter) =>
        new() { Header = header, Tag = glyph, Command = command, CommandParameter = parameter };

    private void OnCardDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left
            && ItemsControl.ContainerFromElement(CardList, e.OriginalSource as DependencyObject) is ListBoxItem { DataContext: TrainerCardViewModel card })
        {
            _ = _vm?.Activate(card);
        }
    }
}
