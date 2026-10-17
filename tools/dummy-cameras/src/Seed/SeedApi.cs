using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;

namespace DummyCameras.Seed;

/// <summary>Result of one API call.</summary>
public sealed record ApiResult(int Status, JsonNode? Body)
{
    public bool Ok => Status is >= 200 and < 300;
    public int? DataId => Body?["data"]?["id"]?.GetValue<int>();
}

/// <summary>Seed transport: live HTTP or dry-run printer. The seeder code path is identical for both.</summary>
public interface ISeedApi : IDisposable
{
    bool IsDryRun { get; }
    Task<ApiResult> SendAsync(HttpMethod method, string path, JsonNode? body, CancellationToken ct);
}

/// <summary>Only loopback API hosts are ever contacted (checked on every request, not just at startup).</summary>
public static class LoopbackGuard
{
    public static void Assert(Uri uri)
    {
        if (!(uri.IsLoopback || uri.Host is "127.0.0.1" or "::1" or "[::1]" or "localhost"))
            throw new InvalidOperationException($"refusing non-loopback host: {uri.Host}");
    }
}

/// <summary>
/// Reads ONLY the <c>id=</c> / <c>pw=</c> lines (the dedicated injector account). Other keys (e.g. admin_*) are ignored.
/// Values are never printed.
/// </summary>
public static class InjectorCredential
{
    public static (string Id, string Password) Read(string path)
    {
        string? id = null, pw = null;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line[..eq].Trim();
            var val = line[(eq + 1)..].Trim();
            if (key == "id") id = val;
            else if (key == "pw") pw = val;
        }
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw))
            throw new InvalidOperationException($"credential file has no id=/pw= lines: {path}");
        return (id, pw);
    }
}

public sealed class DryRunSeedApi : ISeedApi
{
    private static readonly System.Text.Json.JsonSerializerOptions Display = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private readonly TextWriter _out;
    private int _nextId = -1;

    public DryRunSeedApi(TextWriter output) => _out = output;

    public bool IsDryRun => true;

    public Task<ApiResult> SendAsync(HttpMethod method, string path, JsonNode? body, CancellationToken ct)
    {
        var shown = body?.DeepClone();
        if (shown?["password"] is not null) shown["password"] = "***";
        if (shown?["connection"]?["credentials"]?["user_password"] is not null) shown["connection"]!["credentials"]!["user_password"] = "***";
        _out.WriteLine($"  [dry-run] {method.Method,-6} {path}{(shown is null ? "" : "  " + shown.ToJsonString(Display))}");

        JsonNode? response = null;
        if (method == HttpMethod.Post)
        {
            if (path.EndsWith("/auth/login", StringComparison.Ordinal))
                response = new JsonObject { ["data"] = new JsonObject { ["access_token"] = "dry-run" } };
            else if (path.EndsWith("/cameras/bulk", StringComparison.Ordinal))
            {
                var n = body?["items"]?.AsArray().Count ?? 0;
                var ids = new JsonArray();
                for (int i = 0; i < n; i++) ids.Add(_nextId--);
                response = new JsonObject { ["data"] = new JsonObject { ["created_ids"] = ids, ["failed_items"] = new JsonArray() } };
            }
            else response = new JsonObject { ["data"] = new JsonObject { ["id"] = _nextId-- } };
        }
        else if (method == HttpMethod.Get)
            response = new JsonObject { ["data"] = new JsonArray() };
        return Task.FromResult(new ApiResult(method == HttpMethod.Post ? 201 : 200, response));
    }

    public void Dispose() { }
}

/// <summary>
/// Live transport against the loopback test server. TLS is VALIDATED - never bypassed: a certificate that fails the
/// system trust is accepted only if it chains to the configured local CA (the api-test-server mkcert root) AND the
/// host is loopback.
/// </summary>
public sealed class LiveSeedApi : ISeedApi
{
    private readonly HttpClient _http;
    private readonly Uri _base;
    private string? _token;

    public LiveSeedApi(Uri apiBase, string? localCaPem)
    {
        LoopbackGuard.Assert(apiBase);
        _base = new Uri(apiBase.ToString().TrimEnd('/') + "/");
        X509Certificate2? ca = null;
        if (!string.IsNullOrWhiteSpace(localCaPem) && File.Exists(localCaPem))
            ca = X509Certificate2.CreateFromPem(File.ReadAllText(localCaPem));
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (req, cert, chain, errors) =>
            {
                if (req.RequestUri is null) return false;
                LoopbackGuard.Assert(req.RequestUri);
                if (errors == SslPolicyErrors.None) return true;
                if (ca is null || cert is null || (errors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0) return false;
                using var custom = new X509Chain();
                custom.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                custom.ChainPolicy.CustomTrustStore.Add(ca);
                custom.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                return custom.Build(cert);
            },
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
    }

    public bool IsDryRun => false;

    public void SetToken(string token) => _token = token;

    public async Task<ApiResult> SendAsync(HttpMethod method, string path, JsonNode? body, CancellationToken ct)
    {
        var uri = new Uri(_base, path.TrimStart('/'));
        LoopbackGuard.Assert(uri);
        using var req = new HttpRequestMessage(method, uri);
        if (_token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        req.Headers.Add("X-Client-Id", "dummy-cameras-seed");
        if (body is not null) req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var text = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        JsonNode? node = null;
        try { node = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text); } catch (System.Text.Json.JsonException) { }
        return new ApiResult((int)res.StatusCode, node);
    }

    public void Dispose() => _http.Dispose();
}
