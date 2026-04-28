using System.Diagnostics;
using System.IO;
using ByesLauncher.Models;

namespace ByesLauncher.Services;

public class LaunchService
{
    private readonly ConfigService _config;

    public LaunchService(ConfigService config) => _config = config;

    public Process Launch(ServerInfo? server, IEnumerable<string> activeMods, string? profileName, string? password = null)
    {
        var c = _config.Config;
        var root = c.Arma2OaPath;
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("Arma 2 path not configured.");

        var exe = Path.Combine(root, c.ExeName);
        if (!File.Exists(exe))
            throw new FileNotFoundException($"Launch exe not found: {exe}");

        var args = new List<string>();
        var mods = activeMods.Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToList();
        if (mods.Count > 0) args.Add($"-mod={string.Join(";", mods)}");
        if (!string.IsNullOrWhiteSpace(profileName)) args.Add($"-name={profileName}");
        if (server != null)
        {
            args.Add($"-connect={server.Endpoint.Address}");
            args.Add($"-port={server.Endpoint.GamePort}");
            if (!string.IsNullOrEmpty(password)) args.Add($"-password={password}");
        }

        if (c.NoSplash) args.Add("-nosplash");
        if (c.SkipIntro) args.Add("-skipIntro");
        if (c.WorldEmpty) args.Add("-world=empty");
        if (c.NoPause) args.Add("-noPause");
        if (c.ShowScriptErrors) args.Add("-showScriptErrors");
        if (c.FilePatching) args.Add("-filePatching");
        if (c.EnableHT) args.Add("-enableHT");
        if (c.Windowed) args.Add("-window");
        if (c.MaxMem is int mm && mm > 0) args.Add($"-maxMem={mm}");
        if (c.CpuCount is int cc && cc > 0) args.Add($"-cpuCount={cc}");
        if (c.ExThreads is int et && et >= 0) args.Add($"-exThreads={et}");
        if (!string.IsNullOrWhiteSpace(c.Malloc) && !string.Equals(c.Malloc, "system", StringComparison.OrdinalIgnoreCase))
            args.Add($"-malloc={c.Malloc}");

        if (!string.IsNullOrWhiteSpace(c.CustomArgs))
            args.AddRange(c.CustomArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = root,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        return Process.Start(psi) ?? throw new InvalidOperationException("Failed to start Arma 2.");
    }

    /// Launch DayZ Standalone via the Steam protocol (handles BattlEye/Steam auth automatically).
    public void LaunchDayZStandalone(ServerInfo? server)
    {
        var args = "-nolauncher -world=empty";
        if (server != null) args += $" \"-connect={server.Endpoint.Address}\" \"-port={server.Endpoint.GamePort}\"";
        var url = $"steam://run/221100//{Uri.EscapeDataString(args)}/";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
