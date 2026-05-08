using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ByesLauncher.Models;

namespace ByesLauncher.Services;

/// A2S_INFO + A2S_PLAYER (Source Engine Query). Challenge-response per Valve 2020+ update.
public class A2sQuery
{
    private static readonly byte[] InfoRequest =
        Encoding.ASCII.GetBytes("\xFF\xFF\xFF\xFFTSource Engine Query\0");

    // 0x55 = A2S_PLAYER, requires a 4-byte challenge appended.
    private static readonly byte[] PlayerChallengeRequest =
        new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x55, 0xFF, 0xFF, 0xFF, 0xFF };

    public async Task<ServerInfo?> QueryAsync(ServerEndpoint ep, int timeoutMs = 2000, CancellationToken ct = default)
    {
        try
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.ReceiveTimeout = timeoutMs;
            var target = new IPEndPoint(ep.Address, ep.QueryPort);

            var sw = Stopwatch.StartNew();
            await udp.SendAsync(InfoRequest, InfoRequest.Length, target);

            var resp = await udp.ReceiveAsync(ct).AsTask().WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct);

            // Challenge response: 0xFF FF FF FF 0x41 + 4-byte challenge → resend with challenge appended.
            if (resp.Buffer.Length >= 9 && resp.Buffer[4] == 0x41)
            {
                var challenged = new byte[InfoRequest.Length + 4];
                Buffer.BlockCopy(InfoRequest, 0, challenged, 0, InfoRequest.Length);
                Buffer.BlockCopy(resp.Buffer, 5, challenged, InfoRequest.Length, 4);
                await udp.SendAsync(challenged, challenged.Length, target);
                resp = await udp.ReceiveAsync(ct).AsTask().WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct);
            }
            sw.Stop();

            var info = Parse(resp.Buffer, ep);
            if (info != null) info.Ping = (int)sw.ElapsedMilliseconds;
            return info;
        }
        catch
        {
            return null;
        }
    }

    /// A2S_PLAYER returns the server's connected player roster (name, score, time).
    /// Needs a two-step challenge handshake first.
    public async Task<List<A2sPlayer>?> QueryPlayersAsync(ServerEndpoint ep, int timeoutMs = 2500, CancellationToken ct = default)
    {
        try
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.ReceiveTimeout = timeoutMs;
            var target = new IPEndPoint(ep.Address, ep.QueryPort);

            // Step 1: send -1 challenge → server returns 4-byte challenge.
            await udp.SendAsync(PlayerChallengeRequest, PlayerChallengeRequest.Length, target);
            var resp = await udp.ReceiveAsync(ct).AsTask().WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct);
            if (resp.Buffer.Length < 9 || resp.Buffer[4] != 0x41) return null;

            // Step 2: resend with the challenge appended.
            var req = new byte[9];
            req[0] = req[1] = req[2] = req[3] = 0xFF;
            req[4] = 0x55;
            Buffer.BlockCopy(resp.Buffer, 5, req, 5, 4);
            await udp.SendAsync(req, req.Length, target);

            resp = await udp.ReceiveAsync(ct).AsTask().WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct);
            if (resp.Buffer.Length < 6 || resp.Buffer[4] != 0x44) return null;

            return ParsePlayers(resp.Buffer);
        }
        catch
        {
            return null;
        }
    }

    private static List<A2sPlayer> ParsePlayers(byte[] buf)
    {
        var list = new List<A2sPlayer>();
        int i = 5;                  // skip header + 0x44
        if (i >= buf.Length) return list;
        byte count = buf[i++];
        for (int p = 0; p < count && i < buf.Length; p++)
        {
            i++;                    // index byte (ignored)
            string name = ReadCString(buf, ref i);
            if (i + 4 > buf.Length) break;
            int score = BitConverter.ToInt32(buf, i); i += 4;
            if (i + 4 > buf.Length) break;
            float duration = BitConverter.ToSingle(buf, i); i += 4;
            list.Add(new A2sPlayer(name, score, TimeSpan.FromSeconds(Math.Max(0, duration))));
        }
        return list;
    }

    public async Task<List<ServerInfo>> QueryManyAsync(
        IEnumerable<ServerEndpoint> endpoints,
        int parallelism = 64,
        Action<ServerInfo>? onResult = null,
        CancellationToken ct = default)
    {
        var sem = new SemaphoreSlim(parallelism);
        var results = new List<ServerInfo>();
        var lockObj = new object();

        var tasks = endpoints.Select(async ep =>
        {
            await sem.WaitAsync(ct);
            try
            {
                // 2.5s — DayZ servers under heavy player load can be slow to
                // respond to A2S. Lower than this drops too many real servers.
                var info = await QueryAsync(ep, 2500, ct);
                if (info != null)
                {
                    lock (lockObj) results.Add(info);
                    onResult?.Invoke(info);
                }
            }
            finally { sem.Release(); }
        }).ToArray();

        await Task.WhenAll(tasks);
        return results;
    }

    private static ServerInfo? Parse(byte[] buf, ServerEndpoint ep)
    {
        if (buf.Length < 7 || buf[4] != 0x49) return null;
        int i = 5; // skip header + type
        i++;       // protocol
        string name = ReadCString(buf, ref i);
        string map = ReadCString(buf, ref i);
        string folder = ReadCString(buf, ref i);
        string game = ReadCString(buf, ref i);
        if (i + 2 > buf.Length) return null;
        i += 2;    // appid (short)
        if (i + 5 > buf.Length) return null;
        byte players = buf[i++];
        byte maxPlayers = buf[i++];
        i++;       // bots
        i++;       // server type
        i++;       // os
        byte visibility = buf[i++];
        byte vac = buf[i++];
        string version = i < buf.Length ? ReadCString(buf, ref i) : "";

        // Extra Data Flag — optional fields after Version. Parse out the
        // Keywords (tags) string when present; that's where Arma 2 servers
        // expose their build number (`n144629`), BattlEye filter (`bt`/`bf`),
        // signature flags (`vt`), and other identity bits we use to match
        // servers to modpacks. Layout per the Source A2S spec:
        //   if (edf & 0x80) i += 2;        // game port
        //   if (edf & 0x10) i += 8;        // steam id
        //   if (edf & 0x40) i += 2 + cstr; // spectator port + name
        //   if (edf & 0x20) keywords = cstr;
        //   if (edf & 0x01) i += 8;        // game id
        // We're only after keywords; everything else gets skipped via the
        // appropriate offset. Bail safely if the buffer runs short.
        string keywords = "";
        if (i < buf.Length)
        {
            byte edf = buf[i++];
            if ((edf & 0x80) != 0) { if (i + 2 > buf.Length) goto done; i += 2; }
            if ((edf & 0x10) != 0) { if (i + 8 > buf.Length) goto done; i += 8; }
            if ((edf & 0x40) != 0)
            {
                if (i + 2 > buf.Length) goto done;
                i += 2;
                _ = ReadCString(buf, ref i);
            }
            if ((edf & 0x20) != 0 && i < buf.Length)
                keywords = ReadCString(buf, ref i);
            // Game ID we don't need; deliberately not parsed.
        }
        done:
        _ = vac; _ = folder;

        return new ServerInfo
        {
            Endpoint = ep,
            Name = name,
            Map = map,
            // The "game" CString is usually the BI mod description ("DayZ Epoch",
            // "ArmA 2: Operation Arrowhead"). Keywords carries the version-stamp
            // tags. We surface both: Gametype for human display, Keywords for
            // matching infrastructure.
            Gametype = game,
            Keywords = keywords,
            Version = version,
            Players = players,
            MaxPlayers = maxPlayers,
            PasswordProtected = visibility == 1,
            BattleEye = name.Contains("BE", StringComparison.OrdinalIgnoreCase)
                     || keywords.Contains("bt", StringComparison.OrdinalIgnoreCase),
        };
    }

    private static string ReadCString(byte[] buf, ref int i)
    {
        int start = i;
        while (i < buf.Length && buf[i] != 0) i++;
        var s = Encoding.UTF8.GetString(buf, start, i - start);
        if (i < buf.Length) i++;
        return s;
    }
}

public record A2sPlayer(string Name, int Score, TimeSpan PlayTime)
{
    public string PlayTimeDisplay => PlayTime.TotalHours >= 1
        ? $"{(int)PlayTime.TotalHours}h {PlayTime.Minutes:D2}m"
        : $"{(int)PlayTime.TotalMinutes}m {PlayTime.Seconds:D2}s";
}
