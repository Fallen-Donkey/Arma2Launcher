using Microsoft.Extensions.DependencyInjection;
using ByesLauncher.Services;
using ByesLauncher.ViewModels;

namespace ByesLauncher.Views;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<MainViewModel>();
        Loaded += OnLoaded;
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
                ModpackId = req.Modpack,
            };
        }

        if (match != null) await vm.Servers.JoinFromExternalAsync(match);
    }
}
