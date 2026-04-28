using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ByesLauncher.Models;
using ByesLauncher.Services;

namespace ByesLauncher.ViewModels;

public partial class ModpacksViewModel : ObservableObject
{
    private readonly BackendClient _backend;
    private readonly DownloadService _downloads;

    [ObservableProperty] private ObservableCollection<Modpack> modpacks = new();
    [ObservableProperty] private Modpack? selected;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string? status;
    [ObservableProperty] private double progressFraction;

    public ModpacksViewModel(BackendClient backend, DownloadService downloads)
    {
        _backend = backend;
        _downloads = downloads;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            Modpacks.Clear();
            foreach (var m in await _backend.GetModpacksAsync()) Modpacks.Add(m);
        }
        catch (Exception ex) { Status = "Error: " + ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SyncAsync()
    {
        if (Selected == null) return;
        try
        {
            Status = $"Fetching manifest for {Selected.Name}...";
            var manifest = await _backend.GetManifestAsync(Selected.Id);
            if (manifest == null) { Status = "Manifest not found."; return; }

            var progress = new Progress<DownloadProgress>(p =>
            {
                ProgressFraction = p.Fraction;
                Status = $"{p.CompletedFiles}/{p.TotalFiles}  {FormatBytes(p.CompletedBytes)} / {FormatBytes(p.TotalBytes)}  {p.CurrentFile}";
            });
            await _downloads.SyncModpackAsync(manifest, progress);
            Status = "Up to date.";
            ProgressFraction = 1.0;
        }
        catch (Exception ex)
        {
            Status = "Error: " + ex.Message;
        }
    }

    private static string FormatBytes(long b)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double v = b; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.##} {u[i]}";
    }
}
