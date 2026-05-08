using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ByesLauncher.ViewModels;

namespace ByesLauncher.Views;

public partial class HistoryView : UserControl
{
    public HistoryView() => InitializeComponent();

    /// Double-click a history row → rejoin. Same row-resolution guard as the
    /// other tabs: only act if the click landed on an actual DataGridRow.
    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject src) return;
        var row = FindAncestor<DataGridRow>(src);
        if (row == null) return;
        if (DataContext is not HistoryViewModel vm) return;
        if (vm.RejoinCommand.CanExecute(null))
            vm.RejoinCommand.Execute(null);
    }

    private static T? FindAncestor<T>(DependencyObject? obj) where T : DependencyObject
    {
        while (obj != null && obj is not T)
            obj = System.Windows.Media.VisualTreeHelper.GetParent(obj);
        return obj as T;
    }
}
