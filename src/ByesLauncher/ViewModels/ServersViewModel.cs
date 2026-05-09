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
    private readonly A2sQuery _a2s;
    private readonly ModpackJoinCoordinator _joiner;
    private readonly FavoritesService _favorites;
    private readonly ModpackMatcher _matcher;

    [ObservableProperty] private ObservableCollection<ServerInfo> servers = new();
    [ObservableProperty] private ServerInfo? selected;
    [ObservableProperty] private string filter = "";
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

    /// Forwarded from the singleton join coordinator. Lets the Join button
    /// flip its label/IsEnabled while a launch is in progress on any tab.
    public bool IsJoining => _joiner.IsJoining;

    /// UTC timestamp of the last successful (or attempted) refresh. Drives
    /// the tab-activate stale-check — we don't want to spam the backend
    /// every time the user clicks a tab, only if the data is actually
    /// likely to be stale.
    public DateTime LastRefreshUtc { get; private set; } = DateTime.MinValue;

    public ServersViewModel(BackendClient backend, A2sQuery a2s,
                            ModpackJoinCoordinator joiner, FavoritesService favorites,
                            ModpackMatcher matcher)
    {
        _backend = backend;
        _a2s = a2s;
        _joiner = joiner;
        _favorites = favorites;
        _matcher = matcher;

        _joiner.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ModpackJoinCoordinator.IsJoining))
                OnPropertyChanged(nameof(IsJoining));
        };

        ServersView = CollectionViewSource.GetDefaultView(Servers);
        // Sort tiers, top → bottom of the list:
        //   1. IsByes desc      — BYES-curated servers always pinned at top
        //   2. HasPing desc     — measured servers above unmeasured ("—") ones
        //   3. Ping asc         — lowest ping first within measured group
        //   4. Players desc     — more-populated wins as the unmeasured tiebreaker
        ServersView.SortDescriptions.Add(new SortDescription(nameof(ServerInfo.IsByes),  ListSortDirection.Descending));
        ServersView.SortDescriptions.Add(new SortDescription(nameof(ServerInfo.HasPing), ListSortDirection.Descending));
        ServersView.SortDescriptions.Add(new SortDescription(nameof(ServerInfo.Ping),    ListSortDirection.Ascending));
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

    /// When the user clicks an unmeasured server, kick off a fresh probe
    /// across all three port candidates with a generous 6s timeout. Often
    /// resolves the "why is this row stuck on —" question without a manual
    /// refresh.
    partial void OnSelectedChanged(ServerInfo? value)
    {
        if (value == null || value.Ping > 0) return;
        var target = value;
        _ = Task.Run(async () =>
        {
            try
            {
                var info = await TryThreePortsAsync(target.Endpoint, timeoutMs: 6000);
                if (info != null && info.Ping > 0)
                    Application.Current.Dispatcher.Invoke(() => target.Ping = info.Ping);
            }
            catch { /* swallow — same server can be retried by the user clicking refresh */ }
        });
    }

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
        // Preserve user's current dropdown selections across the rebuild.
        // Clearing the ObservableCollection makes the bound ComboBox drop its
        // SelectedItem (the old reference is gone), which the user reads as
        // "Refresh All cleared my filters". We restore the value if the new
        // server set still contains it.
        var prevMap = MapFilter;
        var prevMod = ModFilter;

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

        // Restore the previous selection if it survived the rebuild. If a
        // map/mod the user was filtering on disappears between refreshes
        // (server went offline), fall back to the empty "show all" entry
        // rather than leaving the ComboBox in an inconsistent state.
        MapFilter = AvailableMaps.Contains(prevMap, StringComparer.OrdinalIgnoreCase) ? prevMap : "";
        ModFilter = AvailableMods.Contains(prevMod, StringComparer.OrdinalIgnoreCase) ? prevMod : "";
    }

    /// Background A2S sweep — probes each server's query port for real ping
    /// and updates ServerInfo.Ping in place. Two-pass strategy:
    ///
    ///  1. **Fast pass** (parallelism 192, 2.5s timeout) — covers the bulk of
    ///     responsive servers in 5–10 seconds. Most measurements land here.
    ///  2. **Slow retry pass** (parallelism 48, 5s timeout) — only the ones
    ///     the fast pass missed. Catches servers under heavy player load
    ///     that drop the first request.
    ///  3. **ICMP fallback** for servers still unmeasured — many shared
    ///     hosts block A2S but allow ICMP. Not as accurate (route latency,
    ///     not application response) but useful as a "host is reachable"
    ///     signal vs. "—".
    ///
    /// Servers that fail all three will keep `Ping = 0` and render as "—",
    /// which is the truthful answer: we tried, they didn't answer.
    private async Task EnrichPingsAsync(List<ServerInfo> servers)
    {
        await ProbePassAsync(servers, parallelism: 192, timeoutMs: 2500, label: "fast pass");
        // Re-run the sort after each pass so newly-measured servers visually
        // settle above the unmeasured "—" rows. ICollectionView doesn't
        // auto-resort on item-property changes — only on add/remove/replace.
        Application.Current.Dispatcher.Invoke(() => ServersView.Refresh());

        var stillUnmeasured = servers.Where(s => s.Ping == 0).ToList();
        if (stillUnmeasured.Count > 0)
        {
            Application.Current.Dispatcher.Invoke(() =>
                Status = $"{Servers.Count} servers loaded · retrying {stillUnmeasured.Count} slow servers…");
            await ProbePassAsync(stillUnmeasured, parallelism: 48, timeoutMs: 5000, label: "retry pass");
            Application.Current.Dispatcher.Invoke(() => ServersView.Refresh());
        }

        var icmpCandidates = servers.Where(s => s.Ping == 0).ToList();
        if (icmpCandidates.Count > 0)
        {
            Application.Current.Dispatcher.Invoke(() =>
                Status = $"{Servers.Count} servers loaded · ICMP probing {icmpCandidates.Count} A2S-blocked hosts…");
            await IcmpProbePassAsync(icmpCandidates);
            Application.Current.Dispatcher.Invoke(() => ServersView.Refresh());
        }

        var measured = servers.Count(s => s.Ping > 0);
        Application.Current.Dispatcher.Invoke(() =>
            Status = $"{Servers.Count} servers loaded · {measured}/{servers.Count} pings measured.");
    }

    private async Task ProbePassAsync(IReadOnlyList<ServerInfo> servers, int parallelism, int timeoutMs, string label)
    {
        var sem = new SemaphoreSlim(parallelism);
        var tasks = servers.Select(async s =>
        {
            await sem.WaitAsync();
            try
            {
                var info = await TryThreePortsAsync(s.Endpoint, timeoutMs);
                if (info != null && info.Ping > 0)
                    Application.Current.Dispatcher.Invoke(() => s.Ping = info.Ping);
            }
            catch { /* skip individual failures */ }
            finally { sem.Release(); }
        }).ToArray();
        await Task.WhenAll(tasks);
    }

    /// Most Arma 2 OA servers respond on `gamePort+1` — but a non-trivial
    /// minority follow Arma-3 style (gamePort itself) or ship hosting where
    /// the query socket is at `gamePort+2`. Maca's launcher gets ping for
    /// these by trying multiple candidates; we do the same. First successful
    /// reply wins. Stops after the first hit so we don't waste UDP traffic.
    private async Task<ServerInfo?> TryThreePortsAsync(ServerEndpoint baseEp, int timeoutMs)
    {
        // 1. Standard A2 OA convention (queryPort = baseEp.QueryPort, which
        //    is either the explicit value from discovery or gamePort+1).
        var info = await _a2s.QueryAsync(baseEp, timeoutMs);
        if (info != null) return info;

        // 2. Game port itself — covers Arma 3-style configs.
        if (baseEp.QueryPort != baseEp.GamePort)
        {
            var altGp = new ServerEndpoint(baseEp.Address, baseEp.GamePort, baseEp.GamePort);
            info = await _a2s.QueryAsync(altGp, timeoutMs);
            if (info != null) return info;
        }

        // 3. gamePort + 2 — some shared hosts use a non-default offset.
        var plus2 = (ushort)Math.Min(65535, baseEp.GamePort + 2);
        if (plus2 != baseEp.QueryPort && plus2 != baseEp.GamePort)
        {
            var altP2 = new ServerEndpoint(baseEp.Address, baseEp.GamePort, plus2);
            info = await _a2s.QueryAsync(altP2, timeoutMs);
            if (info != null) return info;
        }

        return null;
    }

    /// ICMP pings the host IP. Last-resort fallback when A2S is firewalled
    /// off. RoundtripTime is host-level latency, not application response —
    /// flag in the UI? For now we just use it as a "you can probably reach
    /// this server" signal; if it's wildly different from real A2S latency,
    /// users will figure it out from the playing experience.
    private async Task IcmpProbePassAsync(IReadOnlyList<ServerInfo> servers)
    {
        var sem = new SemaphoreSlim(64);
        var tasks = servers.Select(async s =>
        {
            await sem.WaitAsync();
            try
            {
                using var pinger = new System.Net.NetworkInformation.Ping();
                var reply = await pinger.SendPingAsync(s.Endpoint.Address, 2500);
                if (reply.Status == System.Net.NetworkInformation.IPStatus.Success && reply.RoundtripTime > 0)
                    Application.Current.Dispatcher.Invoke(() => s.Ping = (int)reply.RoundtripTime);
            }
            catch { /* ICMP commonly blocked too — give up cleanly */ }
            finally { sem.Release(); }
        }).ToArray();
        await Task.WhenAll(tasks);
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
        // Re-entrancy guard. RelayCommand doesn't auto-disable while running,
        // and v0.1.18's tab-activate auto-refresh path can fire concurrently
        // with the startup OnLoaded refresh — both Servers.Clear() before
        // either's data arrives, then both Servers.Add() the same results,
        // producing duplicates in the grid (v0.1.18 regression). Bail
        // silently if a refresh is already in flight.
        if (IsLoading) return;

        IsLoading = true;
        Status = "Loading...";
        Servers.Clear();

        // Track endpoints added during this refresh so we don't list a server
        // twice. Two real sources of duplication that this guards against:
        //   1. A BYES server that's also publicly discoverable on Steam — the
        //      BYES entry wins (loaded first, has IsByes=true and the proper
        //      modpack mapping); the public-discovery duplicate is skipped.
        //   2. Backend-side Steam Web API ↔ BattleMetrics overlap returning
        //      the same server twice in one response.
        var addedEndpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // Load modpack catalog up-front so we can auto-tag public servers.
            try
            {
                var modpacks = await _backend.GetModpacksAsync();
                _matcher.Load(modpacks);
            }
            catch { /* backend down — matcher just won't tag anything */ }

            // 1) BYES curated servers (pinned at top via the IsByes sort).
            // The /api/servers/byes endpoint enriches with cached gamedig
            // stats server-side; no A2S round-trip from the launcher needed.
            try
            {
                Status = "Loading BYES servers...";
                var byes = await _backend.GetByesServersAsync();
                foreach (var e in byes)
                {
                    if (!IPAddress.TryParse(e.Ip, out var ip)) continue;
                    // Honor admin-set query_port from /admin servers form. If
                    // the value matches gamePort+1 (the convention) or wasn't
                    // sent, leave ExplicitQueryPort null so ServerEndpoint
                    // computes the default.
                    ushort? qp = (e.QueryPort > 0 && e.QueryPort != e.Port + 1) ? (ushort)e.QueryPort : null;
                    var ep = new ServerEndpoint(ip, (ushort)e.Port, qp);
                    // Defensive dedup against backend repeats — admin shouldn't
                    // register the same server twice but if they did we won't
                    // show it twice.
                    if (!addedEndpoints.Add(ep.ToString())) continue;
                    var modpackIds = e.ResolveModpackIds();
                    var info = new ServerInfo
                    {
                        Endpoint = ep,
                        Name = e.Live?.Online == true ? e.Name : e.Name + "  (offline)",
                        Map = e.Live?.Map ?? "",
                        Gametype = ByesLauncher.Services.GameTypeDetector.Detect(
                            e.Name,
                            fallback: modpackIds.FirstOrDefault() ?? ""),
                        Players = e.Live?.Players ?? 0,
                        MaxPlayers = e.Live?.MaxPlayers ?? 0,
                        Ping = e.Live?.Ping ?? 0,
                        IsByes = true,
                        ModpackIds = modpackIds,
                    };
                    info.IsFavorite = _favorites.IsFavorite(info.Endpoint);
                    Servers.Add(info);
                }
            }
            catch (Exception ex)
            {
                Status = "BYES list unavailable: " + ex.Message + " — continuing with public servers.";
            }

            // 2) Public-server discovery via the backend's /api/servers/discover.
            // Replaces direct Steam-master-server UDP (Valve retired the public
            // hostname). Backend cascades Steam Web API → BattleMetrics with a
            // 60s fresh / 5min stale-while-revalidate cache.
            try
            {
                Status = "Discovering public servers...";
                var result = await _backend.GetDiscoveredServersAsync("arma2oa");
                foreach (var info in result.Servers)
                {
                    // Skip if a BYES entry (or a prior public entry from
                    // Steam ↔ BattleMetrics overlap) already covered this
                    // endpoint. BYES wins because it has IsByes=true and the
                    // admin-curated modpack mapping.
                    if (!addedEndpoints.Add(info.Endpoint.ToString())) continue;
                    info.IsFavorite = _favorites.IsFavorite(info.Endpoint);
                    // Public servers tag with at most one inferred modpack via
                    // admin-defined regex patterns — the matcher is single-shot
                    // and the first regex match wins.
                    if (info.ModpackIds.Count == 0)
                    {
                        var matched = _matcher.MatchFor(info);
                        if (!string.IsNullOrEmpty(matched))
                            info.ModpackIds = new List<string> { matched };
                    }
                    Servers.Add(info);
                }
                var src = string.IsNullOrEmpty(result.SourceDisplay) ? "" : $" · {result.SourceDisplay}";
                Status = $"{Servers.Count} servers loaded ({Servers.Count(s => s.IsByes)} BYES + {Servers.Count(s => !s.IsByes)} public){src}. Measuring pings…";
                RebuildFilterDropdowns();

                // Steam Web API doesn't measure latency. Fire off A2S probes
                // in the background and stream real ping values into the grid
                // as they arrive (ServerInfo is observable so the column
                // refreshes in place). Discovered servers only — BYES rows
                // already have ping from gamedig server-side.
                _ = EnrichPingsAsync(Servers.Where(s => !s.IsByes).ToList());
            }
            catch (Exception ex)
            {
                // BYES list still showed if it succeeded; just flag that public
                // discovery is down and let the user retry.
                Status = $"Public server discovery offline: {ex.Message}";
            }
        }
        catch (Exception ex) { Status = "Error: " + ex.Message; }
        finally
        {
            // Track timestamp regardless of success — failed refreshes still
            // count for the stale-check (we don't want to retry a failing
            // backend every 30s on every tab click).
            LastRefreshUtc = DateTime.UtcNow;
            IsLoading = false;
        }
    }

    /// Tab-activate hook: refresh only if the data is older than `staleness`
    /// AND we're not already loading. This is what MainWindow calls when
    /// the user switches into the Servers tab — keeps tab-flipping cheap
    /// while still giving fresh data after the user has been away.
    public async Task RefreshIfStaleAsync(TimeSpan staleness)
    {
        if (IsLoading) return;
        if (DateTime.UtcNow - LastRefreshUtc < staleness) return;
        await RefreshAsync();
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
