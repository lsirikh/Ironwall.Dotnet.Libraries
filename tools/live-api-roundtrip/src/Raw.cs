using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LiveApiRoundTrip;

/// <summary>
/// Raw loopback JSON client used ONLY to arrange fixtures and to assert server state
/// (re-GET). It never stands in for the client under test - every "act" goes through
/// the real library service. Keeping arrange/assert out of the DTO layer also avoids
/// trap #2 (asserting a re-serialized DTO instead of the real server state).
/// </summary>
public sealed class Raw
{
    readonly HttpClient _http;
    public string Token = "";

    public Raw()
    {
        LoopbackGuard.Assert(Bootstrap.BASE_URL);
        var h = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (req, c, ch, e) =>
            {
                if (req.RequestUri is not null) LoopbackGuard.Assert(req.RequestUri);
                return true;
            }
        };
        _http = new HttpClient(h) { BaseAddress = new Uri(Bootstrap.BASE_URL + "/"), Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<(int Status, JObject Body)> Send(HttpMethod m, string path, object body = null)
    {
        var req = new HttpRequestMessage(m, path);
        LoopbackGuard.Assert(req.RequestUri!.IsAbsoluteUri ? req.RequestUri : new Uri(_http.BaseAddress!, path));
        if (body is not null)
            req.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
        if (Token.Length > 0) req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + Token);
        using var res = await _http.SendAsync(req).ConfigureAwait(false);
        var txt = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
        JObject jo;
        try { jo = string.IsNullOrWhiteSpace(txt) ? new JObject() : JObject.Parse(txt); }
        catch { jo = new JObject { ["_raw"] = txt }; }
        return ((int)res.StatusCode, jo);
    }

    public Task<(int, JObject)> Get(string p) => Send(HttpMethod.Get, p);
    public Task<(int, JObject)> Post(string p, object b) => Send(HttpMethod.Post, p, b);
    public Task<(int, JObject)> Patch(string p, object b) => Send(new HttpMethod("PATCH"), p, b);
    public Task<(int, JObject)> Delete(string p, object b = null) => Send(HttpMethod.Delete, p, b);

    /// <summary>Login with the local seeded admin. Returns the access token.</summary>
    public async Task<string> LoginAsync()
    {
        var (st, jo) = await Post("auth/login", new { login_id = Bootstrap.LOGIN_ID, password = Bootstrap.LOGIN_PW }).ConfigureAwait(false);
        if (st != 200) throw new InvalidOperationException("raw login failed: " + st + " " + jo.ToString(Formatting.None));
        Token = (string)jo["data"]!["access_token"]!;
        return Token;
    }
}
