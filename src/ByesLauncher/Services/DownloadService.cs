using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using ByesLauncher.Models;

namespace ByesLauncher.Services;

public class DownloadProgress
{
    public long TotalBytes;
    public long CompletedBytes;
    public int TotalFiles;
    public int CompletedFiles;
    public int SkippedFiles;
    public string? CurrentFile;
    public double Fraction => TotalBytes <= 0 ? 0 : (double)CompletedBytes / TotalBytes;
}

/// User decision when a per-file download fails (network 5xx, hash mismatch, etc.).
public enum DownloadFailureAction { Retry, Skip, Abort }

public class DownloadService
{
    private readonly ConfigService _config;
    private readonly BackendClient _backend;
    private readonly IHttpClientFactory _httpFactory;

    public DownloadService(ConfigService config, BackendClient backend, IHttpClientFactory httpFactory)
    {
        _config = config;
        _backend = backend;
        _httpFactory = httpFactory;
    }

    /// Sync every file in `manifest` into the Arma 2 OA root.
    /// `onFailure` (optional) is invoked when a single file errors — it returns
    /// Retry / Skip / Abort so the caller (download dialog) can ask the user.
    /// If null, failures throw immediately.
    public async Task SyncModpackAsync(
        Manifest manifest,
        IProgress<DownloadProgress>? progress = null,
        Func<ManifestFile, Exception, Task<DownloadFailureAction>>? onFailure = null,
        CancellationToken ct = default)
    {
        var armaRoot = _config.Config.Arma2OaPath;
        if (string.IsNullOrWhiteSpace(armaRoot) || !Directory.Exists(armaRoot))
            throw new InvalidOperationException("Arma 2 install path is not set. Configure it in Settings.");

        var state = new DownloadProgress
        {
            TotalFiles = manifest.Files.Count,
            TotalBytes = manifest.Files.Sum(f => f.Size),
        };
        progress?.Report(state);

        var sem = new SemaphoreSlim(Math.Max(1, _config.Config.MaxParallelDownloads));
        var tasks = manifest.Files.Select(async file =>
        {
            await sem.WaitAsync(ct);
            try
            {
                while (true)
                {
                    try
                    {
                        await EnsureFileAsync(armaRoot, file, ct);
                        lock (state)
                        {
                            state.CompletedBytes += file.Size;
                            state.CompletedFiles += 1;
                            state.CurrentFile = file.Path;
                        }
                        progress?.Report(state);
                        return;
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        if (onFailure == null) throw;
                        var action = await onFailure(file, ex);
                        if (action == DownloadFailureAction.Retry) continue;
                        if (action == DownloadFailureAction.Skip)
                        {
                            lock (state)
                            {
                                state.SkippedFiles += 1;
                                state.CompletedBytes += file.Size; // count as "done" for progress bar honesty
                                state.CurrentFile = $"(skipped) {file.Path}";
                            }
                            progress?.Report(state);
                            return;
                        }
                        throw; // Abort
                    }
                }
            }
            finally { sem.Release(); }
        }).ToArray();

        await Task.WhenAll(tasks);
    }

    private async Task EnsureFileAsync(string armaRoot, ManifestFile file, CancellationToken ct)
    {
        var targetPath = Path.Combine(armaRoot, file.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        if (File.Exists(targetPath))
        {
            var existingHash = await HashFileAsync(targetPath, ct);
            if (string.Equals(existingHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
                return;
        }

        var blobPath = Path.Combine(_config.BlobDir, file.Sha256[..2], file.Sha256);
        if (!File.Exists(blobPath) || await HashFileAsync(blobPath, ct) != file.Sha256)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(blobPath)!);
            await DownloadBlobWithFallbackAsync(file, blobPath, ct);
        }

        // Hard-copy into place (hardlink preferred; fall back to copy across volumes)
        if (File.Exists(targetPath)) File.Delete(targetPath);
        try { CreateHardLink(targetPath, blobPath); }
        catch { File.Copy(blobPath, targetPath, overwrite: true); }
    }

    /// Try the primary BYES blob URL first; on network/HTTP failure or hash mismatch,
    /// fall back to mirror URLs (in order) so a flaky CDN or a temporarily-unhosted file
    /// doesn't block the user. Hash is verified against the manifest after every attempt.
    private async Task DownloadBlobWithFallbackAsync(ManifestFile file, string destPath, CancellationToken ct)
    {
        var sources = new List<string> { _backend.BlobUrl(file.Sha256) };
        if (file.Mirrors != null) sources.AddRange(file.Mirrors);

        Exception? lastError = null;
        foreach (var url in sources)
        {
            try
            {
                await DownloadBlobFromUrlAsync(url, file.Sha256, destPath, ct);
                return; // hash already verified inside
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex;
                // Drop the partial so the next source starts fresh
                if (File.Exists(destPath)) try { File.Delete(destPath); } catch { }
                if (File.Exists(destPath + ".part")) try { File.Delete(destPath + ".part"); } catch { }
            }
        }
        throw new InvalidDataException(
            $"All sources failed for {file.Path} ({file.Sha256[..10]}…): {lastError?.Message}",
            lastError);
    }

    private async Task DownloadBlobFromUrlAsync(string url, string sha256, string destPath, CancellationToken ct)
    {
        using var client = _httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(30);

        long resumeFrom = 0;
        var partPath = destPath + ".part";
        if (File.Exists(partPath)) resumeFrom = new FileInfo(partPath).Length;

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (resumeFrom > 0) req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(resumeFrom, null);

        using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            File.Delete(partPath);
            resumeFrom = 0;
            using var req2 = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp2 = await client.SendAsync(req2, HttpCompletionOption.ResponseHeadersRead, ct);
            resp2.EnsureSuccessStatusCode();
            await using var ins = await resp2.Content.ReadAsStreamAsync(ct);
            await using var outs = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, true);
            await ins.CopyToAsync(outs, ct);
        }
        else
        {
            resp.EnsureSuccessStatusCode();
            await using var ins = await resp.Content.ReadAsStreamAsync(ct);
            await using var outs = new FileStream(partPath, resumeFrom > 0 ? FileMode.Append : FileMode.Create,
                FileAccess.Write, FileShare.None, 1 << 16, true);
            await ins.CopyToAsync(outs, ct);
        }

        File.Move(partPath, destPath, overwrite: true);

        var hash = await HashFileAsync(destPath, ct);
        if (!string.Equals(hash, sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(destPath);
            throw new InvalidDataException($"SHA256 mismatch for blob {sha256} (got {hash}).");
        }
    }

    public static async Task<string> HashFileAsync(string path, CancellationToken ct = default)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, true);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    private static void CreateHardLink(string newPath, string existingPath)
    {
        if (!CreateHardLinkW(newPath, existingPath, IntPtr.Zero))
            throw new IOException("CreateHardLink failed");
    }
}
