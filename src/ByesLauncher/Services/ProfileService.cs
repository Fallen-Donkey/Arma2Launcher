using System.IO;
using ByesLauncher.Models;

namespace ByesLauncher.Services;

public class ProfileService
{
    public string ProfilesRoot
    {
        get
        {
            // OneDrive-redirected Documents on this machine.
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var oneDriveDocs = Path.Combine(userProfile, "OneDrive", "Documents", "ArmA 2 Other Profiles");
            if (Directory.Exists(Path.Combine(userProfile, "OneDrive", "Documents")))
                return oneDriveDocs;

            var docs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ArmA 2 Other Profiles");
            return docs;
        }
    }

    public List<ProfileInfo> List()
    {
        var root = ProfilesRoot;
        if (!Directory.Exists(root)) return new();
        return Directory.GetDirectories(root)
            .Select(d => new ProfileInfo { Name = Path.GetFileName(d), FolderPath = d })
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public ProfileInfo Create(string name)
    {
        var safe = SanitizeName(name);
        var dir = Path.Combine(ProfilesRoot, safe);
        Directory.CreateDirectory(dir);
        // Arma creates the .ArmA2OAProfile on first launch; we just stub the folder.
        return new ProfileInfo { Name = safe, FolderPath = dir };
    }

    public void Delete(ProfileInfo p)
    {
        if (Directory.Exists(p.FolderPath)) Directory.Delete(p.FolderPath, recursive: true);
    }

    private static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Player" : cleaned;
    }
}
