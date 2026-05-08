using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using ByesLauncher.Models;

namespace ByesLauncher.Services;

public class BackendClient
{
    private readonly ConfigService _config;
    private readonly IHttpClientFactory _httpFactory;

    public BackendClient(ConfigService config, IHttpClientFactory httpFactory)
    {
        _config = config;
        _httpFactory = httpFactory;
    }

    private HttpClient NewClient()
    {
        var client = _httpFactory.CreateClient();
        client.BaseAddress = new Uri(_config.Config.BackendUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    public async Task<List<ByesServerEntry>> GetByesServersAsync(CancellationToken ct = default)
    {
        using var c = NewClient();
        return await c.GetFromJsonAsync<List<ByesServerEntry>>("api/servers/byes", ct) ?? new();
    }

    public async Task<List<Modpack>> GetModpacksAsync(CancellationToken ct = default)
    {
        using var c = NewClient();
        return await c.GetFromJsonAsync<List<Modpack>>("api/modpacks", ct) ?? new();
    }

    public async Task<Manifest?> GetManifestAsync(string modpackSlug, CancellationToken ct = default)
    {
        using var c = NewClient();
        // The site exposes manifests at /api/modpacks/<slug>/manifest.json
        return await c.GetFromJsonAsync<Manifest>($"api/modpacks/{modpackSlug}/manifest.json", ct);
    }

    public string BlobUrl(string sha256)
    {
        var baseUrl = _config.Config.BackendUrl.TrimEnd('/');
        return $"{baseUrl}/files/{sha256[..2]}/{sha256}";
    }

    /// <summary>
    /// Public-server discovery. Replaces the deprecated direct Steam-master-
    /// server UDP query with a backend-mediated lookup that cascades Steam
    /// Web API → BattleMetrics. The response is enriched with player counts
    /// and metadata, so the launcher doesn't need to A2S each server itself.
    /// </summary>
    /// <param name="game">"arma2oa" or "dayzsa"</param>
    public async Task<DiscoveryResult> GetDiscoveredServersAsync(string game, CancellationToken ct = default)
    {
        using var c = NewClient();
        c.Timeout = TimeSpan.FromSeconds(20); // backend caches; first call worst-case ~5s
        DiscoveryResponse? body;
        try { body = await c.GetFromJsonAsync<DiscoveryResponse>($"api/servers/discover?game={Uri.EscapeDataString(game)}", ct); }
        catch (Exception ex) { throw new InvalidOperationException($"Discovery endpoint unreachable: {ex.Message}", ex); }
        if (body == null) return new DiscoveryResult(new(), null, false);

        var list = new List<ServerInfo>(body.Servers.Count);
        foreach (var s in body.Servers)
        {
            if (!IPAddress.TryParse(s.Ip, out var addr)) continue; // hostname-only entries dropped — A2S needs IP
            var port = (ushort)Math.Clamp(s.Port, 1, 65535);
            ushort? qp = (s.QueryPort > 0 && s.QueryPort != s.Port + 1) ? (ushort)s.QueryPort : null;

            // Steam's gametype field is BI tag soup; extract a clean mod
            // label from the server name and use that for display + filtering.
            var modLabel = GameTypeDetector.Detect(s.Name, fallback: "Arma 2");
            list.Add(new ServerInfo
            {
                Endpoint = new ServerEndpoint(addr, port, qp),
                Name              = s.Name,
                Map               = s.Map,
                Gametype          = modLabel,
                Version           = s.Version,
                Players           = s.Players,
                MaxPlayers        = s.MaxPlayers,
                Ping              = s.Ping ?? 0,
                PasswordProtected = s.PasswordProtected,
                BattleEye         = s.BattlEye,
            });
        }
        return new DiscoveryResult(list, body.Source, body.Cached);
    }
}

/// Tuple-style result so the ViewModel can render data-source provenance
/// (e.g. "steam-stale" → show "showing cached data, Steam may be down").
public record DiscoveryResult(List<ServerInfo> Servers, string? Source, bool Cached)
{
    public string SourceDisplay => Source switch
    {
        "steam"               => "Steam · fresh",
        "battlemetrics"       => "BattleMetrics · fresh",
        "steam-stale"         => "Steam · stale (revalidating)",
        "battlemetrics-stale" => "BattleMetrics · stale (revalidating)",
        "none"                => "no source available",
        "error"               => "discovery error",
        null                  => "",
        _                     => Source,
    };
}
