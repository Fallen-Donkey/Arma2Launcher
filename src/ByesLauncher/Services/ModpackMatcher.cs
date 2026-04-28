using System.Text.RegularExpressions;
using ByesLauncher.Models;

namespace ByesLauncher.Services;

/// Compiles modpack `MatchPatterns` once and tags incoming server entries
/// with the matching modpack id. Used by ServersViewModel after the master
/// query returns — turns "any public server running Epoch" into a
/// one-click-join experience even when the server isn't in the BYES list.
public class ModpackMatcher
{
    private readonly List<(string ModpackId, Regex Regex)> _rules = new();

    public void Load(IEnumerable<Modpack> modpacks)
    {
        _rules.Clear();
        foreach (var p in modpacks)
        {
            foreach (var pat in p.MatchPatterns ?? new())
            {
                try
                {
                    var re = new Regex(pat, RegexOptions.IgnoreCase | RegexOptions.Compiled,
                        TimeSpan.FromMilliseconds(20)); // hard cap on regex eval time
                    _rules.Add((p.Id, re));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[matcher] bad pattern '{pat}' in {p.Id}: {ex.Message}");
                }
            }
        }
    }

    /// Returns the modpack id that matches this server, or null if none.
    /// First match wins — so admins should put the most-specific patterns first.
    public string? MatchFor(ServerInfo s)
    {
        // Don't override BYES-curated assignments.
        if (s.IsByes && !string.IsNullOrEmpty(s.ModpackId)) return s.ModpackId;

        var haystack = $"{s.Name} {s.Gametype} {s.Map}";
        foreach (var (id, re) in _rules)
        {
            try { if (re.IsMatch(haystack)) return id; }
            catch (RegexMatchTimeoutException) { /* skip pathological pattern */ }
        }
        return null;
    }
}
