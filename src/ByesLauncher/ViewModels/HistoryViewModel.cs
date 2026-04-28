using System.Collections.ObjectModel;
using System.Net;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ByesLauncher.Models;
using ByesLauncher.Services;

namespace ByesLauncher.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly HistoryService _history;
    private readonly ModpackJoinCoordinator _joiner;
    private readonly A2sQuery _a2s;

    [ObservableProperty] private ObservableCollection<HistoryEntry> entries = new();
    [ObservableProperty] private HistoryEntry? selected;

    public HistoryViewModel(HistoryService history, ModpackJoinCoordinator joiner, A2sQuery a2s)
    {
        _history = history;
        _joiner = joiner;
        _a2s = a2s;
        Refresh();
    }

    [RelayCommand]
    public void Refresh()
    {
        Entries.Clear();
        foreach (var h in _history.All()) Entries.Add(h);
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
        info.ModpackId = Selected.ModpackId;
        await _joiner.JoinAsync(info);
        Refresh();
    }

    [RelayCommand]
    private void Clear()
    {
        if (MessageBox.Show("Clear all history?", "Confirm", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        _history.Clear();
        Refresh();
    }
}
