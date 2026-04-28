using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ByesLauncher.Models;
using ByesLauncher.Services;

namespace ByesLauncher.ViewModels;

/// Filtered view over the main ServersViewModel.Servers collection — only the
/// servers the user has starred. Auto-syncs whenever Servers changes.
public partial class FavoritesViewModel : ObservableObject
{
    private readonly ServersViewModel _servers;
    private readonly ModpackJoinCoordinator _joiner;
    private readonly FavoritesService _favorites;

    [ObservableProperty] private ServerInfo? selected;

    public ICollectionView FavoritesView { get; }

    public FavoritesViewModel(ServersViewModel servers, ModpackJoinCoordinator joiner, FavoritesService favorites)
    {
        _servers = servers;
        _joiner = joiner;
        _favorites = favorites;

        FavoritesView = CollectionViewSource.GetDefaultView(_servers.Servers);
        // We don't replace the existing servers ICollectionView's filter — we wrap a fresh one.
        // Simpler: a manual ListCollectionView over the same backing collection.
        FavoritesView = new ListCollectionView(_servers.Servers)
        {
            Filter = obj => obj is ServerInfo s && s.IsFavorite,
        };
        FavoritesView.SortDescriptions.Add(new SortDescription(nameof(ServerInfo.IsByes), ListSortDirection.Descending));
        FavoritesView.SortDescriptions.Add(new SortDescription(nameof(ServerInfo.Name), ListSortDirection.Ascending));

        // Refresh whenever server list changes (favorites might come/go).
        ((System.Collections.Specialized.INotifyCollectionChanged)_servers.Servers).CollectionChanged += (_, _) => FavoritesView.Refresh();
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
        FavoritesView.Refresh();
    }
}
