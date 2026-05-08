namespace ByesLauncher.Models;

public class AppConfig
{
    public string BackendUrl { get; set; } = "https://byes.nz";
    public string DiscordUrl { get; set; } = "https://discord.gg/Kng9hBA9V8";
    public string SupportUrl { get; set; } = "https://byes.nz/launcher";
    public string DonateUrl { get; set; } = "";
    public string Arma2OaPath { get; set; } = "";
    public string DayZStandalonePath { get; set; } = "";
    public string ActiveProfile { get; set; } = "";
    public bool UseBattleEye { get; set; } = true;
    public int MaxParallelDownloads { get; set; } = 6;
    public List<string> FavoriteServers { get; set; } = new();
    public List<HistoryEntry> History { get; set; } = new();
    public bool FirstLaunchCompleted { get; set; } = false;
    public Dictionary<string, List<string>> ProfileModOverrides { get; set; } = new();

    // Arma 2 OA launch parameters (mirrors what Maca's launcher and Arma3Launcher expose).
    public bool NoSplash { get; set; } = true;
    public bool SkipIntro { get; set; } = true;
    public bool WorldEmpty { get; set; } = true;
    public bool NoPause { get; set; } = false;
    public bool ShowScriptErrors { get; set; } = false;
    public bool FilePatching { get; set; } = false;
    public bool EnableHT { get; set; } = true;
    public bool Windowed { get; set; } = false;

    /// `-nod3d9ex` — force classic D3D9 instead of D3D9Ex. Required on
    /// Windows 11 24H2 to prevent the engine crashing when the device
    /// resets (player death, server disconnect, Abort from ESC menu).
    /// 24H2 broke the D3D9Ex Reset() codepath via DWM's fullscreen-
    /// optimisation compat layer; classic D3D9's Reset() doesn't go
    /// through that path. No-op on Windows 10 / 11 23H2 and earlier
    /// — fully supported on every GPU that runs Arma 2. Defaults ON
    /// because the cost is nil and the fix is critical for 24H2.
    public bool NoD3D9Ex { get; set; } = true;

    /// We've shown the one-time Windows 11 24H2 compatibility notice
    /// (covers BattlEye Error 577 — community fix required, can't
    /// auto-apply). Don't repeat on every startup.
    public bool Win11Notice24H2Shown { get; set; } = false;

    /// Opt-in: launch via the Steam URL handler (`steam://run/33930//<args>`)
    /// instead of starting arma2oa_be.exe directly. Eliminates the per-launch
    /// UAC prompt because Steam supplies its own elevation context.
    /// Trade-off: relies on the user's Steam launch-options default for app
    /// 33930 — if they've changed that to the non-BE option, BattlEye-
    /// protected servers will reject the connection. Defaults off until
    /// validated by power users in the field; flip on once enough installs
    /// have road-tested the path.
    public bool UseSteamLaunch { get; set; } = false;

    public int? MaxMem { get; set; } = null;        // MB
    public int? CpuCount { get; set; } = null;
    public int? ExThreads { get; set; } = null;     // 0/1/3/5/7
    public string Malloc { get; set; } = "system";  // system | jemalloc | tbb4malloc_bi | tcmalloc
    public string CustomArgs { get; set; } = "";

    public string ExeName => UseBattleEye ? "arma2oa_be.exe" : "arma2oa.exe";
}

public class HistoryEntry
{
    public required string Endpoint { get; set; }   // "ip:port"
    public required string Name { get; set; }
    public string Map { get; set; } = "";
    /// Modpack ids in load_order. Existing config files written by the old
    /// single-modpack code path won't have this field — defaults to empty
    /// and the entry just rejoins without auto-sync (acceptable graceful
    /// degradation for past history).
    public List<string> ModpackIds { get; set; } = new();
    public DateTime JoinedAt { get; set; }
}
