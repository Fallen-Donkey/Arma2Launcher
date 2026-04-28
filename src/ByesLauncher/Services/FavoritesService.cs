using ByesLauncher.Models;

namespace ByesLauncher.Services;

/// Persisted set of favorite server endpoints (string "ip:port"). Read-through
/// for the Favorites tab and ServersViewModel's IsFavorite hydration.
public class FavoritesService
{
    private readonly ConfigService _config;

    public FavoritesService(ConfigService config) => _config = config;

    public bool IsFavorite(ServerEndpoint ep)
        => _config.Config.FavoriteServers.Contains(ep.ToString(), StringComparer.OrdinalIgnoreCase);

    public void Toggle(ServerInfo server)
    {
        var key = server.Endpoint.ToString();
        var list = _config.Config.FavoriteServers;
        var existing = list.FirstOrDefault(s => s.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (existing != null) list.Remove(existing);
        else list.Add(key);
        server.IsFavorite = existing == null;
        _config.Save();
    }

    public IReadOnlyList<string> All() => _config.Config.FavoriteServers;
}
