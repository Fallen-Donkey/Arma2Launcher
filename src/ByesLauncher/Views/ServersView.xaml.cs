using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ByesLauncher.Models;
using ByesLauncher.ViewModels;

namespace ByesLauncher.Views;

public partial class ServersView : UserControl
{
    public ServersView() => InitializeComponent();

    /// Double-click anywhere on a row → join. Walks up the visual tree to
    /// confirm the click was on a DataGridRow (otherwise dbl-clicking the
    /// scrollbar or column headers would launch a server, which would be
    /// horrible). Falls through to the same JoinCommand the button uses, so
    /// the IsJoining lifecycle and re-entrancy guard apply identically.
    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject src) return;
        var row = FindAncestor<DataGridRow>(src);
        if (row == null) return;
        if (DataContext is not ServersViewModel vm) return;
        if (vm.JoinCommand.CanExecute(null))
            vm.JoinCommand.Execute(null);
    }

    private static T? FindAncestor<T>(DependencyObject? obj) where T : DependencyObject
    {
        while (obj != null && obj is not T)
            obj = System.Windows.Media.VisualTreeHelper.GetParent(obj);
        return obj as T;
    }

    /// Custom column-header sort handler. WPF's DataGrid would normally
    /// throw away our 4-tier default sort the moment the user clicks any
    /// column header — we want BYES servers pinned at top regardless of
    /// what the user is sorting by. So we cancel the default behavior
    /// and rebuild SortDescriptions: IsByes desc first, then whatever
    /// column the user clicked (toggle direction on repeat clicks).
    private void OnSorting(object sender, DataGridSortingEventArgs e)
    {
        if (e.Column == null) return;
        var path = e.Column.SortMemberPath;
        if (string.IsNullOrEmpty(path)) return;

        // Toggle direction: ascending → descending → ascending. Numeric
        // columns (Ping, Players) start ascending so first click on Ping
        // gives "lowest ping first" which matches the default sort.
        var newDir = e.Column.SortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;

        if (sender is not DataGrid grid) return;
        if (grid.ItemsSource is not System.ComponentModel.ICollectionView view) return;

        view.SortDescriptions.Clear();
        // Always pin BYES servers first.
        view.SortDescriptions.Add(new SortDescription(nameof(ServerInfo.IsByes), ListSortDirection.Descending));
        // Always push unmeasured ("—") rows to the bottom regardless of
        // which column the user clicked. Without this, sorting by Ping
        // ascending puts every Ping=0 row at the top (smallest int wins);
        // sorting by Name leaves "—" rows mixed throughout the alphabet.
        view.SortDescriptions.Add(new SortDescription(nameof(ServerInfo.HasPing), ListSortDirection.Descending));
        // The user's chosen column becomes the third tier — orders rows
        // within the measured group (and within the unmeasured group too,
        // though it doesn't visually matter much there since all pings = 0).
        view.SortDescriptions.Add(new SortDescription(path, newDir));

        // Update the column's visual sort indicator (the little arrow on
        // the header). Clear arrows on every other column.
        foreach (var col in grid.Columns) col.SortDirection = null;
        e.Column.SortDirection = newDir;

        e.Handled = true;
    }
}
