using System.Windows;

namespace ByesLauncher.Services;

/// Windows-version detection + one-time compatibility notices for issues
/// the launcher itself can't auto-fix. Currently focused on Windows 11
/// 24H2 (build 26100+) which broke two unrelated things in Arma 2 OA:
///
///   Issue A — BattlEye Error 577 (HVCI rejects the legacy BE driver).
///             Can't fix from the launcher; user has to replace BE
///             service files manually with community-patched versions.
///   Issue B — D3D9Ex Reset() crash on death/disconnect/abort.
///             Auto-fixed by `-nod3d9ex` (default-on in AppConfig).
///
/// The notice handles Issue A education; Issue B is silent because we
/// already apply the fix.
public class WindowsCompatService
{
    private readonly ConfigService _config;

    public WindowsCompatService(ConfigService config) => _config = config;

    /// True iff running on Windows 11 24H2 or later (build >= 26100).
    public static bool IsWindows11_24H2OrLater =>
        Environment.OSVersion.Platform == PlatformID.Win32NT &&
        Environment.OSVersion.Version.Build >= 26100;

    /// Show the one-time notice if applicable. Safe to call on every
    /// app start; flag in AppConfig short-circuits repeats.
    public void ShowNoticeIfApplicable(Window? owner)
    {
        if (!IsWindows11_24H2OrLater) return;
        if (_config.Config.Win11Notice24H2Shown) return;

        // Short URL we control — easier on the eyes than the full Discord
        // channel link, and rerouteable without shipping a launcher update
        // if the channel ever moves. Requires a one-line redirect on the
        // byes.nz nginx config: `/win11fix` → Discord channel URL.
        const string ByesWin11FixUrl = "https://byes.nz/win11fix";

        var msg =
            "Windows 11 24H2 detected — heads up about two known Arma 2 OA issues:\n\n" +
            "✓ DEATH / DISCONNECT CRASH (auto-fixed)\n" +
            "  The launcher applies -nod3d9ex by default, which avoids the " +
            "D3D9Ex Reset() crash that 24H2 introduced. Nothing for you to do.\n\n" +
            "⚠ BATTLEYE ERROR 577 (manual fix needed)\n" +
            "  24H2's HVCI / Memory Integrity rejects BattlEye's legacy " +
            "kernel driver. If the game fails to launch with a BE error, " +
            "grab the community fix package and replace these three files " +
            "in your Arma 2 OA folder:\n\n" +
            "    BattlEye\\BEService.exe\n" +
            "    BattlEye\\BEService_x64.exe\n" +
            "    ArmA2OA_BE.exe\n\n" +
            "The fix package lives in the Backyard Esports Discord:\n\n" +
            "    " + ByesWin11FixUrl + "\n\n" +
            "Click \"Yes\" to open it now, or \"No\" to dismiss and apply " +
            "later. Either way, we'll only show this notice once.\n\n" +
            "Last-resort workaround: disable Memory Integrity in Windows " +
            "Security → Device Security → Core Isolation. Replacing the " +
            "files is preferred (keeps your kernel-level malware protection " +
            "intact).";

        var result = MessageBox.Show(owner, msg, "Windows 11 24H2 — Arma 2 OA compatibility",
            MessageBoxButton.YesNo, MessageBoxImage.Information);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ByesWin11FixUrl)
                {
                    UseShellExecute = true,
                });
            }
            catch { /* user can navigate manually if browser launch fails */ }
        }

        _config.Config.Win11Notice24H2Shown = true;
        _config.Save();
    }
}
