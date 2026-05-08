using System.Text.Json.Serialization;

namespace ByesLauncher.Models;

/// JSON shape returned by the BYES site's /api/servers/discover endpoint.
/// The endpoint cascades Steam Web API → BattleMetrics on failure (and
/// stale-while-revalidate caches the last good response), so the launcher
/// only needs to consume this one schema.
public class DiscoveryResponse
{
    [JsonPropertyName("servers")]     public List<DiscoveredServerDto> Servers { get; set; } = new();
    [JsonPropertyName("count")]       public int Count { get; set; }
    [JsonPropertyName("generatedAt")] public long GeneratedAt { get; set; }
    /// Top-level source identifier: "steam" | "battlemetrics" | "steam-stale"
    /// | "battlemetrics-stale" | "none" | "error". Lets the launcher tell the
    /// user when they're looking at fresh vs stale-while-revalidate data.
    [JsonPropertyName("source")]      public string? Source { get; set; }
    /// True when the response came from the backend's 60s in-memory cache
    /// rather than a fresh upstream call.
    [JsonPropertyName("cached")]      public bool Cached { get; set; }
}

public class DiscoveredServerDto
{
    [JsonPropertyName("ip")]                public string Ip { get; set; } = "";
    [JsonPropertyName("port")]              public int Port { get; set; }
    [JsonPropertyName("queryPort")]         public int QueryPort { get; set; }
    [JsonPropertyName("name")]              public string Name { get; set; } = "";
    [JsonPropertyName("map")]               public string Map { get; set; } = "";
    [JsonPropertyName("gametype")]          public string Gametype { get; set; } = "";
    [JsonPropertyName("players")]           public int Players { get; set; }
    [JsonPropertyName("maxPlayers")]        public int MaxPlayers { get; set; }
    [JsonPropertyName("ping")]              public int? Ping { get; set; }
    [JsonPropertyName("version")]           public string Version { get; set; } = "";
    [JsonPropertyName("passwordProtected")] public bool PasswordProtected { get; set; }
    [JsonPropertyName("battlEye")]          public bool BattlEye { get; set; }
    [JsonPropertyName("source")]            public string Source { get; set; } = "";
}
