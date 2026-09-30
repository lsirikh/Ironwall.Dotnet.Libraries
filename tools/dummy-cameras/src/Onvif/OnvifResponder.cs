using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;
using DummyCameras.Infra;
using DummyCameras.Ptz;

namespace DummyCameras.Onvif;

/// <summary>
/// One dummy camera's ONVIF endpoint (device_service · media_service · ptz_service on one loopback port).
/// Operations are dispatched by the SOAP body element (namespace + local name), so the path is informational.
/// Every request is logged to onvif-requests.jsonl; every PTZ operation also to ptz-commands.jsonl
/// (op · args · result · position after · latency) - the assertion surface for headed tests.
/// </summary>
public sealed class OnvifResponder : IAsyncDisposable
{
    private static readonly HashSet<string> PtzOps = new(StringComparer.Ordinal)
    {
        "ContinuousMove", "Stop", "GotoPreset", "GotoHomePosition", "SetHomePosition", "SetPreset", "RemovePreset",
        "AbsoluteMove", "RelativeMove", "GetStatus", "GetPresets",
    };

    private readonly CameraSpec _cam;
    private readonly string _user;
    private readonly string _password;
    private readonly JsonlLog _requests;
    private readonly JsonlLog _ptzLog;
    private readonly Func<DateTime> _utcNow;
    private MiniHttpServer? _server;

    public OnvifResponder(CameraSpec cam, string user, string password, PtzSimulator? ptz, JsonlLog requests, JsonlLog ptzLog, Func<DateTime>? utcNow = null)
    {
        _cam = cam;
        _user = user;
        _password = password;
        Ptz = ptz;
        _requests = requests;
        _ptzLog = ptzLog;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public PtzSimulator? Ptz { get; }
    public CameraSpec Camera => _cam;
    public int Port => _server?.Port ?? _cam.OnvifPort;
    public long RequestCount => Interlocked.Read(ref _requestCount);
    private long _requestCount;

    public void Start()
    {
        _server = new MiniHttpServer(_cam.OnvifPort, HandleAsync);
        _cam.OnvifPort = _server.Port;   // 0 → the real port (so XAddr / URLs are right)
    }

    private async Task<HttpReply> HandleAsync(HttpRequestData req, CancellationToken ct)
    {
        Interlocked.Increment(ref _requestCount);
        var sw = Stopwatch.StartNew();
        var faults = _cam.Faults;

        string op = "?", ns = string.Empty;
        XDocument? doc = null;
        try
        {
            doc = XDocument.Parse(req.Body);
            var body = doc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "Body");
            var first = body?.Elements().FirstOrDefault();
            if (first is not null) { op = first.Name.LocalName; ns = first.Name.NamespaceName; }
        }
        catch (XmlException) { /* not SOAP - answered below */ }

        if (faults.NoOnvifReply)
        {
            LogRequest(req, op, "no-reply (fault injected)", sw, HasUsernameToken(doc));
            return HttpReply.Hold;
        }
        if (faults.SlowOnvifMs > 0)
            await Task.Delay(faults.SlowOnvifMs, ct).ConfigureAwait(false);

        HttpReply reply;
        string result;
        if ((doc is null || op == "?") && req.Header("authorization") is null)
        {
            // An HTTP client doing Digest may probe with an empty body (curl --digest) - challenge first, like a camera's web layer.
            reply = Challenge();
            result = "http-401-challenge";
        }
        else if (doc is null || op == "?")
        {
            reply = SoapFault(400, "Sender", "ter:InvalidArgs", null, "Malformed SOAP request");
            result = "fault:malformed";
        }
        else if (faults.HttpDigest && !HttpDigestAuth.Verify(req.Header("authorization"), req.Method, _user, _password))
        {
            reply = Challenge();
            result = "http-401-challenge";
        }
        else
        {
            bool httpDigestOk = !faults.AuthFail && !faults.WsSecurityOnly && HttpDigestAuth.Verify(req.Header("authorization"), req.Method, _user, _password);
            (reply, result) = Dispatch(doc, op, ns, httpDigestOk);
        }

        LogRequest(req, op, result, sw, HasUsernameToken(doc));
        return reply;
    }

    /// <summary>
    /// ONVIF access class PRE_AUTH (Core spec, default access policy): answered without credentials. Our client relies on
    /// this - its device client syncs the clock and reads capabilities before (or without) a WS-Security header.
    /// </summary>
    private static readonly HashSet<string> PreAuthOps = new(StringComparer.Ordinal)
    {
        "GetSystemDateAndTime", "GetCapabilities", "GetServices", "GetServiceCapabilities", "GetWsdlUrl", "GetHostname", "GetEndpointReference",
    };

    /// <summary>
    /// Auth like a field camera: PRE_AUTH ops are open; everything else needs a valid WS-UsernameToken OR HTTP Digest.
    /// No credentials at all → HTTP 401 Digest challenge (a client holding digest credentials retries);
    /// a bad UsernameToken → SOAP ter:NotAuthorized. <see cref="CameraFaults.AuthFail"/> rejects every credential.
    /// </summary>
    private (HttpReply, string) Dispatch(XDocument doc, string op, string ns, bool httpDigestOk)
    {
        var now = _utcNow();
        var body = doc.Root!.Elements().First(e => e.Name.LocalName == "Body").Elements().First();

        if (op == "GetSystemDateAndTime") return (Ok(OnvifXml.SystemDateAndTime(now)), "ok");

        bool strict = _cam.Faults.WsSecurityOnly;
        if ((strict || !PreAuthOps.Contains(op)) && !httpDigestOk)
        {
            var header = doc.Root.Elements().FirstOrDefault(e => e.Name.LocalName == "Header");
            var auth = WsUsernameToken.Verify(header, _user, _password, now);
            if (_cam.Faults.AuthFail && auth != AuthResult.Missing) auth = AuthResult.BadPassword;
            if (auth == AuthResult.Missing && !strict)
            {
                LogPtzIfNeeded(op, body, "http-401-challenge", now);
                return (Challenge(), "http-401-challenge");
            }
            if (auth != AuthResult.Ok)
            {
                var r = SoapFault(400, "Sender", "ter:NotAuthorized", null, $"Sender not authorized ({auth})");
                LogPtzIfNeeded(op, body, $"fault:NotAuthorized({auth})", now);
                return (r, $"fault:NotAuthorized({auth})");
            }
        }

        try
        {
            return ns switch
            {
                OnvifXml.NsTds => Device(op, body),
                OnvifXml.NsTrt => Media(op, body),
                OnvifXml.NsTptz => PtzOp(op, body, now),
                _ => (NotSupported(op), "fault:ActionNotSupported"),
            };
        }
        catch (FormatException ex)
        {
            return (SoapFault(400, "Sender", "ter:InvalidArgVal", null, ex.Message), "fault:InvalidArgVal");
        }
    }

    // ── Device ────────────────────────────────────────────────────────────

    private (HttpReply, string) Device(string op, XElement body)
    {
        switch (op)
        {
            case "GetDeviceInformation": return (Ok(OnvifXml.DeviceInformation(_cam)), "ok");
            case "GetCapabilities":
            {
                var cats = body.Elements().Where(e => e.Name.LocalName == "Category").Select(e => e.Value.Trim()).ToList();
                var xml = OnvifXml.Capabilities(_cam, cats);
                return xml is null
                    ? (SoapFault(400, "Receiver", "ter:ActionNotSupported", "ter:NoSuchService", "No such service"), "fault:NoSuchService")
                    : (Ok(xml), "ok");
            }
            case "GetServices": return (Ok(OnvifXml.Services(_cam)), "ok");
            case "GetScopes": return (Ok(OnvifXml.Scopes(_cam)), "ok");
            case "GetNetworkInterfaces": return (Ok(OnvifXml.NetworkInterfaces(_cam)), "ok");
            case "GetHostname": return (Ok(OnvifXml.Hostname(_cam)), "ok");
            default: return (NotSupported(op), "fault:ActionNotSupported");
        }
    }

    // ── Media ─────────────────────────────────────────────────────────────

    private (HttpReply, string) Media(string op, XElement body)
    {
        switch (op)
        {
            case "GetProfiles": return (Ok(OnvifXml.Profiles(_cam)), "ok");
            case "GetProfile":
            {
                var p = FindProfile(Child(body, "ProfileToken"));
                return p is null ? (NoProfile(), "fault:NoProfile")
                    : (Ok("<trt:GetProfileResponse>" + OnvifXml.Profile(_cam, p.Value.Token, p.Value.W, p.Value.H, "trt:Profile") + "</trt:GetProfileResponse>"), "ok");
            }
            case "GetStreamUri":
            {
                var p = FindProfile(Child(body, "ProfileToken"));
                return p is null ? (NoProfile(), "fault:NoProfile") : (Ok(OnvifXml.StreamUri(p.Value.Uri)), $"ok uri={p.Value.Uri}");
            }
            case "GetVideoSources": return (Ok(OnvifXml.VideoSources(_cam)), "ok");
            default: return (NotSupported(op), "fault:ActionNotSupported");
        }
    }

    private (string Token, int W, int H, string Uri)? FindProfile(string? token)
    {
        foreach (var p in OnvifXml.ProfileList(_cam))
            if (p.Token == token) return p;
        return null;
    }

    // ── PTZ ───────────────────────────────────────────────────────────────

    private (HttpReply, string) PtzOp(string op, XElement body, DateTime now)
    {
        var ptz = Ptz;
        if (ptz is null || !_cam.IsPtz)
        {
            LogPtzIfNeeded(op, body, "fault:ActionNotSupported(fixed camera)", now);
            return (NotSupported(op), "fault:ActionNotSupported");
        }

        // Profile-scoped operations must name a real profile (our client uses the first profile's token).
        var profile = Child(body, "ProfileToken");
        bool needsProfile = PtzOps.Contains(op) && op is not ("GetNodes" or "GetNode" or "GetConfigurations" or "GetConfiguration");
        if (needsProfile && FindProfile(profile) is null)
        {
            LogPtzIfNeeded(op, body, "fault:NoProfile", now);
            return (NoProfile(), "fault:NoProfile");
        }

        HttpReply reply;
        string result = "ok";
        switch (op)
        {
            case "GetConfigurations": reply = Ok(OnvifXml.Configurations()); break;
            case "GetConfiguration": reply = Ok(OnvifXml.Configuration()); break;
            case "GetNodes": reply = Ok("<tptz:GetNodesResponse>" + OnvifXml.Node("tptz:PTZNode", 32) + "</tptz:GetNodesResponse>"); break;
            case "GetNode":
                reply = Child(body, "NodeToken") == OnvifXml.PtzNodeToken
                    ? Ok("<tptz:GetNodeResponse>" + OnvifXml.Node("tptz:PTZNode", 32) + "</tptz:GetNodeResponse>")
                    : SoapFault(400, "Sender", "ter:InvalidArgVal", "ter:NoEntity", "No such PTZ node");
                if (reply.Status != 200) result = "fault:NoEntity";
                break;
            case "GetStatus": reply = Ok(OnvifXml.Status(ptz.Status(now), now)); break;
            case "GetPresets": reply = Ok(OnvifXml.Presets(ptz.Presets)); break;
            case "ContinuousMove":
            {
                var v = FindVector(body, "Velocity");
                var timeout = Child(body, "Timeout");
                TimeSpan? t = string.IsNullOrWhiteSpace(timeout) ? null : XmlConvert.ToTimeSpan(timeout);
                ptz.ContinuousMove(v.Pan, v.Tilt, v.Zoom, t, now);
                reply = Ok("<tptz:ContinuousMoveResponse/>");
                break;
            }
            case "Stop":
            {
                bool pt = ParseBool(Child(body, "PanTilt"), true), z = ParseBool(Child(body, "Zoom"), true);
                ptz.Stop(pt, z, now);
                reply = Ok("<tptz:StopResponse/>");
                break;
            }
            case "GotoPreset":
            {
                var token = Child(body, "PresetToken") ?? string.Empty;
                if (ptz.GotoPreset(token, now)) reply = Ok("<tptz:GotoPresetResponse/>");
                else { reply = SoapFault(400, "Sender", "ter:InvalidArgVal", "ter:NoToken", "No such preset"); result = "fault:NoToken"; }
                break;
            }
            case "GotoHomePosition": ptz.GotoHome(now); reply = Ok("<tptz:GotoHomePositionResponse/>"); break;
            case "SetHomePosition": ptz.SetHome(now); reply = Ok("<tptz:SetHomePositionResponse/>"); break;
            case "SetPreset":
            {
                var token = ptz.SetPreset(Child(body, "PresetToken"), Child(body, "PresetName"), now);
                reply = Ok($"<tptz:SetPresetResponse><tptz:PresetToken>{OnvifXml.Esc(token)}</tptz:PresetToken></tptz:SetPresetResponse>");
                result = $"ok token={token}";
                break;
            }
            case "RemovePreset":
            {
                if (ptz.RemovePreset(Child(body, "PresetToken") ?? string.Empty)) reply = Ok("<tptz:RemovePresetResponse/>");
                else { reply = SoapFault(400, "Sender", "ter:InvalidArgVal", "ter:NoToken", "No such preset"); result = "fault:NoToken"; }
                break;
            }
            case "AbsoluteMove":
            {
                var v = FindVector(body, "Position");
                var cur = ptz.Status(now).Position;
                ptz.AbsoluteMove(new PtzVector(v.Pan ?? cur.Pan, v.Tilt ?? cur.Tilt, v.Zoom ?? cur.Zoom), now);
                reply = Ok("<tptz:AbsoluteMoveResponse/>");
                break;
            }
            case "RelativeMove":
            {
                var v = FindVector(body, "Translation");
                ptz.RelativeMove(new PtzVector(v.Pan ?? 0, v.Tilt ?? 0, v.Zoom ?? 0), now);
                reply = Ok("<tptz:RelativeMoveResponse/>");
                break;
            }
            default:
                reply = NotSupported(op);
                result = "fault:ActionNotSupported";
                break;
        }

        LogPtzIfNeeded(op, body, result, now);
        return (reply, result);
    }

    // ── logging ──────────────────────────────────────────────────────────

    /// <summary>Did the SOAP request carry a WS-Security UsernameToken header (validity is judged in Dispatch)?</summary>
    private static bool HasUsernameToken(XDocument? doc)
        => doc?.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "Header")
               ?.Descendants().Any(e => e.Name.LocalName == "UsernameToken") == true;

    private void LogRequest(HttpRequestData req, string op, string result, Stopwatch sw, bool wsSecurity)
    {
        _requests.Write(new Dictionary<string, object?>
        {
            ["ts"] = _utcNow().ToString("O"),
            ["cam"] = _cam.Id,
            ["port"] = Port,
            ["path"] = req.Path,
            ["op"] = op,
            ["result"] = result,
            ["expect100"] = req.HasExpectContinue,
            ["wsSecurity"] = wsSecurity,
            ["httpAuth"] = req.Header("authorization") is { } a ? a.Split(' ')[0] : null,
            // whether an HTTP Digest header named the camera account (the name itself is not logged)
            ["httpAuthUserOk"] = req.Header("authorization") is { } d && d.StartsWith("Digest ", StringComparison.OrdinalIgnoreCase)
                ? d.Contains($"username=\"{_user}\"", StringComparison.Ordinal) : null,
            ["latencyMs"] = sw.ElapsedMilliseconds,
        });
    }

    private void LogPtzIfNeeded(string op, XElement body, string result, DateTime now)
    {
        if (!PtzOps.Contains(op)) return;
        if (op is "GetStatus" or "GetPresets" && result == "ok") return;   // reads are in onvif-requests.jsonl; keep the command log about commands
        var v = op switch
        {
            "ContinuousMove" => FindVector(body, "Velocity"),
            "AbsoluteMove" => FindVector(body, "Position"),
            "RelativeMove" => FindVector(body, "Translation"),
            _ => (null, null, null),
        };
        var snap = Ptz?.Status(now);
        _ptzLog.Write(new Dictionary<string, object?>
        {
            ["ts"] = now.ToString("O"),
            ["cam"] = _cam.Id,
            ["op"] = op,
            ["profile"] = Child(body, "ProfileToken"),
            ["pan"] = v.Pan,
            ["tilt"] = v.Tilt,
            ["zoom"] = v.Zoom,
            ["timeout"] = op == "ContinuousMove" ? Child(body, "Timeout") : null,
            ["preset"] = Child(body, "PresetToken"),
            ["stopPanTilt"] = op == "Stop" ? Child(body, "PanTilt") : null,
            ["stopZoom"] = op == "Stop" ? Child(body, "Zoom") : null,
            ["result"] = result,
            ["position"] = snap is null ? null : new[] { Math.Round(snap.Position.Pan, 4), Math.Round(snap.Position.Tilt, 4), Math.Round(snap.Position.Zoom, 4) },
            ["motion"] = snap?.Motion,
            ["arrivesAt"] = snap?.ArrivesAtUtc?.ToString("O"),
        });
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private static string? Child(XElement parent, string local)
        => parent.Elements().FirstOrDefault(e => e.Name.LocalName == local)?.Value.Trim();

    /// <summary>PanTilt x/y and Zoom x attributes under the named vector element (Velocity · Position · Translation).</summary>
    private static (double? Pan, double? Tilt, double? Zoom) FindVector(XElement body, string vectorName)
    {
        var vec = body.Elements().FirstOrDefault(e => e.Name.LocalName == vectorName);
        if (vec is null) return (null, null, null);
        var pt = vec.Elements().FirstOrDefault(e => e.Name.LocalName == "PanTilt");
        var z = vec.Elements().FirstOrDefault(e => e.Name.LocalName == "Zoom");
        return (Num(pt?.Attribute("x")), Num(pt?.Attribute("y")), Num(z?.Attribute("x")));
    }

    private static double? Num(XAttribute? a)
        => a is null ? null : double.Parse(a.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);

    private static bool ParseBool(string? s, bool dflt) => s is null ? dflt : s.Trim() is "true" or "1";

    private static HttpReply Ok(string inner) => new()
    {
        ContentType = "application/soap+xml; charset=utf-8",
        Body = OnvifXml.Envelope(inner),
    };

    private static HttpReply SoapFault(int status, string code, string subcode, string? subSub, string reason) => new()
    {
        Status = status,
        Reason = status == 400 ? "Bad Request" : "Internal Server Error",
        ContentType = "application/soap+xml; charset=utf-8",
        Body = OnvifXml.Fault(code, subcode, subSub, reason),
    };

    private static HttpReply Challenge() => new()
    {
        Status = 401,
        Reason = "Unauthorized",
        ContentType = "text/plain",
        Body = "401 Unauthorized",
        Headers = new Dictionary<string, string> { ["WWW-Authenticate"] = HttpDigestAuth.Challenge() },
    };

    private static HttpReply NotSupported(string op) =>
        SoapFault(400, "Receiver", "ter:ActionNotSupported", null, $"Optional action not implemented: {op}");

    private static HttpReply NoProfile() =>
        SoapFault(400, "Sender", "ter:InvalidArgVal", "ter:NoProfile", "No such profile");

    public async ValueTask DisposeAsync()
    {
        if (_server is not null) await _server.DisposeAsync().ConfigureAwait(false);
    }
}
