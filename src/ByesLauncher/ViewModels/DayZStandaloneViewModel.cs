using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ByesLauncher.Models;
using ByesLauncher.Services;

namespace ByesLauncher.ViewModels;

/// DayZ Standalone server browser. Server discovery goes through the BYES
/// backend's /api/servers/discover?game=dayzsa endpoint (Steam Web API for
/// appid 221100, with BattleMetrics fallback). Joining uses the Steam
/// protocol so BattlEye/Steam auth flows work natively.
public partial class DayZStandaloneViewModel : ObservableObject
{
    private readonly BackendClient _backend;
    private readonly A2sQuery _a2s;
    private readonly LaunchService _launch;

    [ObservableProperty] private ObservableCollection<ServerInfo> servers = new();
    [ObservableProperty] private ServerInfo? selected;
    [ObservableProperty] private string filter = "";
    [ObservableProperty] private bool hideEmpty = true;
    [ObservableProperty] private bool firstPersonOnly = false;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string status = "";

    public ICollectionView ServersView { get; }

    public DayZStandaloneViewModel(BackendClient backend, A2sQuery a2s, LaunchService launch)
    {
        _backend = backend;
        _a2s = a2s;
        _launch = launch;

        ServersView = CollectionViewSource.GetDefaultView(Servers);
        ServersView.SortDescriptions.Add(new SortDescription(nameof(ServerInfo.Players), ListSortDirection.Descending));
        ServersView.Filter = obj =>
        {
            if (obj is not ServerInfo s) return false;
            if (HideEmpty && s.Players == 0) return false;
            if (FirstPersonOnly && !s.Gametype.Contains("1pp", StringComparison.OrdinalIgnoreCase)
                                && !s.Name.Contains("1PP", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.IsNullOrWhiteSpace(Filter)) return true;
            var f = Filter.Trim();
            return s.Name.Contains(f, StringComparison.OrdinalIgnoreCase)
                || s.Map.Contains(f, StringComparison.OrdinalIgnoreCase);
        };
    }

    partial void OnFilterChanged(string value) => ServersView.Refresh();
    partial void OnHideEmptyChanged(bool value) => ServersView.Refresh();
    partial void OnFirstPersonOnlyChanged(bool value) => ServersView.Refresh();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        Status = "Discovering DayZ Standalone servers...";
        Servers.Clear();
        try
        {
            var result = await _backend.GetDiscoveredServersAsync("dayzsa");
            foreach (var info in result.Servers) Servers.Add(info);
            var src = string.IsNullOrEmpty(result.SourceDisplay) ? "" : $" · {result.SourceDisplay}";
            Status = $"{Servers.Count} servers loaded{src}.";
        }
        catch (Exception ex) { Status = "Discovery offline: " + ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void Join()
    {
        if (Selected == null) return;
        try { _launch.LaunchDayZStandalone(Selected); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Launch failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}
