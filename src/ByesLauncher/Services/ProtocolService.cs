using System.IO;
using Microsoft.Win32;

namespace ByesLauncher.Services;

/// Registers `byes://` as a custom URL protocol handler under HKCU so the
/// website can deep-link straight into the launcher:
///
///    <a href="byes://connect?ip=1.2.3.4&port=2302&modpack=epoch-1.0.7.1">Join</a>
///
/// HKCU registration doesn't need admin rights — works for the normal user
/// install path. We re-register on every launch so a moved EXE updates the
/// path automatically.
public static class ProtocolService
{
    private const string ProtocolName = "byes";
    private const string DisplayName = "Backyard Esports Launcher";

    public static void EnsureRegistered()
    {
        try
        {
            var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe)) return;

            // HKCU\Software\Classes\byes — same shape as HKCR\* but per-user.
            using var rootKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProtocolName}");
            rootKey.SetValue("", $"URL:{DisplayName}");
            rootKey.SetValue("URL Protocol", "");

            using var iconKey = rootKey.CreateSubKey("DefaultIcon");
            iconKey.SetValue("", $"\"{exe}\",0");

            using var commandKey = rootKey.CreateSubKey(@"shell\open\command");
            commandKey.SetValue("", $"\"{exe}\" \"%1\"");
        }
        catch (Exception ex)
        {
            // Registration failure is non-fatal — the launcher just can't be
            // deep-linked from the browser until the user runs as admin or
            // we get write perms.
            System.Diagnostics.Debug.WriteLine($"[proto] register failed: {ex.Message}");
        }
    }

    /// Parse a `byes://...` URI into a structured request, or null if not
    /// a recognized BYES URL.
    public static ProtocolRequest? Parse(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri)) return null;
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed)) return null;
        if (!string.Equals(parsed.Scheme, ProtocolName, StringComparison.OrdinalIgnoreCase)) return null;

        var host = parsed.Host.ToLowerInvariant();
        var query = System.Web.HttpUtility.ParseQueryString(parsed.Query);
        return new ProtocolRequest(
            Action: host,
            Ip: query["ip"],
            Port: int.TryParse(query["port"], out var p) ? p : null,
            Modpack: query["modpack"],
            ServerId: query["id"]);
    }
}

public record ProtocolRequest(string Action, string? Ip, int? Port, string? Modpack, string? ServerId);
