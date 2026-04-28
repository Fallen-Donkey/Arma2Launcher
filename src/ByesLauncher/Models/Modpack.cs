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
    public string? ModpackId { get; set; }
    public string? Description { get; set; }
    public LiveStats? Live { get; set; }
}

public class LiveStats
{
    public bool Online { get; set; }
    public int Players { get; set; }
    public int MaxPlayers { get; set; }
    public string? Map { get; set; }
    public int? Ping { get; set; }
}
