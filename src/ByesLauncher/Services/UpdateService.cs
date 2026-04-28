using System.Windows;
using Velopack;
using Velopack.Sources;

namespace ByesLauncher.Services;

/// Velopack-based auto-updater. The update feed is a static directory served
/// by the BYES site (e.g. https://byes.nz/launcher/updates) that
/// contains releases.win.json + .nupkg packages produced by `vpk pack`.
///
/// Flow:
///  - On startup, App.OnStartup calls VelopackApp.Build().Run() FIRST so first-run
///    install hooks fire correctly. Then this service is constructed by DI.
///  - At idle (e.g. 5s after the window appears), CheckSilentlyAsync runs.
///  - If an update is available we download it in the background and prompt
///    the user to restart at their convenience.
public class UpdateService
{
    private readonly ConfigService _config;

    public UpdateService(ConfigService config) => _config = config;

    public string FeedUrl => _config.Config.BackendUrl.TrimEnd('/') + "/launcher/updates";

    public async Task CheckSilentlyAsync()
    {
        try
        {
            var mgr = new UpdateManager(new SimpleWebSource(FeedUrl));
            // IsInstalled returns false in dev (`dotnet run`) — short-circuits
            // any Velopack RPC. Once shipped via `vpk pack`, this reads the
            // installed marker from the AppData folder.
            if (!mgr.IsInstalled) return;

            var update = await mgr.CheckForUpdatesAsync();
            if (update == null) return;

            await mgr.DownloadUpdatesAsync(update);

            // Don't restart blindly — show a small toast / dialog asking user.
            var result = MessageBox.Show(
                $"A new launcher version ({update.TargetFullRelease.Version}) has been downloaded.\n\nRestart now to apply?",
                "Update ready",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
                mgr.ApplyUpdatesAndRestart(update);
        }
        catch (Exception ex)
        {
            // Updater failures should never crash the app — log and continue.
            System.Diagnostics.Debug.WriteLine($"[update] check failed: {ex.Message}");
        }
    }
}
