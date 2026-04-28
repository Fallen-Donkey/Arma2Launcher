using CommunityToolkit.Mvvm.ComponentModel;

namespace ByesLauncher.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private ServersViewModel servers;
    [ObservableProperty] private FavoritesViewModel favorites;
    [ObservableProperty] private HistoryViewModel history;
    [ObservableProperty] private DayZStandaloneViewModel dayz;
    [ObservableProperty] private ModpacksViewModel modpacks;
    [ObservableProperty] private ProfilesViewModel profiles;
    [ObservableProperty] private SettingsViewModel settings;

    [ObservableProperty] private string statusText = "Ready.";

    public MainViewModel(
        ServersViewModel servers,
        FavoritesViewModel favorites,
        HistoryViewModel history,
        DayZStandaloneViewModel dayz,
        ModpacksViewModel modpacks,
        ProfilesViewModel profiles,
        SettingsViewModel settings)
    {
        this.servers = servers;
        this.favorites = favorites;
        this.history = history;
        this.dayz = dayz;
        this.modpacks = modpacks;
        this.profiles = profiles;
        this.settings = settings;
    }
}
