using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;

namespace LiveNatsPipeline;

/// <summary>
/// --real mode only. TERMINAL handler given to the library ApiService (ApiService overwrites the
/// InnerHandler of whatever it is given, so we forward through our own invoker: Bearer → loopback-only TLS).
/// Lets through GET and the one login/refresh POST; every other request is refused locally (403) and recorded —
/// the probe never creates or changes a row on the test server.
/// </summary>
public sealed class ReadOnlyGate : DelegatingHandler
{
    readonly HttpMessageInvoker _invoker;
    public readonly ConcurrentQueue<FakeRequest> Requests;

    public ReadOnlyGate(HttpMessageHandler realChain, ConcurrentQueue<FakeRequest> requests)
    {
        _invoker = new HttpMessageInvoker(realChain, disposeHandler: false);
        Requests = requests;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Safety.AssertLoopback(request.RequestUri!.ToString());
        var path = request.RequestUri.AbsolutePath.TrimEnd('/');
        var allowed = request.Method == HttpMethod.Get
                      || (request.Method == HttpMethod.Post && (path.EndsWith("/auth/login") || path.EndsWith("/auth/refresh")));
        if (!allowed)
        {
            Requests.Enqueue(new FakeRequest(DateTime.Now, request.Method.Method, path, 403, true));
            return new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("{\"success\":false,\"message\":\"live-nats-pipeline: write refused (read-only probe)\",\"error\":{\"code\":\"PROBE_WRITE_BLOCKED\",\"message\":\"write refused by probe\"}}",
                    Encoding.UTF8, "application/json"),
            };
        }
        var resp = await _invoker.SendAsync(request, ct).ConfigureAwait(false);
        Requests.Enqueue(new FakeRequest(DateTime.Now, request.Method.Method, path + request.RequestUri.Query, (int)resp.StatusCode, false));
        return resp;
    }

    /// <summary>Reads id=/pw= (never admin_id/admin_pw) from the credential file. Values never logged.</summary>
    public static (string Id, string Pw) ReadCredential(string path)
    {
        string? id = null, pw = null;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith("id=", StringComparison.Ordinal)) id = line[3..];
            else if (line.StartsWith("pw=", StringComparison.Ordinal)) pw = line[3..];
        }
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw))
            throw new InvalidOperationException("credential file has no id=/pw= lines (admin_* lines are deliberately ignored)");
        if (id.Equals("admin", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SAFETY ABORT: refusing the admin account (single-session evict would kick the headed GIS)");
        return (id, pw);
    }
}
