using CommunityToolkit.Mvvm.ComponentModel;

namespace ByesLauncher.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private ServersViewModel servers;
    [ObservableProperty] private FavoritesViewModel favorites;
    [ObservableProperty] private HistoryViewModel history;
    [ObservableProperty] private ModpacksViewModel modpacks;
    [ObservableProperty] private ProfilesViewModel profiles;
    [ObservableProperty] private SettingsViewModel settings;

    [ObservableProperty] private string statusText = "Ready.";

    /// Read from the assembly attributes so it matches whatever was actually
    /// built. Useful for "are you running the latest?" debugging in the field.
    public string VersionLabel
    {
        get
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var ver = asm.GetName().Version;
            return ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "v?";
        }
    }

    public MainViewModel(
        ServersViewModel servers,
        FavoritesViewModel favorites,
        HistoryViewModel history,
        ModpacksViewModel modpacks,
        ProfilesViewModel profiles,
        SettingsViewModel settings)
    {
        this.servers = servers;
        this.favorites = favorites;
        this.history = history;
        this.modpacks = modpacks;
        this.profiles = profiles;
        this.settings = settings;
    }
}
