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
    public string? ModpackId { get; set; }
    public DateTime JoinedAt { get; set; }
}
