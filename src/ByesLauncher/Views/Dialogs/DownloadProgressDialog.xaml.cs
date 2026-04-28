using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using ByesLauncher.Models;
using ByesLauncher.Services;

namespace ByesLauncher.Views.Dialogs;

/// Modal sync dialog. Constructs with a manifest, runs DownloadService.SyncModpackAsync
/// behind the scenes, surfaces progress + ETA, and prompts the user on per-file
/// failures (Retry / Skip / Abort). DialogResult=true on success, false on cancel/abort.
public partial class DownloadProgressDialog : INotifyPropertyChanged
{
    private readonly DownloadService _downloads;
    private readonly Manifest _manifest;
    private readonly CancellationTokenSource _cts = new();

    private readonly Stopwatch _stopwatch = new();

    public string Headline { get; }
    public string Subtitle { get; }

    private string _currentFile = "(preparing...)";
    public string CurrentFile { get => _currentFile; set { _currentFile = value; OnPropertyChanged(); } }

    private string _progressLine = "0 / 0";
    public string ProgressLine { get => _progressLine; set { _progressLine = value; OnPropertyChanged(); } }

    private string _etaLine = "";
    public string EtaLine { get => _etaLine; set { _etaLine = value; OnPropertyChanged(); } }

    private string _statusLine = "Hashing local files…";
    public string StatusLine { get => _statusLine; set { _statusLine = value; OnPropertyChanged(); } }

    private double _percent;
    public double ProgressPercent { get => _percent; set { _percent = value; OnPropertyChanged(); } }

    public bool Succeeded { get; private set; }

    public DownloadProgressDialog(DownloadService downloads, Manifest manifest, string headline)
    {
        _downloads = downloads;
        _manifest = manifest;
        Headline = headline;
        Subtitle = $"{manifest.Files.Count} files · {FormatBytes(manifest.Files.Sum(f => f.Size))}";
        InitializeComponent();
        DataContext = this;
        Loaded += async (_, _) => await RunAsync();
    }

    private async Task RunAsync()
    {
        _stopwatch.Start();
        var progress = new Progress<DownloadProgress>(p =>
        {
            ProgressPercent = p.Fraction * 100.0;
            ProgressLine = $"{p.CompletedFiles}/{p.TotalFiles} files · {FormatBytes(p.CompletedBytes)} / {FormatBytes(p.TotalBytes)}";
            CurrentFile = p.CurrentFile ?? "";
            // Simple ETA: bytes-per-second since the dialog opened, applied to remaining bytes.
            var elapsed = _stopwatch.Elapsed.TotalSeconds;
            if (elapsed > 1 && p.CompletedBytes > 0)
            {
                var bps = p.CompletedBytes / elapsed;
                if (bps > 0)
                {
                    var remaining = Math.Max(0, p.TotalBytes - p.CompletedBytes);
                    var etaSec = remaining / bps;
                    EtaLine = $"~ETA {FormatTime(etaSec)} · {FormatBytes((long)bps)}/s";
                }
            }
            if (p.SkippedFiles > 0)
                StatusLine = $"{p.SkippedFiles} file(s) skipped — see launcher log.";
            else
                StatusLine = "Downloading…";
        });

        try
        {
            await _downloads.SyncModpackAsync(
                _manifest,
                progress,
                onFailure: PromptOnFailureAsync,
                ct: _cts.Token);
            Succeeded = true;
            DialogResult = true;
            Close();
        }
        catch (OperationCanceledException)
        {
            DialogResult = false;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Sync failed:\n\n{ex.Message}", "Sync error", MessageBoxButton.OK, MessageBoxImage.Error);
            DialogResult = false;
            Close();
        }
    }

    private Task<DownloadFailureAction> PromptOnFailureAsync(ManifestFile file, Exception ex)
    {
        // Marshal to UI thread because SyncModpackAsync is running on a worker.
        var tcs = new TaskCompletionSource<DownloadFailureAction>();
        Dispatcher.Invoke(() =>
        {
            var msg =
                $"Failed to download:\n\n  {file.Path}\n\nReason: {ex.Message}\n\n" +
                "Yes  → Retry this file\n" +
                "No   → Skip and continue with the rest\n" +
                "Cancel → Abort the whole sync";
            var result = MessageBox.Show(this, msg, "Download error", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            tcs.SetResult(result switch
            {
                MessageBoxResult.Yes => DownloadFailureAction.Retry,
                MessageBoxResult.No  => DownloadFailureAction.Skip,
                _                    => DownloadFailureAction.Abort,
            });
        });
        return tcs.Task;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _cts.Cancel();

    private static string FormatBytes(long b)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double v = b; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.##} {u[i]}";
    }
    private static string FormatTime(double sec)
    {
        if (sec < 60) return $"{sec:0}s";
        if (sec < 3600) return $"{(int)(sec/60)}m {(int)(sec%60):D2}s";
        return $"{(int)(sec/3600)}h {(int)((sec%3600)/60):D2}m";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name!));
}
