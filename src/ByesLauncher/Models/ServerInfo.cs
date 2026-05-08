using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ByesLauncher.Models;

public record ServerEndpoint(IPAddress Address, ushort GamePort, ushort? ExplicitQueryPort = null)
{
    /// Most Arma 2 OA servers use game+1 as the A2S query port. The
    /// discovery API gives us the real value when it differs from default,
    /// which we honor here so player-list / refresh-single still works on
    /// non-default port pairs.
    public ushort QueryPort => ExplicitQueryPort ?? (ushort)(GamePort + 1);
    public override string ToString() => $"{Address}:{GamePort}";
}

/// One row in the server browser. Implemented as an ObservableObject so the
/// DataGrid's `{Binding Ping}` etc. refresh in place when background A2S
/// probes update fields after the initial discovery returns.
public partial class ServerInfo : ObservableObject
{
    public required ServerEndpoint Endpoint { get; set; }

    [ObservableProperty] private string name = "";
    [ObservableProperty] private string map = "";
    [ObservableProperty] private string gametype = "";
    [ObservableProperty] private string version = "";

    /// Raw Arma 2 A2S keywords/tags string — comma-separated flags like
    /// `bt,r164,n144629,s1,i0,mf,lf,vt,dt,g`. Carries server-self-reported
    /// identity (build number `nXXXX`, BattlEye flag, signature verify),
    /// which the modpack matcher uses for fingerprint-based auto-linking
    /// without requiring admins to register their server anywhere.
    [ObservableProperty] private string keywords = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayersDisplay))]
    private int players;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayersDisplay))]
    private int maxPlayers;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PingDisplay))]
    [NotifyPropertyChangedFor(nameof(HasPing))]
    private int ping;
    [ObservableProperty] private bool passwordProtected;
    [ObservableProperty] private bool battleEye;

    /// Required modpacks in launch (load_order) order. The last entry
    /// overrides earlier ones on file-path collisions when synced; the
    /// `-mod=` argument list mirrors that order so Arma's later-wins
    /// semantics stays consistent.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModpackDisplay))]
    private List<string> modpackIds = new();

    [ObservableProperty] private bool isByes;
    [ObservableProperty] private bool isFavorite;

    public string PlayersDisplay => $"{Players}/{MaxPlayers}";
    /// "—" when ping wasn't measured (Steam Web API doesn't return it; A2S
    /// probe failed or hasn't run yet) so the column doesn't lie with "0".
    public string PingDisplay => Ping > 0 ? $"{Ping}" : "—";

    /// Used as a sort key — true rows sort above false ones so unmeasured
    /// servers fall to the bottom of the list rather than appearing first
    /// (which would happen with a naïve `Ping ascending` sort, since 0 is
    /// the smallest value).
    public bool HasPing => Ping > 0;

    /// Human-readable rendering of ModpackIds for the details panel.
    /// Empty → "", one → just the id, multiple → joined with " + ".
    public string ModpackDisplay => ModpackIds.Count switch
    {
        0 => "",
        1 => ModpackIds[0],
        _ => string.Join(" + ", ModpackIds),
    };
}
