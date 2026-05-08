namespace ByesLauncher.Models;

public class Modpack
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Version { get; set; }
    public long TotalSize { get; set; }
    public List<string> Mods { get; set; } = new();
    public string? Description { get; set; }
    /// Regex patterns (case-insensitive) the launcher applies against each
    /// public server's Name + Gametype + Map. Matching servers are tagged
    /// with this modpack so Join auto-syncs the right files.
    public List<string> MatchPatterns { get; set; } = new();
}

public class Manifest
{
    public required string ModpackId { get; set; }
    public required string Version { get; set; }
    public List<ManifestFile> Files { get; set; } = new();
}

public class ManifestFile
{
    public required string Path { get; set; }
    public required string Sha256 { get; set; }
    public long Size { get; set; }
    /// Optional fallback HTTP URLs the launcher tries if the primary BYES blob URL fails.
    /// Hash check is still enforced against the downloaded bytes — a tampered mirror is rejected.
    public List<string>? Mirrors { get; set; }
}

public class ByesServerEntry
{
    public required string Name { get; set; }
    public required string Ip { get; set; }
    public required int Port { get; set; }
    /// Authoritative query port from the website's `game_servers.query_port`
    /// admin-set field (in the wire since the multi-modpack work). When 0
    /// or missing, fall back to gamePort+1 via ServerEndpoint's default.
    [System.Text.Json.Serialization.JsonPropertyName("queryPort")]
    public int QueryPort { get; set; }
    public string? Description { get; set; }
    public LiveStats? Live { get; set; }

    /// New shape (post-multi-modpack rollout, breaking-contract change).
    /// Server may require N modpacks loaded in `LoadOrder` ascending order.
    /// Empty array = no modpack requirement.
    [System.Text.Json.Serialization.JsonPropertyName("modpacks")]
    public List<ModpackRef> Modpacks { get; set; } = new();

    /// Legacy single-modpack shape. Kept only so v0.1.2 launchers that hit
    /// a backend still serving the old shape don't lose modpack info during
    /// the transition window. Drop in v0.1.3 once the website rollout is
    /// confirmed and old clients are gone.
    [System.Text.Json.Serialization.JsonPropertyName("modpackId")]
    public string? LegacyModpackId { get; set; }

    /// Materialize the modpack id list from whichever shape the backend
    /// served, sorted by load order. Empty list → no modpack required.
    public List<string> ResolveModpackIds()
    {
        if (Modpacks.Count > 0)
            return Modpacks.OrderBy(m => m.LoadOrder).Select(m => m.Id).ToList();
        return string.IsNullOrEmpty(LegacyModpackId) ? new() : new() { LegacyModpackId };
    }
}

public class ModpackRef
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public required string Id { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("loadOrder")]
    public int LoadOrder { get; set; }
}

public class LiveStats
{
    public bool Online { get; set; }
    public int Players { get; set; }
    public int MaxPlayers { get; set; }
    public string? Map { get; set; }
    public int? Ping { get; set; }
}
