using ByesLauncher.Models;

namespace ByesLauncher.Services;

/// Persisted set of favorite server endpoints (string "ip:port"). Read-through
/// for the Favorites tab and ServersViewModel's IsFavorite hydration.
public class FavoritesService
{
    private readonly ConfigService _config;

    public FavoritesService(ConfigService config) => _config = config;

    /// Fired after a Toggle mutates the favorites set. The Favorites tab
    /// subscribes so favoriting a server in the Servers tab makes it appear
    /// instantly without requiring a "Refresh All". Argument is the
    /// "ip:port" key that was added/removed.
    public event Action<string>? Changed;

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
        Changed?.Invoke(key);
    }

    public IReadOnlyList<string> All() => _config.Config.FavoriteServers;
}
