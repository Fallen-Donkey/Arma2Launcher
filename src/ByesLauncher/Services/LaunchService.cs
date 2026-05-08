using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using ByesLauncher.Models;

namespace ByesLauncher.Services;

public class LaunchService
{
    private readonly ConfigService _config;

    public LaunchService(ConfigService config) => _config = config;

    public Process Launch(ServerInfo? server, IEnumerable<string> activeMods, string? profileName, string? password = null)
    {
        var c = _config.Config;
        var root = c.Arma2OaPath;
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("Arma 2 path not configured.");

        var exe = Path.Combine(root, c.ExeName);
        if (!File.Exists(exe))
            throw new FileNotFoundException($"Launch exe not found: {exe}");

        var args = new List<string>();

        // Build the -mod load list. Convention used by every working DayZ-mod
        // launcher (DZSA, Maca, ArmA2Sync):
        //   1. `<arma2 base>` — Steam installs Arma 2 as a SEPARATE folder
        //      from Arma 2 OA. Chernarus etc. live in `<arma2>/AddOns/`. We
        //      detect the sibling Steam dir (most common) or fall back to
        //      the registry. Without this, addons that ref Chernarus
        //      (Sauerland, Taviana variants, every DayZ Mod build) fail.
        //   2. `Expansion`    — Arma 2 OA's expansion content (relative to
        //      the working dir, which is the OA install root).
        //   3. user-selected @ mod folders.
        // Anything missing on disk is silently skipped — degrades to "load
        // what you can" rather than refusing to launch.
        var modOrder = new List<string>();
        var arma2Base = FindArma2BasePath(root);
        if (!string.IsNullOrEmpty(arma2Base)) modOrder.Add(arma2Base);
        if (Directory.Exists(Path.Combine(root, "Expansion"))) modOrder.Add("Expansion");

        // Drop @*server* mods — those are packaged for the dedicated server
        // (mission frameworks, anti-hack DLLs) and either do nothing or
        // hard-error when loaded into the client. Same convention as Maca's.
        var clientMods = activeMods
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Where(m => m.IndexOf("server", StringComparison.OrdinalIgnoreCase) < 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        modOrder.AddRange(clientMods);

        if (modOrder.Count > 0) args.Add($"-mod={string.Join(";", modOrder)}");
        if (!string.IsNullOrWhiteSpace(profileName)) args.Add($"-name={profileName}");
        if (server != null)
        {
            args.Add($"-connect={server.Endpoint.Address}");
            args.Add($"-port={server.Endpoint.GamePort}");
            if (!string.IsNullOrEmpty(password)) args.Add($"-password={password}");
        }

        if (c.NoSplash) args.Add("-nosplash");
        if (c.SkipIntro) args.Add("-skipIntro");
        if (c.WorldEmpty) args.Add("-world=empty");
        if (c.NoPause) args.Add("-noPause");
        if (c.ShowScriptErrors) args.Add("-showScriptErrors");
        if (c.FilePatching) args.Add("-filePatching");
        if (c.EnableHT) args.Add("-enableHT");
        if (c.Windowed) args.Add("-window");
        // Mandatory on Windows 11 24H2 to avoid death/disconnect crash;
        // harmless on other Windows versions. Default-on in AppConfig.
        if (c.NoD3D9Ex) args.Add("-nod3d9ex");
        if (c.MaxMem is int mm && mm > 0) args.Add($"-maxMem={mm}");
        if (c.CpuCount is int cc && cc > 0) args.Add($"-cpuCount={cc}");
        if (c.ExThreads is int et && et >= 0) args.Add($"-exThreads={et}");
        if (!string.IsNullOrWhiteSpace(c.Malloc) && !string.Equals(c.Malloc, "system", StringComparison.OrdinalIgnoreCase))
            args.Add($"-malloc={c.Malloc}");

        if (!string.IsNullOrWhiteSpace(c.CustomArgs))
            args.AddRange(c.CustomArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var argsString = string.Join(" ", args.Select(QuoteArg));

        // Steam-protocol path is opt-in (Settings → "Launch via Steam
        // protocol"). When enabled:
        //   - Steam handles BattlEye injection + Combined Operations
        //     setup; no UAC prompt every join.
        //   - Steam tracks playtime in the user's library.
        // Caveat: relies on the user's per-game Steam launch-option
        // default for appid 33930. If they've set it to the non-BE
        // option, BE-protected servers refuse the connection — that's
        // why this is opt-in until validated.
        // When disabled (default), we use direct exe + UAC (functional,
        // proven path). If the Steam invocation throws, fall back to
        // direct exe so the launcher never fails to start the game just
        // because Steam is misbehaving.
        if (c.UseSteamLaunch && TryStartViaSteam(argsString, out var steamProc))
            return steamProc!;

        // Direct-exe path (default, or Steam-protocol fallback). Shell-
        // execute triggers the UAC prompt for arma2oa_be.exe.
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = root,
            Arguments = argsString,
            UseShellExecute = true,
        };

        try
        {
            return Process.Start(psi) ?? throw new InvalidOperationException("Failed to start Arma 2.");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // 1223 = ERROR_CANCELLED — user said No to the UAC prompt.
            throw new InvalidOperationException(
                "Launch cancelled — BattlEye needs admin rights. " +
                "If you want to skip BattlEye (single-player or non-BE servers), " +
                "uncheck \"Use BattlEye\" in Settings.");
        }
    }

    /// Try launching via Steam's `steam://run/<appid>` URL handler.
    /// Returns true if the URL handler started; the returned Process is
    /// the Steam invocation, not the actual game (Steam's own bootstrap
    /// is what we trigger). 33930 = Arma 2: Operation Arrowhead app id.
    private static bool TryStartViaSteam(string argsString, out Process? proc)
    {
        proc = null;
        try
        {
            var url = $"steam://run/33930//{Uri.EscapeDataString(argsString)}/";
            proc = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return proc != null;
        }
        catch
        {
            return false;
        }
    }

    /// Quote argument if it contains spaces, escaping any embedded quotes.
    /// Used because UseShellExecute=true forces a single Arguments string.
    private static string QuoteArg(string a)
    {
        if (string.IsNullOrEmpty(a)) return "\"\"";
        // Already quoted? leave it.
        if (a.StartsWith('"') && a.EndsWith('"')) return a;
        if (!a.Contains(' ') && !a.Contains('"') && !a.Contains('\t')) return a;
        return "\"" + a.Replace("\"", "\\\"") + "\"";
    }

    /// Locate Arma 2 base-game install (separate from Arma 2 OA). Steam
    /// Combined Operations puts both games in the same library but as
    /// sibling folders, so `<oa-root>/../Arma 2/` is the common case.
    /// Falls back to the registry key Bohemia writes on install.
    /// Returns null if Arma 2 base isn't present — caller treats it as
    /// "Standalone OA only," in which case Chernarus-dependent mods will
    /// still error (no launcher fix possible without the base game).
    private static string? FindArma2BasePath(string oaRoot)
    {
        // 1. Sibling Steam install — covers ~all combined-ops users.
        try
        {
            var parent = Directory.GetParent(oaRoot)?.FullName;
            if (!string.IsNullOrEmpty(parent))
            {
                var sibling = Path.Combine(parent, "Arma 2");
                if (Directory.Exists(Path.Combine(sibling, "AddOns")))
                    return sibling;
            }
        }
        catch { /* IO / permission */ }

        // 2. Registry fallback — Bohemia's installer writes here. The 32-bit
        //    Arma 2 installer lives under WOW6432Node on 64-bit Windows.
        foreach (var subkey in new[] {
            @"SOFTWARE\WOW6432Node\Bohemia Interactive\ArmA 2",
            @"SOFTWARE\Bohemia Interactive\ArmA 2",
        })
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(subkey);
                if (key?.GetValue("MAIN") is string main && Directory.Exists(Path.Combine(main, "AddOns")))
                    return main;
            }
            catch { /* registry access might be denied */ }
        }

        return null;
    }

    /// Launch DayZ Standalone via the Steam protocol (handles BattlEye/Steam auth automatically).
    public void LaunchDayZStandalone(ServerInfo? server)
    {
        var args = "-nolauncher -world=empty";
        if (server != null) args += $" \"-connect={server.Endpoint.Address}\" \"-port={server.Endpoint.GamePort}\"";
        var url = $"steam://run/221100//{Uri.EscapeDataString(args)}/";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
