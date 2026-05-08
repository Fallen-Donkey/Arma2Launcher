using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Velopack;
using ByesLauncher.Services;
using ByesLauncher.ViewModels;

namespace ByesLauncher;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static string? PendingProtocolRequest { get; set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Velopack must run BEFORE any UI to handle install/update lifecycle hooks.
        // No-op when running outside an installed Velopack build (e.g. dev).
        VelopackApp.Build().Run();

        // Register byes:// scheme so the website can deep-link.
        ProtocolService.EnsureRegistered();

        // Capture any byes://... arg so MainWindow can act on it after construction.
        if (e.Args.Length > 0 && e.Args[0].StartsWith("byes://", StringComparison.OrdinalIgnoreCase))
            PendingProtocolRequest = e.Args[0];

        var services = new ServiceCollection();

        services.AddSingleton<ConfigService>();
        services.AddSingleton<BackendClient>();
        services.AddSingleton<A2sQuery>();
        services.AddSingleton<DownloadService>();
        services.AddSingleton<ProfileService>();
        services.AddSingleton<LaunchService>();
        services.AddSingleton<ModpackService>();
        services.AddSingleton<UpdateService>();
        services.AddSingleton<HistoryService>();
        services.AddSingleton<FavoritesService>();
        services.AddSingleton<ModpackMatcher>();
        services.AddSingleton<WindowsCompatService>();
        services.AddSingleton<ViewModels.ModpackJoinCoordinator>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<ServersViewModel>();
        services.AddSingleton<FavoritesViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<ModpacksViewModel>();
        services.AddSingleton<ProfilesViewModel>();
        services.AddSingleton<SettingsViewModel>();

        services.AddHttpClient();

        Services = services.BuildServiceProvider();

        base.OnStartup(e);
    }
}
