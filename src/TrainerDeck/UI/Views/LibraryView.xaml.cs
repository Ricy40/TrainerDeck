using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TrainerDeck.UI.Controls;
using TrainerDeck.UI.Native;
using TrainerDeck.UI.ViewModels;

namespace TrainerDeck.UI.Views;

public partial class LibraryView : UserControl
{
    private CardGridPanel? _panel;
    private ScrollViewer? _scroller;
    private LibraryViewModel? _vm;

    public LibraryView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
        SizeChanged += (_, _) => UpdateMetrics();
    }

    public bool IsSearchFocused => SearchBox.IsKeyboardFocusWithin;

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

        item.BringIntoView(new Rect(-24, -28, item.ActualWidth + 48, item.ActualHeight + 56));
        if (!IsSearchFocused && IsKeyboardFocusWithin)
        {
            item.Focus();
        }
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

    private void OnCardDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left
            && ItemsControl.ContainerFromElement(CardList, e.OriginalSource as DependencyObject) is ListBoxItem { DataContext: TrainerCardViewModel card })
        {
            _vm?.Activate(card);
        }
    }
}
