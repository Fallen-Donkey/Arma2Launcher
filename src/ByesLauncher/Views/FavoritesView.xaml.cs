using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ByesLauncher.ViewModels;

namespace ByesLauncher.Views;

public partial class FavoritesView : UserControl
{
    public FavoritesView() => InitializeComponent();

    /// Double-click a favorite row → join. Same row-resolution guard as the
    /// Servers tab so dbl-clicks on scrollbars / column headers don't fire.
    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject src) return;
        var row = FindAncestor<DataGridRow>(src);
        if (row == null) return;
        if (DataContext is not FavoritesViewModel vm) return;
        if (vm.JoinCommand.CanExecute(null))
            vm.JoinCommand.Execute(null);
    }

    private static T? FindAncestor<T>(DependencyObject? obj) where T : DependencyObject
    {
        while (obj != null && obj is not T)
            obj = System.Windows.Media.VisualTreeHelper.GetParent(obj);
        return obj as T;
    }
}
