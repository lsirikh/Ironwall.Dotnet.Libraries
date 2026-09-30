using System.Globalization;
using System.Text.Json;
using DummyCameras.Infra;

namespace DummyCameras.Control;

/// <summary>
/// Loopback control/status endpoint for headed tests (no auth - binds 127.0.0.1 only):
/// <list type="bullet">
/// <item><c>GET /status</c> - every camera: type · ports · URLs · PTZ position/motion · faults · stream state.</item>
/// <item><c>POST /cam/{n}/fault?slow=ms&amp;noreply=0|1&amp;authfail=0|1&amp;digest=0|1</c> - change ONVIF faults at runtime
/// (RTSP auth-fail is fixed at start: MediaMTX grants are written once).</item>
/// <item><c>POST /cam/{n}/stream/kill</c> · <c>/stream/start</c> - camera power off / on.</item>
/// <item><c>POST /cam/{n}/ptz/reset</c> - back home, idle.</item>
/// </list>
/// </summary>
public sealed class ControlServer : IAsyncDisposable
{
    private readonly Farm _farm;
    private readonly MiniHttpServer _server;

    public ControlServer(Farm farm, int port)
    {
        _farm = farm;
        _server = new MiniHttpServer(port, HandleAsync);
    }

    public int Port => _server.Port;

    private Task<HttpReply> HandleAsync(HttpRequestData req, CancellationToken ct)
    {
        var parts = req.Path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (req.Method == "GET" && (parts.Length == 0 || parts[0] == "status"))
            return Task.FromResult(HttpReply.Json(StatusJson()));

        if (req.Method == "POST" && parts.Length >= 2 && parts[0] == "cam" && int.TryParse(parts[1], out var n) && _farm.Find(n) is { } cam)
        {
            var q = ParseQuery(req.Query);
            switch (parts.Length >= 3 ? parts[2] : string.Empty)
            {
                case "fault":
                    var f = cam.Spec.Faults;
                    if (q.TryGetValue("slow", out var slow) && int.TryParse(slow, out var ms)) f.SlowOnvifMs = ms;
                    if (q.TryGetValue("noreply", out var nr)) f.NoOnvifReply = nr is "1" or "true";
                    if (q.TryGetValue("authfail", out var af)) f.AuthFail = af is "1" or "true";
                    if (q.TryGetValue("digest", out var dg)) f.HttpDigest = dg is "1" or "true";
                    return Task.FromResult(HttpReply.Json(JsonSerializer.Serialize(new { cam = cam.Spec.Id, faults = f.ToString() })));
                case "stream" when parts.Length >= 4 && cam.Publisher is { } pub:
                    if (parts[3] == "kill") pub.Kill("control");
                    else if (parts[3] == "start") pub.Restart();
                    else break;
                    return Task.FromResult(HttpReply.Json(JsonSerializer.Serialize(new { cam = cam.Spec.Id, stream = parts[3] })));
                case "ptz" when parts.Length >= 4 && parts[3] == "reset" && cam.Ptz is { } ptz:
                    ptz.Reset();
                    return Task.FromResult(HttpReply.Json(JsonSerializer.Serialize(new { cam = cam.Spec.Id, ptz = "reset" })));
            }
        }
        return Task.FromResult(HttpReply.Json("{\"error\":\"not found\"}", 404));
    }

    private string StatusJson()
    {
        var now = DateTime.UtcNow;
        var cams = _farm.Cameras.Select(c =>
        {
            var s = c.Ptz?.Status(now);
            return new
            {
                id = c.Spec.Id,
                name = c.Spec.Name,
                type = c.Spec.IsPtz ? "PTZ" : "FIXED",
                onvif = c.Spec.DeviceServiceUrl,
                rtspMain = _farm.Options.EnableRtsp ? c.Spec.MainRtspUrl : null,
                rtspSub = _farm.Options.EnableRtsp && c.Spec.HasSub ? c.Spec.SubRtspUrl : null,
                faults = c.Spec.Faults.ToString(),
                onvifRequests = c.Onvif.RequestCount,
                stream = c.Publisher is null ? null : new { running = c.Publisher.IsRunning, killed = c.Publisher.IsKilled, starts = c.Publisher.Starts },
                ptz = s is null ? null : new
                {
                    pan = Math.Round(s.Position.Pan, 4),
                    tilt = Math.Round(s.Position.Tilt, 4),
                    zoom = Math.Round(s.Position.Zoom, 4),
                    motion = s.Motion,
                    target = s.TargetPreset,
                    arrivesAt = s.ArrivesAtUtc?.ToString("O", CultureInfo.InvariantCulture),
                    presets = c.Ptz!.Presets.Select(p => new { token = p.Token, name = p.Name }),
                },
            };
        });
        return JsonSerializer.Serialize(new { now = now.ToString("O"), logs = _farm.LogDir, cameras = cams });
    }

    private static Dictionary<string, string> ParseQuery(string q)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int i = kv.IndexOf('=');
            if (i < 0) d[Uri.UnescapeDataString(kv)] = "1";
            else d[Uri.UnescapeDataString(kv[..i])] = Uri.UnescapeDataString(kv[(i + 1)..]);
        }
        return d;
    }

    public ValueTask DisposeAsync() => _server.DisposeAsync();
}
