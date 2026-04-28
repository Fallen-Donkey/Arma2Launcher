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
}
