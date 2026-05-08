using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ByesLauncher.Models;
using ByesLauncher.Services;

namespace ByesLauncher.ViewModels;

/// Filtered view over the main ServersViewModel.Servers collection — only the
/// servers the user has starred. Auto-syncs whenever Servers changes or the
/// FavoritesService raises Changed (so toggling a star in the Servers tab
/// shows up here instantly without forcing the user to hit Refresh All).
public partial class FavoritesViewModel : ObservableObject
{
    private readonly ServersViewModel _servers;
    private readonly ModpackJoinCoordinator _joiner;
    private readonly FavoritesService _favorites;

    [ObservableProperty] private ServerInfo? selected;
    [ObservableProperty] private string filter = "";

    public ICollectionView FavoritesView { get; }

    /// Forwarded from the singleton join coordinator so the Join button on
    /// this tab disables (and label-flips to "JOINING…") whenever a join is
    /// in progress on any tab. Same instance fires for Servers / History too.
    public bool IsJoining => _joiner.IsJoining;

    public FavoritesViewModel(ServersViewModel servers, ModpackJoinCoordinator joiner, FavoritesService favorites)
    {
        _servers = servers;
        _joiner = joiner;
        _favorites = favorites;

        FavoritesView = new ListCollectionView(_servers.Servers)
        {
            Filter = ApplyFilter,
        };
        FavoritesView.SortDescriptions.Add(new SortDescription(nameof(ServerInfo.IsByes), ListSortDirection.Descending));
        FavoritesView.SortDescriptions.Add(new SortDescription(nameof(ServerInfo.Name), ListSortDirection.Ascending));

        // Refresh whenever the underlying server list mutates (cold-start
        // load brings in entries that match existing favorites).
        ((System.Collections.Specialized.INotifyCollectionChanged)_servers.Servers).CollectionChanged += (_, _) => FavoritesView.Refresh();

        // Live-update on toggle from any tab — fixes the old behavior where
        // favoriting a server in the Servers tab required a manual Refresh
        // All before it appeared here.
        _favorites.Changed += _ =>
        {
            // Marshal to UI thread — Toggle is called from VM commands which
            // are already UI-thread today, but cheaper to be defensive than
            // chase a cross-thread refresh exception later.
            Application.Current.Dispatcher.Invoke(() => FavoritesView.Refresh());
        };

        // Surface coordinator IsJoining changes so the bound Button repaints.
        _joiner.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ModpackJoinCoordinator.IsJoining))
                OnPropertyChanged(nameof(IsJoining));
        };
    }

    partial void OnFilterChanged(string value) => FavoritesView.Refresh();

    private bool ApplyFilter(object obj)
    {
        if (obj is not ServerInfo s) return false;
        if (!s.IsFavorite) return false;
        if (string.IsNullOrWhiteSpace(Filter)) return true;
        var f = Filter.Trim();
        return s.Name.Contains(f, StringComparison.OrdinalIgnoreCase)
            || s.Map.Contains(f, StringComparison.OrdinalIgnoreCase)
            || s.Gametype.Contains(f, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private async Task JoinAsync()
    {
        if (Selected != null) await _joiner.JoinAsync(Selected);
    }

    [RelayCommand]
    private void Unfavorite()
    {
        if (Selected == null) return;
        _favorites.Toggle(Selected);
        // FavoritesService.Changed will trigger the refresh; no manual call needed.
    }

    /// Re-runs the parent Servers tab refresh so favorites reflect any
    /// newly-discovered (or now-offline) servers. Using the parent VM's
    /// command keeps the BYES-discovery + matcher pipeline in one place.
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await _servers.RefreshCommand.ExecuteAsync(null);
    }
}
