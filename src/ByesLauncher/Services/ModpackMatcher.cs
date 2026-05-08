using System.Text.RegularExpressions;
using ByesLauncher.Models;

namespace ByesLauncher.Services;

/// Tags incoming server entries with a modpack id so the standard Join flow
/// auto-syncs the right files. Two passes:
///
///  1. **Regex** — `match_patterns` configured by the admin per modpack
///     (most specific; admin-defined). First match wins.
///  2. **Fuzzy by detected gametype** — if regex didn't hit but the
///     gametype detector found a known label (Epoch, Overpoch, Origins, …),
///     try linking against any published modpack whose slug contains that
///     label. Lets "any public Epoch server" auto-link to e.g.
///     `epoch-1.0.7.1` even if the admin never set patterns. When several
///     candidate modpacks match (`epoch-1.0.7.1`, `epoch-1.0.6.2`),
///     disambiguate by version-string overlap with the server name; ties go
///     to the highest-versioned slug.
public class ModpackMatcher
{
    private readonly List<(string ModpackId, Regex Regex)> _rules = new();
    private readonly List<Modpack> _catalog = new();

    public void Load(IEnumerable<Modpack> modpacks)
    {
        _rules.Clear();
        _catalog.Clear();
        foreach (var p in modpacks)
        {
            _catalog.Add(p);
            foreach (var pat in p.MatchPatterns ?? new())
            {
                try
                {
                    var re = new Regex(pat, RegexOptions.IgnoreCase | RegexOptions.Compiled,
                        TimeSpan.FromMilliseconds(20));
                    _rules.Add((p.Id, re));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[matcher] bad pattern '{pat}' in {p.Id}: {ex.Message}");
                }
            }
        }
    }

    public string? MatchFor(ServerInfo s)
    {
        // Don't override BYES-curated assignments.
        if (s.IsByes && s.ModpackIds.Count > 0) return s.ModpackIds[0];

        // Keywords carries the A2S build-number stamp (`n144629` etc.) and
        // server flags (`bt`, `vt`). Including it in the haystack lets
        // admins put fingerprints like `\bn144629\b` in their modpack's
        // `match_patterns` and have the matcher auto-link servers reporting
        // that exact build — no manual server registration needed, the
        // server self-broadcasts its own identity through Arma's protocol.
        var haystack = $"{s.Name} {s.Gametype} {s.Map} {s.Keywords}";

        // Pass 1: explicit regex patterns from admin.
        foreach (var (id, re) in _rules)
        {
            try { if (re.IsMatch(haystack)) return id; }
            catch (RegexMatchTimeoutException) { /* skip pathological pattern */ }
        }

        // Pass 2: fuzzy gametype-to-modpack-slug match. Only run when the
        // gametype detector resolved to a known mod label (filtering out
        // generic fallbacks like "Arma 2"). The slug filter prefers
        // hyphen-delimited boundaries so "epoch" matches `epoch-1.0.7.1`
        // but not `xepoch-experimental` if both ever existed.
        var gt = s.Gametype?.Trim().ToLowerInvariant() ?? "";
        if (string.IsNullOrEmpty(gt) || gt == "arma 2") return null;

        // Strip spaces so multi-word gametypes ("Breaking Point") collapse
        // to "breakingpoint" for slug comparison.
        var gtCompact = new string(gt.Where(c => !char.IsWhiteSpace(c)).ToArray());

        var candidates = _catalog
            .Where(m => SlugMatchesGametype(m.Id, gtCompact))
            .ToList();
        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0].Id;

        // Multiple candidates — prefer one whose version segment shows up
        // in the server name. e.g. server name `[Genesis]Epoch|WIPED` plus
        // candidates `epoch-1.0.7.1` / `epoch-1.0.6.2` won't disambiguate
        // here (no version in server name), so we fall through to the
        // highest-version pick below.
        var nameLower = s.Name.ToLowerInvariant();
        var versionedHit = candidates.FirstOrDefault(m =>
        {
            var idLower = m.Id.ToLowerInvariant();
            // Pull anything that looks like a version (digits and dots) out
            // of the slug and check the server name for it.
            var verMatch = Regex.Match(idLower, @"\d[\d.]+");
            return verMatch.Success && nameLower.Contains(verMatch.Value);
        });
        if (versionedHit != null) return versionedHit.Id;

        // Last resort: highest slug wins (string sort is "good enough" for
        // semver-like slugs of the same family — `epoch-1.0.7.1` beats
        // `epoch-1.0.6.2`).
        return candidates.OrderByDescending(m => m.Id, StringComparer.OrdinalIgnoreCase).First().Id;
    }

    private static bool SlugMatchesGametype(string slug, string gametypeCompact)
    {
        var s = slug.ToLowerInvariant();
        // Word-boundary-ish match: slug starts with "<gametype>" followed by
        // end-of-string or a non-letter, OR contains "-<gametype>-".
        if (s == gametypeCompact) return true;
        if (s.StartsWith(gametypeCompact))
        {
            var next = s.Length > gametypeCompact.Length ? s[gametypeCompact.Length] : '\0';
            return !char.IsLetter(next);
        }
        return s.Contains($"-{gametypeCompact}-") || s.Contains($"-{gametypeCompact}");
    }
}
