using System.IO;
using System.Net;
using System.Net.Sockets;
using ByesLauncher.Models;

namespace ByesLauncher.Services;

/// Valve Master Server v1 protocol.
/// Docs: https://developer.valvesoftware.com/wiki/Master_Server_Query_Protocol
public class SteamMasterQuery
{
    private const string MasterHost = "hl2master.steampowered.com";
    private const int MasterPort = 27011;
    private const int A2OaAppId = 33930;

    public async Task<List<ServerEndpoint>> QueryAsync(string? filter = null, CancellationToken ct = default)
    {
        filter ??= $@"\appid\{A2OaAppId}\empty\1\full\1";

        var masterIp = (await Dns.GetHostAddressesAsync(MasterHost, ct))
            .First(a => a.AddressFamily == AddressFamily.InterNetwork);
        var master = new IPEndPoint(masterIp, MasterPort);

        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.ReceiveTimeout = 4000;

        var results = new List<ServerEndpoint>();
        string seed = "0.0.0.0:0";

        while (!ct.IsCancellationRequested)
        {
            var req = BuildRequest(seed, filter);
            await udp.SendAsync(req, req.Length, master);

            UdpReceiveResult resp;
            try { resp = await udp.ReceiveAsync(ct).AsTask().WaitAsync(TimeSpan.FromSeconds(5), ct); }
            catch { break; }

            var parsed = ParseResponse(resp.Buffer);
            if (parsed.Count == 0) break;

            foreach (var ep in parsed)
            {
                if (ep.Address.ToString() == "0.0.0.0") return results;
                results.Add(ep);
            }
            seed = parsed[^1].ToString();

            if (results.Count > 5000) break;
        }
        return results;
    }

    private static byte[] BuildRequest(string seed, string filter)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0x31);
        ms.WriteByte(0xFF);
        WriteCString(ms, seed);
        WriteCString(ms, filter);
        return ms.ToArray();
    }

    private static void WriteCString(Stream s, string str)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(str);
        s.Write(bytes, 0, bytes.Length);
        s.WriteByte(0);
    }

    private static List<ServerEndpoint> ParseResponse(byte[] buf)
    {
        var list = new List<ServerEndpoint>();
        if (buf.Length < 6) return list;
        // Header: FF FF FF FF 66 0A
        int idx = 6;
        while (idx + 6 <= buf.Length)
        {
            var ip = new IPAddress(new[] { buf[idx], buf[idx + 1], buf[idx + 2], buf[idx + 3] });
            ushort port = (ushort)((buf[idx + 4] << 8) | buf[idx + 5]);
            list.Add(new ServerEndpoint(ip, port));
            idx += 6;
        }
        return list;
    }
}
