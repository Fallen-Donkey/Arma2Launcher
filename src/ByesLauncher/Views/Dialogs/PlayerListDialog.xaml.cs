using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using ByesLauncher.Models;
using ByesLauncher.Services;

namespace ByesLauncher.Views.Dialogs;

public partial class PlayerListDialog : INotifyPropertyChanged
{
    private readonly A2sQuery _a2s;
    private readonly ServerEndpoint _endpoint;

    public string ServerName { get; }
    public ObservableCollection<A2sPlayer> Players { get; } = new();

    private string _subtitle = "Loading...";
    public string Subtitle
    {
        get => _subtitle;
        set { _subtitle = value; OnPropertyChanged(); }
    }

    public Visibility EmptyVisibility => !_loading && Players.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility LoadingVisibility => _loading ? Visibility.Visible : Visibility.Collapsed;

    private bool _loading = true;

    public PlayerListDialog(A2sQuery a2s, ServerInfo server)
    {
        _a2s = a2s;
        _endpoint = server.Endpoint;
        ServerName = server.Name;
        InitializeComponent();
        DataContext = this;
        Loaded += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        _loading = true;
        OnPropertyChanged(nameof(LoadingVisibility));
        OnPropertyChanged(nameof(EmptyVisibility));
        Players.Clear();

        var players = await _a2s.QueryPlayersAsync(_endpoint);
        if (players != null)
            foreach (var p in players) Players.Add(p);

        _loading = false;
        Subtitle = $"{Players.Count} player(s) online · {_endpoint}";
        OnPropertyChanged(nameof(LoadingVisibility));
        OnPropertyChanged(nameof(EmptyVisibility));
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await ReloadAsync();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name!));
}
