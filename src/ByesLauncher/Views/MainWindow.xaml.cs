using Microsoft.Extensions.DependencyInjection;
using ByesLauncher.Services;
using ByesLauncher.ViewModels;

namespace ByesLauncher.Views;

public partial class MainWindow
{
    /// Threshold: if the user re-enters the Servers tab and the last refresh
    /// was more than this long ago, kick a background refresh. Anything
    /// shorter spam-refreshes when the user is tab-flipping; anything longer
    /// makes "I came back from settings, re-show ping" feel sluggish.
    private static readonly System.TimeSpan TabActivateStaleness = System.TimeSpan.FromSeconds(30);

    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<MainViewModel>();
        Loaded += OnLoaded;
    }

    /// Tab-change hook. Currently only the Servers tab triggers a stale-refresh —
    /// Favorites/History both view persisted/derived data and don't need it,
    /// and Modpacks/Profiles/Settings are static-config. Adding more tab
    /// hooks later is just another `if (e.AddedItems[0] == FooTab)` clause.
    private void OnTabChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // SelectionChanged also fires for nested controls (DataGrid selection
        // bubbles up through the visual tree). Filter to only the outer
        // TabControl's own change so we don't refresh on every grid click.
        if (!ReferenceEquals(e.OriginalSource, MainTabs)) return;
        if (e.AddedItems.Count == 0) return;

        if (e.AddedItems[0] == ServersTab && DataContext is MainViewModel vm)
        {
            // Fire-and-forget — RefreshIfStaleAsync handles the in-progress
            // and freshness gates internally. UI thread isn't blocked.
            _ = vm.Servers.RefreshIfStaleAsync(TabActivateStaleness);
        }
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // Background update check — non-blocking, fails silently in dev.
        var updater = App.Services.GetRequiredService<UpdateService>();
        _ = updater.CheckSilentlyAsync();

        // First-launch wizard if Arma path missing or never completed setup.
        var config = App.Services.GetRequiredService<ByesLauncher.Services.ConfigService>();
        if (!config.Config.FirstLaunchCompleted || string.IsNullOrWhiteSpace(config.Config.Arma2OaPath))
        {
            var wiz = new Dialogs.FirstLaunchWizard { Owner = this };
            wiz.ShowDialog();
        }

        // After first-launch flow, show Windows 11 24H2 compatibility
        // notice if applicable (one-time; flag persists in config).
        var compat = App.Services.GetRequiredService<ByesLauncher.Services.WindowsCompatService>();
        compat.ShowNoticeIfApplicable(this);

        // Auto-load BYES servers + master list on startup so the launcher feels alive.
        var vm = (ViewModels.MainViewModel)DataContext;
        _ = vm.Servers.RefreshCommand.ExecuteAsync(null);

        // Honor any byes:// URL the launcher was started with.
        if (App.PendingProtocolRequest is { } uri)
        {
            App.PendingProtocolRequest = null;
            await HandleProtocolAsync(uri);
        }
    }

    /// byes://connect?ip=...&port=...&modpack=...
    /// Wait for the server list to arrive, find a match, kick off the standard join flow.
    private async System.Threading.Tasks.Task HandleProtocolAsync(string uri)
    {
        var req = ByesLauncher.Services.ProtocolService.Parse(uri);
        if (req == null || !req.Action.Equals("connect", System.StringComparison.OrdinalIgnoreCase)) return;
        if (string.IsNullOrWhiteSpace(req.Ip) || req.Port is null) return;

        var vm = (ViewModels.MainViewModel)DataContext;

        // Wait up to 12 seconds for the server list to populate (it normally takes a few).
        ByesLauncher.Models.ServerInfo? match = null;
        for (var i = 0; i < 24 && match == null; i++)
        {
            match = vm.Servers.FindByEndpoint(req.Ip!, req.Port.Value);
            if (match == null) await System.Threading.Tasks.Task.Delay(500);
        }

        // Synthesize a server entry if discovery missed it (private server, not in master).
        if (match == null && System.Net.IPAddress.TryParse(req.Ip, out var ipAddr))
        {
            match = new ByesLauncher.Models.ServerInfo
            {
                Endpoint = new ByesLauncher.Models.ServerEndpoint(ipAddr, (ushort)req.Port.Value),
                Name = $"Direct connect ({req.Ip}:{req.Port})",
                ModpackIds = string.IsNullOrEmpty(req.Modpack) ? new() : new() { req.Modpack! },
            };
        }

        if (match != null) await vm.Servers.JoinFromExternalAsync(match);
    }
}
