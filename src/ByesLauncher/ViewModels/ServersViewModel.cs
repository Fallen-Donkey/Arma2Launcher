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

/// Combined Arma 2 OA server browser. BYES servers always pinned to the top
/// (sorted by IsByes desc, then by player count desc).
public partial class ServersViewModel : ObservableObject
{
    private readonly BackendClient _backend;
    private readonly SteamMasterQuery _master;
    private readonly A2sQuery _a2s;
    private readonly ModpackJoinCoordinator _joiner;
    private readonly FavoritesService _favorites;
    private readonly ModpackMatcher _matcher;

    [ObservableProperty] private ObservableCollection<ServerInfo> servers = new();
    [ObservableProperty] private ServerInfo? selected;
    [ObservableProperty] private string filter = "dayz";
    [ObservableProperty] private bool hideEmpty;
    [ObservableProperty] private bool hideFull;
    [ObservableProperty] private bool hidePassworded;
    [ObservableProperty] private bool hideOffline = true;
    [ObservableProperty] private bool hideNoInfo;
    [ObservableProperty] private bool battleEyeOnly;
    [ObservableProperty] private string mapFilter = "";
    [ObservableProperty] private string modFilter = "";
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string status = "";

    public ObservableCollection<string> AvailableMaps { get; } = new();
    public ObservableCollection<string> AvailableMods { get; } = new();

    public ICollectionView ServersView { get; }

    public ServersViewModel(BackendClient backend, SteamMasterQuery master, A2sQuery a2s,
                            ModpackJoinCoordinator joiner, FavoritesService favorites,
                            ModpackMatcher matcher)
    {
        _backend = backend;
        _master = master;
        _a2s = a2s;
        _joiner = joiner;
        _favorites = favorites;
        _matcher = matcher;

        ServersView = CollectionViewSource.GetDefaultView(Servers);
        ServersView.SortDescriptions.Add(new SortDescription(nameof(ServerInfo.IsByes), ListSortDirection.Descending));
        ServersView.SortDescriptions.Add(new SortDescription(nameof(ServerInfo.Players), ListSortDirection.Descending));
        ServersView.Filter = ServerFilter;
    }

    partial void OnFilterChanged(string value) => ServersView.Refresh();
    partial void OnHideEmptyChanged(bool value) => ServersView.Refresh();
    partial void OnHideFullChanged(bool value) => ServersView.Refresh();
    partial void OnHidePasswordedChanged(bool value) => ServersView.Refresh();
    partial void OnHideOfflineChanged(bool value) => ServersView.Refresh();
    partial void OnHideNoInfoChanged(bool value) => ServersView.Refresh();
    partial void OnBattleEyeOnlyChanged(bool value) => ServersView.Refresh();
    partial void OnMapFilterChanged(string value) => ServersView.Refresh();
    partial void OnModFilterChanged(string value) => ServersView.Refresh();

    private bool ServerFilter(object obj)
    {
        if (obj is not ServerInfo s) return false;
        if (s.IsByes) return true; // BYES always visible regardless of filters

        if (HideEmpty       && s.Players == 0)         return false;
        if (HideFull        && s.MaxPlayers > 0 && s.Players >= s.MaxPlayers) return false;
        if (HidePassworded  && s.PasswordProtected)    return false;
        if (HideOffline     && s.MaxPlayers == 0)      return false; // proxy for "didn't respond"
        if (HideNoInfo      && string.IsNullOrWhiteSpace(s.Name)) return false;
        if (BattleEyeOnly   && !s.BattleEye)           return false;

        if (!string.IsNullOrWhiteSpace(MapFilter) &&
            !s.Map.Equals(MapFilter, StringComparison.OrdinalIgnoreCase)) return false;

        if (!string.IsNullOrWhiteSpace(ModFilter) &&
            !s.Gametype.Contains(ModFilter, StringComparison.OrdinalIgnoreCase)) return false;

        if (string.IsNullOrWhiteSpace(Filter)) return true;
        var f = Filter.Trim();
        return s.Name.Contains(f, StringComparison.OrdinalIgnoreCase)
            || s.Map.Contains(f, StringComparison.OrdinalIgnoreCase)
            || s.Gametype.Contains(f, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void ClearFilters()
    {
        Filter = "";
        HideEmpty = false; HideFull = false; HidePassworded = false;
        HideOffline = false; HideNoInfo = false; BattleEyeOnly = false;
        MapFilter = ""; ModFilter = "";
    }

    private void RebuildFilterDropdowns()
    {
        var maps = Servers.Where(s => !string.IsNullOrWhiteSpace(s.Map))
                          .Select(s => s.Map)
                          .Distinct(StringComparer.OrdinalIgnoreCase)
                          .OrderBy(m => m, StringComparer.OrdinalIgnoreCase);
        var mods = Servers.Where(s => !string.IsNullOrWhiteSpace(s.Gametype))
                          .Select(s => s.Gametype)
                          .Distinct(StringComparer.OrdinalIgnoreCase)
                          .OrderBy(m => m, StringComparer.OrdinalIgnoreCase);
        AvailableMaps.Clear();
        AvailableMods.Clear();
        AvailableMaps.Add("");
        AvailableMods.Add("");
        foreach (var m in maps) AvailableMaps.Add(m);
        foreach (var m in mods) AvailableMods.Add(m);
    }

    [RelayCommand]
    private async Task RefreshSingleAsync()
    {
        if (Selected == null) return;
        var info = await _a2s.QueryAsync(Selected.Endpoint);
        if (info == null) return;
        Selected.Name = info.Name;
        Selected.Map = info.Map;
        Selected.Players = info.Players;
        Selected.MaxPlayers = info.MaxPlayers;
        Selected.Ping = info.Ping;
        Selected.Gametype = info.Gametype;
        ServersView.Refresh();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        Status = "Loading BYES servers...";
        Servers.Clear();
        try
        {
            // Load modpack catalog up-front so we can auto-tag public servers.
            try
            {
                var modpacks = await _backend.GetModpacksAsync();
                _matcher.Load(modpacks);
            }
            catch { /* offline or backend down — matcher just won't tag anything */ }

            // 1) BYES pinned servers first
            try
            {
                var byes = await _backend.GetByesServersAsync();
                foreach (var e in byes)
                {
                    var ep = new ServerEndpoint(IPAddress.Parse(e.Ip), (ushort)e.Port);
                    var info = await _a2s.QueryAsync(ep) ?? new ServerInfo
                    {
                        Endpoint = ep, Name = e.Name + "  (offline)", Players = 0, MaxPlayers = 0,
                    };
                    info.IsByes = true;
                    info.ModpackId = e.ModpackId;
                    if (!info.Name.EndsWith("(offline)")) info.Name = e.Name;
                    info.IsFavorite = _favorites.IsFavorite(info.Endpoint);
                    Servers.Add(info);
                }
            }
            catch (Exception ex)
            {
                Status = "Backend offline: " + ex.Message + " — continuing with public servers.";
            }

            // 2) Public master server discovery
            Status = "Querying Steam master server...";
            var endpoints = await _master.QueryAsync();
            Status = $"Querying {endpoints.Count} servers...";

            int tally = 0;
            await _a2s.QueryManyAsync(endpoints, parallelism: 96, onResult: info =>
            {
                info.IsFavorite = _favorites.IsFavorite(info.Endpoint);
                // Auto-link to a modpack if the server's metadata matches admin-defined
                // patterns. Lets "any public Epoch server" auto-sync our Epoch pack.
                if (string.IsNullOrEmpty(info.ModpackId))
                    info.ModpackId = _matcher.MatchFor(info);
                Application.Current.Dispatcher.Invoke(() => Servers.Add(info));
                Interlocked.Increment(ref tally);
            });

            Status = $"{Servers.Count} servers loaded ({Servers.Count(s => s.IsByes)} BYES + {Servers.Count(s => !s.IsByes)} public).";
            RebuildFilterDropdowns();
        }
        catch (Exception ex) { Status = "Error: " + ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task JoinAsync()
    {
        if (Selected == null) return;
        try { await _joiner.JoinAsync(Selected); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Launch failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        if (Selected != null) _favorites.Toggle(Selected);
    }

    [RelayCommand]
    private async Task ShowPlayersAsync()
    {
        if (Selected == null) return;
        var dlg = new Views.Dialogs.PlayerListDialog(_a2s, Selected)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        dlg.Show();
        await Task.CompletedTask;
    }

    /// Look up a server we've already loaded by IP:port. Returns null if not found.
    /// Used by the byes:// auto-join path.
    public ServerInfo? FindByEndpoint(string ip, int port) =>
        Servers.FirstOrDefault(s =>
            s.Endpoint.Address.ToString() == ip &&
            s.Endpoint.GamePort == port);

    /// Programmatic join (from byes:// or anywhere). Selects the server and runs JoinAsync.
    public async Task JoinFromExternalAsync(ServerInfo server)
    {
        Selected = server;
        await _joiner.JoinAsync(server);
    }
}
