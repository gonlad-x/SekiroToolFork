using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SekiroTool.ViewModels;

namespace SekiroTool.Views.Tabs;

public partial class SaveManagerTab : UserControl
{
    private readonly SaveManagerViewModel _saveManagerViewModel;

    public SaveManagerTab(SaveManagerViewModel saveManagerViewModel)
    {
        DataContext = saveManagerViewModel;
        _saveManagerViewModel = saveManagerViewModel;
        InitializeComponent();
    }

    // TreeView.SelectedItem is read-only, so selection is pushed to the ViewModel from here.
    private void SaveTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        _saveManagerViewModel.SelectedEntry = e.NewValue as SaveEntryViewModel;
    }

    private void SaveTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Double-clicking a folder is the expander's job; only savestates load.
        if (_saveManagerViewModel.SelectedEntry is not { IsFolder: false }) return;
        if (!_saveManagerViewModel.CanLoad) return;

        _saveManagerViewModel.LoadCommand.Execute(null);
        e.Handled = true;
    }
}
