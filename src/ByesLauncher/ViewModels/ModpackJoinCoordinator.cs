using System.Windows;
using ByesLauncher.Models;
using ByesLauncher.Services;
using ByesLauncher.Views.Dialogs;

namespace ByesLauncher.ViewModels;

/// Shared "join server" flow: ask password if needed → resolve modpack → sync via
/// the download progress dialog → launch. Used by both the manual Join button
/// and the byes:// protocol auto-join path.
public class ModpackJoinCoordinator
{
    private readonly BackendClient _backend;
    private readonly DownloadService _downloads;
    private readonly LaunchService _launch;
    private readonly ConfigService _config;
    private readonly HistoryService _history;

    public ModpackJoinCoordinator(BackendClient backend, DownloadService downloads, LaunchService launch,
                                  ConfigService config, HistoryService history)
    {
        _backend = backend;
        _downloads = downloads;
        _launch = launch;
        _config = config;
        _history = history;
    }

    public async Task JoinAsync(ServerInfo server)
    {
        var mods = new List<string>();

        // 1. Sync modpack first (if any) so the install is in place before BattlEye starts checking.
        if (!string.IsNullOrWhiteSpace(server.ModpackId))
        {
            Manifest? manifest;
            try { manifest = await _backend.GetManifestAsync(server.ModpackId!); }
            catch (Exception ex)
            {
                MessageBox.Show($"Couldn't fetch modpack manifest:\n\n{ex.Message}", "Sync failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (manifest == null)
            {
                MessageBox.Show($"Modpack '{server.ModpackId}' not found on backend.",
                    "Sync failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dlg = new DownloadProgressDialog(_downloads, manifest, $"{server.Name} → {server.ModpackId}")
            {
                Owner = Application.Current.MainWindow
            };
            var ok = dlg.ShowDialog() == true && dlg.Succeeded;
            if (!ok) return; // user cancelled or aborted

            mods.AddRange(ModpackService.ModsInManifest(manifest));
        }

        // 2. Per-profile mod overrides
        var profile = string.IsNullOrWhiteSpace(_config.Config.ActiveProfile) ? null : _config.Config.ActiveProfile;
        if (profile != null && _config.Config.ProfileModOverrides.TryGetValue(profile, out var extra))
            mods.AddRange(extra);

        // 3. Password if the server is locked
        string? password = null;
        if (server.PasswordProtected)
        {
            var pwDlg = new PasswordPromptDialog(server.Name)
            {
                Owner = Application.Current.MainWindow
            };
            if (pwDlg.ShowDialog() != true) return; // cancelled
            password = pwDlg.Password;
        }

        // 4. Record in history before launching so a crash still leaves a breadcrumb.
        _history.Record(server);

        // 5. Launch
        try { _launch.Launch(server, mods, profile, password); }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Launch failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
