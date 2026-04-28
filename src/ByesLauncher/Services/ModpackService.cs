using System.IO;
using ByesLauncher.Models;

namespace ByesLauncher.Services;

/// Reconciles modpack manifests with what's installed in the Arma 2 OA root.
public class ModpackService
{
    private readonly ConfigService _config;

    public ModpackService(ConfigService config) => _config = config;

    /// Discover installed `@Mod` folders at the Arma root.
    public List<string> DiscoverInstalledMods()
    {
        var root = _config.Config.Arma2OaPath;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return new();
        return Directory.GetDirectories(root, "@*")
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// Top-level mod folders referenced in a manifest (paths like `@Epoch\Addons\foo.pbo`).
    public static IEnumerable<string> ModsInManifest(Manifest m) =>
        m.Files.Select(f => f.Path.Split('\\', '/').First(s => s.StartsWith("@")))
               .Distinct(StringComparer.OrdinalIgnoreCase);
}
