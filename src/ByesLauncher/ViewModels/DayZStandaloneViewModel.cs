using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ByesLauncher.Models;
using ByesLauncher.Services;

namespace ByesLauncher.ViewModels;

/// DayZ Standalone server browser — same Steam master infrastructure, appid 221100.
/// Joins via the Steam protocol so BattlEye/Steam auth Just Works.
public partial class DayZStandaloneViewModel : ObservableObject
{
    private readonly SteamMasterQuery _master;
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

    public DayZStandaloneViewModel(SteamMasterQuery master, A2sQuery a2s, LaunchService launch)
    {
        _master = master;
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
        Status = "Querying DayZ Standalone master server (appid 221100)...";
        Servers.Clear();
        try
        {
            var endpoints = await _master.QueryAsync(@"\appid\221100\empty\1\full\1");
            Status = $"Querying {endpoints.Count} servers...";

            await _a2s.QueryManyAsync(endpoints, parallelism: 96, onResult: info =>
            {
                Application.Current.Dispatcher.Invoke(() => Servers.Add(info));
            });

            Status = $"{Servers.Count} servers loaded.";
        }
        catch (Exception ex) { Status = "Error: " + ex.Message; }
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
