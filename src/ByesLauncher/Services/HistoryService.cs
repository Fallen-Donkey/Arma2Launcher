using ByesLauncher.Models;

namespace ByesLauncher.Services;

/// Recently-joined servers, persisted in AppConfig.History (most-recent first, max 25).
/// Drives the History tab and the "rejoin last" UX.
public class HistoryService
{
    private readonly ConfigService _config;
    private const int MaxEntries = 25;

    public HistoryService(ConfigService config) => _config = config;

    public List<HistoryEntry> All() => _config.Config.History.ToList();

    public void Record(ServerInfo server)
    {
        var list = _config.Config.History;
        var key = server.Endpoint.ToString();
        list.RemoveAll(h => h.Endpoint == key);
        list.Insert(0, new HistoryEntry
        {
            Endpoint = key,
            Name = server.Name,
            Map = server.Map,
            ModpackId = server.ModpackId,
            JoinedAt = DateTime.UtcNow,
        });
        if (list.Count > MaxEntries) list.RemoveRange(MaxEntries, list.Count - MaxEntries);
        _config.Save();
    }

    public void Clear()
    {
        _config.Config.History.Clear();
        _config.Save();
    }
}
