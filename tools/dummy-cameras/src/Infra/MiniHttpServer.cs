using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DummyCameras.Infra;

public sealed record HttpRequestData(string Method, string Path, string Query, IReadOnlyDictionary<string, string> Headers, string Body)
{
    /// <summary>The client sent <c>Expect: 100-continue</c> (we never send 100 - like the field cameras, FR-21).</summary>
    public bool HasExpectContinue =>
        Headers.TryGetValue("expect", out var v) && v.Contains("100-continue", StringComparison.OrdinalIgnoreCase);

    public string? Header(string name) => Headers.TryGetValue(name, out var v) ? v : null;
}

public sealed class HttpReply
{
    public int Status { get; init; } = 200;
    public string Reason { get; init; } = "OK";
    public string ContentType { get; init; } = "text/plain; charset=utf-8";
    public string Body { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>Do not answer at all - hold the connection until the client or the server gives up.</summary>
    public bool NoReply { get; init; }

    public static HttpReply Json(string json, int status = 200) =>
        new() { Status = status, Reason = status == 200 ? "OK" : "Error", ContentType = "application/json; charset=utf-8", Body = json };

    public static readonly HttpReply Hold = new() { NoReply = true };
}

/// <summary>
/// Minimal HTTP/1.1 server on a loopback <see cref="TcpListener"/> (keep-alive, Content-Length or chunked request bodies).
/// Chosen over HttpListener/Kestrel on purpose: it binds 127.0.0.1 without a URL ACL, never auto-sends
/// <c>100 Continue</c> (so an Expect-header regression shows up as a 1 s stall, exactly like a field camera), and can
/// deliberately not answer (fault injection).
/// </summary>
public sealed class MiniHttpServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly Func<HttpRequestData, CancellationToken, Task<HttpReply>> _handler;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _accept;

    public MiniHttpServer(int port, Func<HttpRequestData, CancellationToken, Task<HttpReply>> handler)
    {
        _handler = handler;
        _listener = new TcpListener(IPAddress.Loopback, port);   // loopback only - never IPAddress.Any
        _listener.Start();
        _accept = Task.Run(AcceptLoopAsync);
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false); }
            catch { return; }
            _ = Task.Run(() => ServeAsync(client));
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using var _ = client;
        client.NoDelay = true;
        var stream = client.GetStream();
        var ct = _cts.Token;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var head = await ReadHeadAsync(stream, ct).ConfigureAwait(false);
                if (head is null) return;
                var lines = head.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0) return;
                var first = lines[0].Split(' ');
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 1; i < lines.Length; i++)
                {
                    int idx = lines[i].IndexOf(':');
                    if (idx > 0) headers[lines[i][..idx].Trim().ToLowerInvariant()] = lines[i][(idx + 1)..].Trim();
                }
                string body;
                if (headers.TryGetValue("transfer-encoding", out var te) && te.Contains("chunked", StringComparison.OrdinalIgnoreCase))
                    body = await ReadChunkedAsync(stream, ct).ConfigureAwait(false);
                else
                {
                    int len = headers.TryGetValue("content-length", out var cl) && int.TryParse(cl, out var n) ? n : 0;
                    body = len > 0 ? await ReadExactAsync(stream, len, ct).ConfigureAwait(false) : string.Empty;
                }
                var target = first.Length > 1 ? first[1] : "/";
                int q = target.IndexOf('?');
                var req = new HttpRequestData(first[0], q < 0 ? target : target[..q], q < 0 ? string.Empty : target[(q + 1)..], headers, body);

                HttpReply reply;
                try { reply = await _handler(req, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    reply = new HttpReply { Status = 500, Reason = "Internal Server Error", Body = ex.GetType().Name };
                }

                if (reply.NoReply)
                {
                    // Hold the connection: wait until the peer closes it or the server stops.
                    var sink = new byte[256];
                    try { while (await stream.ReadAsync(sink, ct).ConfigureAwait(false) > 0) { } }
                    catch { /* peer gone */ }
                    return;
                }

                var payload = Encoding.UTF8.GetBytes(reply.Body);
                var sb = new StringBuilder();
                sb.Append("HTTP/1.1 ").Append(reply.Status).Append(' ').Append(reply.Reason).Append("\r\n");
                sb.Append("Content-Type: ").Append(reply.ContentType).Append("\r\n");
                sb.Append("Content-Length: ").Append(payload.Length).Append("\r\n");
                sb.Append("Server: DummyCam\r\n");
                if (reply.Headers is not null)
                    foreach (var h in reply.Headers) sb.Append(h.Key).Append(": ").Append(h.Value).Append("\r\n");
                sb.Append("\r\n");
                await stream.WriteAsync(Encoding.ASCII.GetBytes(sb.ToString()), ct).ConfigureAwait(false);
                await stream.WriteAsync(payload, ct).ConfigureAwait(false);
                if (string.Equals(req.Header("connection"), "close", StringComparison.OrdinalIgnoreCase)) return;
            }
        }
        catch { /* connection closed / server stopping */ }
    }

    private static async Task<string?> ReadHeadAsync(NetworkStream s, CancellationToken ct)
    {
        var buf = new List<byte>(1024);
        var one = new byte[1];
        while (true)
        {
            int n = await s.ReadAsync(one, ct).ConfigureAwait(false);
            if (n == 0) return null;
            buf.Add(one[0]);
            int c = buf.Count;
            if (c >= 4 && buf[c - 4] == '\r' && buf[c - 3] == '\n' && buf[c - 2] == '\r' && buf[c - 1] == '\n')
                return Encoding.ASCII.GetString(buf.ToArray());
            if (c > 64 * 1024) return null;   // runaway header
        }
    }

    private static async Task<string> ReadExactAsync(NetworkStream s, int len, CancellationToken ct)
    {
        var buf = new byte[len];
        int read = 0;
        while (read < len)
        {
            int n = await s.ReadAsync(buf.AsMemory(read, len - read), ct).ConfigureAwait(false);
            if (n == 0) throw new EndOfStreamException();
            read += n;
        }
        return Encoding.UTF8.GetString(buf);
    }

    private static async Task<string> ReadLineAsync(NetworkStream s, CancellationToken ct)
    {
        var buf = new List<byte>(16);
        var one = new byte[1];
        while (true)
        {
            int n = await s.ReadAsync(one, ct).ConfigureAwait(false);
            if (n == 0) throw new EndOfStreamException();
            if (one[0] == '\n') break;
            if (one[0] != '\r') buf.Add(one[0]);
        }
        return Encoding.ASCII.GetString(buf.ToArray());
    }

    private static async Task<string> ReadChunkedAsync(NetworkStream s, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        while (true)
        {
            var sizeLine = await ReadLineAsync(s, ct).ConfigureAwait(false);
            int semi = sizeLine.IndexOf(';');
            int size = Convert.ToInt32(semi < 0 ? sizeLine : sizeLine[..semi], 16);
            if (size == 0)
            {
                while ((await ReadLineAsync(s, ct).ConfigureAwait(false)).Length > 0) { }   // trailers
                break;
            }
            var chunk = new byte[size];
            int read = 0;
            while (read < size)
            {
                int n = await s.ReadAsync(chunk.AsMemory(read, size - read), ct).ConfigureAwait(false);
                if (n == 0) throw new EndOfStreamException();
                read += n;
            }
            ms.Write(chunk);
            await ReadLineAsync(s, ct).ConfigureAwait(false);
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener.Stop();
        try { await _accept.ConfigureAwait(false); } catch { }
        _cts.Dispose();
    }
}
