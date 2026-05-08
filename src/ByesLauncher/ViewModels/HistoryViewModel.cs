using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ByesLauncher.Models;
using ByesLauncher.Services;

namespace ByesLauncher.ViewModels;

/// History tab. Filterable view over the persisted join log; supports rejoin,
/// remove-single, clear-all, and clear-older-than-N-days bulk operations.
public partial class HistoryViewModel : ObservableObject
{
    private readonly HistoryService _history;
    private readonly ModpackJoinCoordinator _joiner;
    private readonly A2sQuery _a2s;

    [ObservableProperty] private ObservableCollection<HistoryEntry> entries = new();
    [ObservableProperty] private HistoryEntry? selected;
    [ObservableProperty] private string filter = "";

    public ICollectionView EntriesView { get; }

    /// Forwarded from the join coordinator. Drives the Rejoin button label
    /// + IsEnabled like the other tabs.
    public bool IsJoining => _joiner.IsJoining;

    public HistoryViewModel(HistoryService history, ModpackJoinCoordinator joiner, A2sQuery a2s)
    {
        _history = history;
        _joiner = joiner;
        _a2s = a2s;

        EntriesView = new ListCollectionView(Entries) { Filter = ApplyFilter };

        _joiner.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ModpackJoinCoordinator.IsJoining))
                OnPropertyChanged(nameof(IsJoining));
        };

        Refresh();
    }

    partial void OnFilterChanged(string value) => EntriesView.Refresh();

    private bool ApplyFilter(object obj)
    {
        if (obj is not HistoryEntry h) return false;
        if (string.IsNullOrWhiteSpace(Filter)) return true;
        var f = Filter.Trim();
        return h.Name.Contains(f, StringComparison.OrdinalIgnoreCase)
            || h.Map.Contains(f, StringComparison.OrdinalIgnoreCase)
            || h.Endpoint.Contains(f, StringComparison.OrdinalIgnoreCase)
            || h.ModpackIds.Any(m => m.Contains(f, StringComparison.OrdinalIgnoreCase));
    }

    [RelayCommand]
    public void Refresh()
    {
        Entries.Clear();
        foreach (var h in _history.All()) Entries.Add(h);
        EntriesView.Refresh();
    }

    [RelayCommand]
    private async Task RejoinAsync()
    {
        if (Selected == null) return;
        // Build a temporary ServerInfo from the history entry so the standard
        // join flow (modpack sync + password + launch) works unchanged.
        var parts = Selected.Endpoint.Split(':');
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var ip) || !ushort.TryParse(parts[1], out var port))
        {
            MessageBox.Show("Bad history entry — couldn't parse endpoint.");
            return;
        }
        var ep = new ServerEndpoint(ip, port);
        var live = await _a2s.QueryAsync(ep);
        var info = live ?? new ServerInfo
        {
            Endpoint = ep, Name = Selected.Name, Map = Selected.Map,
        };
        info.ModpackIds = new List<string>(Selected.ModpackIds);
        await _joiner.JoinAsync(info);
        Refresh();
    }

    /// Single-entry remove — used by right-click "Remove from history" and
    /// the toolbar button. Quiet (no confirm dialog) since one-row removal
    /// is low-impact and easy to undo by rejoining.
    [RelayCommand]
    private void RemoveSelected()
    {
        if (Selected == null) return;
        _history.Remove(Selected.Endpoint);
        Refresh();
    }

    [RelayCommand]
    private void ClearAll()
    {
        if (MessageBox.Show("Clear all history?", "Confirm", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        _history.Clear();
        Refresh();
    }

    /// Bulk-clear by age. The XAML wires three buttons with parameter values
    /// 7 / 30 / 90 — using a parameter rather than three separate commands
    /// keeps the VM small and lets us add more thresholds via XAML alone.
    [RelayCommand]
    private void ClearOlderThan(object? parameter)
    {
        if (parameter == null) return;
        int days;
        // The parameter comes through as a string from XAML CommandParameter.
        if (parameter is int i) days = i;
        else if (!int.TryParse(parameter.ToString(), out days)) return;
        if (days < 1) return;

        var label = days == 1 ? "1 day" : $"{days} days";
        if (MessageBox.Show($"Clear history entries older than {label}?", "Confirm",
                            MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        var removed = _history.ClearOlderThan(days);
        Refresh();
        if (removed > 0)
            MessageBox.Show($"Removed {removed} {(removed == 1 ? "entry" : "entries")}.",
                            "History pruned", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
