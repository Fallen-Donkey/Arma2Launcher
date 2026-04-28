using System.Net;

namespace ByesLauncher.Models;

public record ServerEndpoint(IPAddress Address, ushort GamePort)
{
    public ushort QueryPort => (ushort)(GamePort + 1);
    public override string ToString() => $"{Address}:{GamePort}";
}

public class ServerInfo
{
    public required ServerEndpoint Endpoint { get; set; }
    public string Name { get; set; } = "";
    public string Map { get; set; } = "";
    public string Gametype { get; set; } = "";
    public string Version { get; set; } = "";
    public int Players { get; set; }
    public int MaxPlayers { get; set; }
    public int Ping { get; set; }
    public bool PasswordProtected { get; set; }
    public bool BattleEye { get; set; }

    public string? ModpackId { get; set; }
    public bool IsByes { get; set; }
    public bool IsFavorite { get; set; }

    public string PlayersDisplay => $"{Players}/{MaxPlayers}";
}
