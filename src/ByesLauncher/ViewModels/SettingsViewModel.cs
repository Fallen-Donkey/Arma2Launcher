using System.IO;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ByesLauncher.Services;

namespace ByesLauncher.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ConfigService _config;

    // Paths
    [ObservableProperty] private string arma2OaPath;
    [ObservableProperty] private string dayZStandalonePath;
    [ObservableProperty] private string backendUrl;

    // Launch
    [ObservableProperty] private bool useBattleEye;
    [ObservableProperty] private bool noSplash;
    [ObservableProperty] private bool skipIntro;
    [ObservableProperty] private bool worldEmpty;
    [ObservableProperty] private bool noPause;
    [ObservableProperty] private bool showScriptErrors;
    [ObservableProperty] private bool filePatching;
    [ObservableProperty] private bool enableHT;
    [ObservableProperty] private bool windowed;

    // Performance
    [ObservableProperty] private string maxMem;
    [ObservableProperty] private string cpuCount;
    [ObservableProperty] private string exThreads;
    [ObservableProperty] private string malloc;

    // Misc
    [ObservableProperty] private int maxParallelDownloads;
    [ObservableProperty] private string customArgs;

    [ObservableProperty] private string saveStatus = "";

    public string[] MallocOptions { get; } = { "system", "jemalloc", "tbb4malloc_bi", "tcmalloc" };
    public string[] ExThreadsOptions { get; } = { "", "0", "1", "3", "5", "7" };

    public SettingsViewModel(ConfigService config)
    {
        _config = config;
        var c = config.Config;
        arma2OaPath = c.Arma2OaPath;
        dayZStandalonePath = c.DayZStandalonePath;
        backendUrl = c.BackendUrl;
        useBattleEye = c.UseBattleEye;
        noSplash = c.NoSplash;
        skipIntro = c.SkipIntro;
        worldEmpty = c.WorldEmpty;
        noPause = c.NoPause;
        showScriptErrors = c.ShowScriptErrors;
        filePatching = c.FilePatching;
        enableHT = c.EnableHT;
        windowed = c.Windowed;
        maxMem = c.MaxMem?.ToString() ?? "";
        cpuCount = c.CpuCount?.ToString() ?? "";
        exThreads = c.ExThreads?.ToString() ?? "";
        malloc = c.Malloc;
        maxParallelDownloads = c.MaxParallelDownloads;
        customArgs = c.CustomArgs;
    }

    [RelayCommand]
    private void BrowseArmaPath()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Arma 2 executable|arma2oa.exe;ArmA2OA.exe;arma2oa_be.exe",
            Title = "Locate arma2oa.exe"
        };
        if (dlg.ShowDialog() == true)
            Arma2OaPath = Path.GetDirectoryName(dlg.FileName) ?? "";
    }

    [RelayCommand]
    private void BrowseDayZPath()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "DayZ executable|DayZ_x64.exe;DayZ_BE.exe",
            Title = "Locate DayZ_x64.exe"
        };
        if (dlg.ShowDialog() == true)
            DayZStandalonePath = Path.GetDirectoryName(dlg.FileName) ?? "";
    }

    [RelayCommand]
    private void Save()
    {
        var c = _config.Config;
        c.Arma2OaPath = Arma2OaPath;
        c.DayZStandalonePath = DayZStandalonePath;
        c.BackendUrl = BackendUrl;
        c.UseBattleEye = UseBattleEye;
        c.NoSplash = NoSplash;
        c.SkipIntro = SkipIntro;
        c.WorldEmpty = WorldEmpty;
        c.NoPause = NoPause;
        c.ShowScriptErrors = ShowScriptErrors;
        c.FilePatching = FilePatching;
        c.EnableHT = EnableHT;
        c.Windowed = Windowed;
        c.MaxMem = int.TryParse(MaxMem, out var mm) ? mm : null;
        c.CpuCount = int.TryParse(CpuCount, out var cc) ? cc : null;
        c.ExThreads = int.TryParse(ExThreads, out var et) ? et : null;
        c.Malloc = Malloc;
        c.MaxParallelDownloads = MaxParallelDownloads;
        c.CustomArgs = CustomArgs?.Trim() ?? "";
        _config.Save();
        SaveStatus = $"Saved {DateTime.Now:HH:mm:ss}";
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        NoSplash = true; SkipIntro = true; WorldEmpty = true; NoPause = false;
        ShowScriptErrors = false; FilePatching = false; EnableHT = true; Windowed = false;
        MaxMem = ""; CpuCount = ""; ExThreads = ""; Malloc = "system"; CustomArgs = "";
    }
}
