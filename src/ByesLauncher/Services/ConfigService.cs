using System.IO;
using System.Text.Json;
using ByesLauncher.Models;

namespace ByesLauncher.Services;

public class ConfigService
{
    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ByesLauncher");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    public AppConfig Config { get; private set; } = new();

    public ConfigService() => Load();

    public string CacheDir => Path.Combine(ConfigDir, "cache");
    public string BlobDir => Path.Combine(CacheDir, "blobs");

    public void Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                Config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch
        {
            Config = new AppConfig();
        }

        if (string.IsNullOrWhiteSpace(Config.Arma2OaPath))
            Config.Arma2OaPath = TryAutoDetectArmaPath() ?? "";

        Directory.CreateDirectory(ConfigDir);
        Directory.CreateDirectory(BlobDir);
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    private static string? TryAutoDetectArmaPath()
    {
        string[] candidates =
        {
            @"G:\SteamLibrary\steamapps\common\Arma 2 Operation Arrowhead",
            @"C:\Program Files (x86)\Steam\steamapps\common\Arma 2 Operation Arrowhead",
            @"C:\Program Files\Steam\steamapps\common\Arma 2 Operation Arrowhead",
        };
        return candidates.FirstOrDefault(p =>
            File.Exists(Path.Combine(p, "arma2oa.exe")) || File.Exists(Path.Combine(p, "ArmA2OA.exe")));
    }
}
