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
            ModpackIds = new List<string>(server.ModpackIds),  // copy so later VM mutations don't bleed back
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

    /// Remove a single history entry by its endpoint string. No-op if not
    /// present (idempotent — caller doesn't have to check first).
    public void Remove(string endpoint)
    {
        var removed = _config.Config.History.RemoveAll(h =>
            string.Equals(h.Endpoint, endpoint, StringComparison.OrdinalIgnoreCase));
        if (removed > 0) _config.Save();
    }

    /// Drop history entries older than `days` (uses JoinedAt). Returns the
    /// number removed so the UI can flash a "removed N entries" toast. Days
    /// must be ≥ 1 — anything less is treated as "do nothing" rather than
    /// silently nuking the whole list.
    public int ClearOlderThan(int days)
    {
        if (days < 1) return 0;
        var cutoff = DateTime.UtcNow.AddDays(-days);
        var removed = _config.Config.History.RemoveAll(h => h.JoinedAt < cutoff);
        if (removed > 0) _config.Save();
        return removed;
    }
}
