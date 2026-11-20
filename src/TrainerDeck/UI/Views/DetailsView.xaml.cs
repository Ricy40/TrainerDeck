using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TrainerDeck.UI.ViewModels;

namespace TrainerDeck.UI.Views;

public partial class DetailsView : UserControl, IPageView
{
    private DetailsViewModel? _vm;

    public DetailsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    public void FocusDefault()
    {
        if (_vm?.IsRenaming == true)
        {
            FocusRename();
        }
        else
        {
            Focus();
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
        {
            _vm.RenameStarted -= OnRenameStarted;
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        _vm = DataContext as DetailsViewModel;
        if (_vm is not null)
        {
            _vm.RenameStarted += OnRenameStarted;
            _vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnRenameStarted(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, FocusRename);

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DetailsViewModel.IsRenaming) && _vm?.IsRenaming == false && RenameBox.IsKeyboardFocusWithin)
        {
            Focus();
        }
    }

    private void FocusRename()
    {
        RenameBox.Focus();
        RenameBox.SelectAll();
    }
}
