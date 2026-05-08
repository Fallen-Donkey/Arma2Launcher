using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using ByesLauncher.Models;
using ByesLauncher.Services;
using ByesLauncher.Views.Dialogs;

namespace ByesLauncher.ViewModels;

/// Shared "join server" flow: ask password if needed → resolve modpack → sync via
/// the download progress dialog → launch. Used by both the manual Join button
/// and the byes:// protocol auto-join path.
///
/// Singleton — every tab's "Join" button funnels through the same instance, so
/// IsJoining drives button state across Servers / Favorites / History at once.
/// That also gives us a free re-entrancy guard against double-clicking Join
/// while the modpack-sync dialog is opening.
public partial class ModpackJoinCoordinator : ObservableObject
{
    private readonly BackendClient _backend;
    private readonly DownloadService _downloads;
    private readonly LaunchService _launch;
    private readonly ConfigService _config;
    private readonly HistoryService _history;

    /// True from the moment Join is invoked until both the join flow has
    /// completed AND a 5s minimum-visible window has elapsed (whichever is
    /// later). The 5s floor is purely for user feedback — fast cache-hit
    /// joins would otherwise flicker the "JOINING…" label too quickly to
    /// read, leaving the user unsure whether their click registered.
    [ObservableProperty] private bool isJoining;

    /// How long the Join button stays in the "JOINING…" state at minimum,
    /// even if the actual launch finishes faster. Tuned to be readable
    /// without feeling sluggish.
    private static readonly TimeSpan MinJoiningVisible = TimeSpan.FromSeconds(5);

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
        // Re-entrancy guard. UI buttons are also bound to !IsJoining, but a
        // determined user (or a byes:// protocol click) can race past the
        // visual disable. Bail silently rather than queue a second launch.
        if (IsJoining) return;
        IsJoining = true;
        var minimumVisible = Task.Delay(MinJoiningVisible);
        try
        {
            await JoinInternalAsync(server);
        }
        finally
        {
            // Hold the "JOINING…" state for at least MinJoiningVisible so the
            // user sees the feedback even on a fast-path cache-hit launch.
            try { await minimumVisible; } catch { /* shutdown */ }
            IsJoining = false;
        }
    }

    private async Task JoinInternalAsync(ServerInfo server)
    {
        var mods = new List<string>();

        // 0. No modpack matched — server requires mods we don't manage. Tell
        // the user honestly rather than silently launching with no `-mod=`
        // and getting them BattlEye-kicked at the connect screen. They can
        // bail (recommended) or proceed if they've installed the mods
        // manually.
        if (!server.IsByes && server.ModpackIds.Count == 0 && ServerLikelyNeedsMods(server))
        {
            var detected = !string.IsNullOrWhiteSpace(server.Gametype) ? server.Gametype : "an unknown mod";
            var msg =
                $"This server reports it's running {detected}.\n\n" +
                "BYES doesn't have a modpack registered that matches it, " +
                "so the launcher can't auto-install the required files. " +
                "Joining without them will get you kicked by BattlEye.\n\n" +
                "If you've already installed the right mods manually (e.g. " +
                "from Maca's launcher or Steam Workshop), click \"Continue\" " +
                "and we'll launch into the server with whatever you have.\n\n" +
                "Otherwise click \"Cancel\" and pick a BYES-supported server.";
            var result = MessageBox.Show(msg, "Mods needed",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (result != MessageBoxResult.OK) return;
            // Falls through to launch with no synced modpack — user took the risk.
        }

        // 1. Modpack(s) first — must be in place before BattlEye starts checking.
        // The list is already in load_order; later entries override earlier
        // ones on file-path collisions, mirroring Arma's `-mod=` precedence.
        if (server.ModpackIds.Count > 0)
        {
            var manifests = new List<Manifest>();
            foreach (var packId in server.ModpackIds)
            {
                Manifest? m;
                try { m = await _backend.GetManifestAsync(packId); }
                catch (Exception ex)
                {
                    MessageBox.Show($"Couldn't fetch manifest for '{packId}':\n\n{ex.Message}",
                        "Sync failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (m == null)
                {
                    MessageBox.Show($"Modpack '{packId}' not found on backend.",
                        "Sync failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                manifests.Add(m);
            }

            var merged = MergeManifests(server.ModpackIds, manifests);

            // Fast-path: if every file is already on disk at the expected
            // size, skip the dialog entirely and go straight to launch.
            // Trust-by-default — saves the 5–10s hash sweep on every Join
            // when nothing has changed since last play. If a file's
            // corrupted with the right size, Arma will error at game start
            // and the user can manually re-sync from the Modpacks tab.
            if (!_downloads.AllFilesLikelyPresent(merged))
            {
                var headline = manifests.Count == 1
                    ? $"{server.Name} → {server.ModpackIds[0]}"
                    : $"{server.Name} → {merged.Files.Count} files from {manifests.Count} modpacks";

                var dlg = new DownloadProgressDialog(_downloads, merged, headline)
                {
                    Owner = Application.Current.MainWindow
                };
                var ok = dlg.ShowDialog() == true && dlg.Succeeded;
                if (!ok) return; // user cancelled or aborted
            }

            // Build the `-mod=` list: each modpack's @ folders, in load order,
            // deduped (first occurrence kept — order matters for Arma).
            var modSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in manifests)
                foreach (var folder in ModpackService.ModsInManifest(m))
                    if (modSet.Add(folder)) mods.Add(folder);
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

    /// Heuristic: does this server look like it requires mods we'd need
    /// to install? Vanilla Arma 2 OA / OA-only servers don't need any
    /// extra mods, so we don't want to nag the user with the "mods needed"
    /// dialog when they're trying to join a vanilla coop server. We
    /// conservatively flag a server as "needs mods" when our gametype
    /// detector classified it as a known mod variant (Epoch, Origins,
    /// Overpoch, etc.) — those genuinely require client mods.
    private static bool ServerLikelyNeedsMods(ServerInfo server)
    {
        var gt = (server.Gametype ?? "").Trim();
        if (string.IsNullOrEmpty(gt)) return false;
        // Known-mod gametypes our detector emits. "Arma 2" is the fallback
        // for unrecognized — that's a vanilla server, no mods needed.
        var modGametypes = new[]
        {
            "Epoch", "Overpoch", "Origins", "Namalsk", "Breaking Point",
            "DayZRP", "Wasteland", "DayZ", "Battle Royale", "Chernarus BR",
        };
        return modGametypes.Any(m => gt.Equals(m, StringComparison.OrdinalIgnoreCase));
    }

    /// Merge N manifests in load_order into a single virtual manifest the
    /// download dialog can sync as one pass. Conflict policy: later modpack
    /// wins on duplicate paths (mirrors Arma's `-mod=` later-wins precedence
    /// for both file resolution and the launch arg list).
    private static Manifest MergeManifests(IReadOnlyList<string> orderedIds, IReadOnlyList<Manifest> manifests)
    {
        var byPath = new Dictionary<string, ManifestFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in manifests)
            foreach (var f in m.Files)
                byPath[f.Path] = f;          // later assignment wins
        return new Manifest
        {
            ModpackId = string.Join("+", orderedIds),
            Version = "merged",
            Files = byPath.Values.ToList(),
        };
    }
}
