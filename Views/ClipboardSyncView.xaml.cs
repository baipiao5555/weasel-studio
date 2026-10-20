using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using WeaselStudio.ViewModels;

namespace WeaselStudio.Views;

public partial class ClipboardSyncView : UserControl
{
    private ClipboardSyncViewModel? _viewModel;
    private bool _syncingPassword;

    public ClipboardSyncView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel = DataContext as ClipboardSyncViewModel;
        if (_viewModel is null)
            return;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        SyncPasswordFromViewModel();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClipboardSyncViewModel.Password))
            SyncPasswordFromViewModel();
    }

    private void SyncPasswordFromViewModel()
    {
        if (_viewModel is null || PasswordInput.Password == _viewModel.Password)
            return;

        _syncingPassword = true;
        PasswordInput.Password = _viewModel.Password;
        _syncingPassword = false;
    }

    private void PasswordInput_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingPassword || _viewModel is null)
            return;

        _viewModel.Password = PasswordInput.Password;
    }
}
