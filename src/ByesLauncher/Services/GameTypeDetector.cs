namespace ByesLauncher.Services;

/// Steam Web API returns Arma 2 OA servers with `gametype` set to the raw
/// BI A2S tag string (`bt,r164,n131129,s7,i1,mf,lf,vt,dt,tc`) — version /
/// flag info that isn't useful in a server-browser column. The mod the
/// server is running (Epoch / Origins / Overpoch / etc.) lives in the
/// server NAME instead. This helper extracts a clean mod label so the
/// Gametype column and filter dropdown show useful values.
///
/// Order matters: more specific patterns (Overpoch, DayZ Epoch) come before
/// less specific ones (Epoch, DayZ) so substring matches don't fire wrong.
public static class GameTypeDetector
{
    private static readonly (string Pattern, string Display)[] Rules =
    {
        ("overpoch",       "Overpoch"),
        ("dayz epoch",     "Epoch"),
        ("epoch",          "Epoch"),
        ("origins",        "Origins"),
        ("namalsk",        "Namalsk"),
        ("breaking point", "Breaking Point"),
        ("breakingpoint",  "Breaking Point"),
        ("dayzrp",         "DayZRP"),
        ("dayz rp",        "DayZRP"),
        ("wasteland",      "Wasteland"),
        ("battle royale",  "Battle Royale"),
        ("chernarus br",   "Chernarus BR"),
        ("dayz",           "DayZ"),
        ("life",           "Life"),       // Altis Life-style RP
        ("king of the hill", "KOTH"),
    };

    public static string Detect(string serverName, string fallback = "")
    {
        if (string.IsNullOrWhiteSpace(serverName)) return fallback;
        var lower = serverName.ToLowerInvariant();
        foreach (var (pattern, display) in Rules)
            if (lower.Contains(pattern)) return display;
        return fallback;
    }
}
