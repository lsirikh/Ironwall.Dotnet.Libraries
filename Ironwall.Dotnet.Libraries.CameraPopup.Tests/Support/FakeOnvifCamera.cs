using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;

/// <summary>
/// 루프백 가짜 ONVIF 카메라(HTTP/1.1 keep-alive, SOAP 1.2) — 제공자 시험용(T-02). 실측 카메라처럼 <c>Expect: 100-continue</c> 에
/// 100 을 보내지 않는다(T-01 FR-21 — 헤더가 붙으면 요청마다 1초가 샌다). 응답은 요청 본문의 작업 이름으로 고른다:
/// GetCapabilities(미디어 · PTZ 주소) · GetProfiles(1920×1080 P0, 640×360 P1) · GetStreamUri · GetConfigurations · GetNode(상대 · 연속 공간) ·
/// GetPresets · ContinuousMove · Stop · 그 밖 = 시각(GetSystemDateAndTime). <see cref="FailStreamUri"/> 면 GetStreamUri 에 SOAP 오류.
/// </summary>
public sealed class FakeOnvifCamera : IAsyncDisposable
{
    public sealed record Request(string Path, IReadOnlyDictionary<string, string> Headers, string Body)
    {
        public bool HasExpectContinue => Headers.TryGetValue("expect", out var v) && v.Contains("100-continue", StringComparison.OrdinalIgnoreCase);
    }

    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _accept;

    public FakeOnvifCamera()
    {
        _listener.Start();
        _accept = Task.Run(AcceptLoopAsync);
    }

    public ConcurrentQueue<Request> Requests { get; } = new();
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public bool FailStreamUri { get; set; }

    /// <summary>응답 지연(ONVIF 무응답 · 느린 카메라 흉내, FR-26).</summary>
    public TimeSpan Delay { get; set; }

    // ── 드래그 PTZ 시험용 좌표 공간 · 위치(기본값 = 이전과 같은 모양: 일반 상대 + 연속, 위치 응답 없음) ──
    public bool HasRelativeGeneric { get; set; } = true;
    /// <summary>화각 상대 공간(TranslationSpaceFov)을 일반 공간보다 <b>앞에</b> 싣는다 — 첫 항목을 무조건 쓰는 실수를 잡는다.</summary>
    public bool HasRelativeFov { get; set; }
    public bool HasAbsolute { get; set; }
    public bool HasContinuous { get; set; } = true;
    /// <summary>GetStatus 가 돌려줄 위치(pan, tilt, zoom). null 이면 GetStatus 를 모르는 카메라(기대한 모양이 아닌 응답).</summary>
    public (double Pan, double Tilt, double Zoom)? Status { get; set; }

    public const string RelGenericUri = "http://www.onvif.org/ver10/tptz/PanTiltSpaces/TranslationGenericSpace";
    public const string RelFovUri = "http://www.onvif.org/ver10/tptz/PanTiltSpaces/TranslationSpaceFov";
    public const string AbsGenericUri = "http://www.onvif.org/ver10/tptz/PanTiltSpaces/PositionGenericSpace";

    /// <summary>카메라에 도착한 PTZ 명령만, 도착 순서대로(작업 이름 · 본문). 위치 읽기(GetStatus)는 뺀다.</summary>
    public IReadOnlyList<(string Op, string Body)> PtzCommands
        => Requests.Select(r => (Op: PtzOpOf(r.Body), r.Body)).Where(x => x.Op is not null).Select(x => (x.Op!, x.Body)).ToList();

    private static string? PtzOpOf(string body)
    {
        foreach (var op in new[] { "RelativeMove", "AbsoluteMove", "ContinuousMove" })
            if (body.Contains(op, StringComparison.Ordinal)) return op;
        return body.Contains("<Stop", StringComparison.Ordinal) || body.Contains(":Stop", StringComparison.Ordinal) ? "Stop" : null;
    }

    /// <summary>명령 본문의 PanTilt x · y · space(없으면 null).</summary>
    public static (double X, double Y, string? Space)? PanTiltOf(string body)
    {
        var m = System.Text.RegularExpressions.Regex.Match(body, "<(?:\\w+:)?PanTilt\\b([^>]*)>");
        if (!m.Success) return null;
        string attrs = m.Groups[1].Value;
        string? Attr(string name)
        {
            var a = System.Text.RegularExpressions.Regex.Match(attrs, "\\b" + name + "=\"([^\"]*)\"");
            return a.Success ? a.Groups[1].Value : null;
        }
        if (Attr("x") is not { } x || Attr("y") is not { } y) return null;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return (double.Parse(x, System.Globalization.NumberStyles.Float, inv), double.Parse(y, System.Globalization.NumberStyles.Float, inv), Attr("space"));
    }

    /// <summary>GetStreamUri 가 돌려주는 주소(계정 없음 — 실측 카메라와 같다).</summary>
    public string StreamUri => $"rtsp://127.0.0.1:{Port}/sub";

    public int Count(string operation) => Requests.Count(r => r.Body.Contains(operation, StringComparison.Ordinal));

    private string? Respond(string body)
    {
        string xaddr = $"http://127.0.0.1:{Port}";
        if (body.Contains("GetCapabilities"))
            return "<tds:GetCapabilitiesResponse><tds:Capabilities>" +
                   $"<tt:Media><tt:XAddr>{xaddr}/onvif/media</tt:XAddr></tt:Media>" +
                   $"<tt:PTZ><tt:XAddr>{xaddr}/onvif/ptz</tt:XAddr></tt:PTZ>" +
                   "</tds:Capabilities></tds:GetCapabilitiesResponse>";
        if (body.Contains("GetStreamUri"))
        {
            if (FailStreamUri) return null; // 시각 응답 = 기대한 모양이 아님 → 조회 실패
            return "<trt:GetStreamUriResponse><trt:MediaUri>" +
                   $"<tt:Uri>{StreamUri}</tt:Uri><tt:InvalidAfterConnect>false</tt:InvalidAfterConnect>" +
                   "<tt:InvalidAfterReboot>false</tt:InvalidAfterReboot><tt:Timeout>PT0S</tt:Timeout>" +
                   "</trt:MediaUri></trt:GetStreamUriResponse>";
        }
        if (body.Contains("GetProfiles"))
            return "<trt:GetProfilesResponse>" + Profile("P0", 1920, 1080) + Profile("P1", 640, 360) + "</trt:GetProfilesResponse>";
        if (body.Contains("GetConfigurations"))
            return "<tptz:GetConfigurationsResponse><tptz:PTZConfiguration token=\"C0\"><tt:Name>c</tt:Name><tt:UseCount>1</tt:UseCount>" +
                   "<tt:NodeToken>N0</tt:NodeToken></tptz:PTZConfiguration></tptz:GetConfigurationsResponse>";
        if (body.Contains("GetNode"))
            return "<tptz:GetNodeResponse><tptz:PTZNode token=\"N0\"><tt:Name>n</tt:Name><tt:SupportedPTZSpaces>" +
                   (HasAbsolute ? Space("AbsolutePanTiltPositionSpace", AbsGenericUri, true) : string.Empty) +
                   (HasAbsolute ? "<tt:AbsoluteZoomPositionSpace><tt:URI>http://www.onvif.org/ver10/tptz/ZoomSpaces/PositionGenericSpace</tt:URI><tt:XRange><tt:Min>0</tt:Min><tt:Max>1</tt:Max></tt:XRange></tt:AbsoluteZoomPositionSpace>" : string.Empty) +
                   (HasRelativeFov ? Space("RelativePanTiltTranslationSpace", RelFovUri, true) : string.Empty) +
                   (HasRelativeGeneric ? Space("RelativePanTiltTranslationSpace", RelGenericUri, true) : string.Empty) +
                   (HasContinuous ? Space("ContinuousPanTiltVelocitySpace", "http://www.onvif.org/ver10/tptz/PanTiltSpaces/VelocityGenericSpace", true) : string.Empty) +
                   (HasContinuous ? Space("ContinuousZoomVelocitySpace", "http://www.onvif.org/ver10/tptz/ZoomSpaces/VelocityGenericSpace", false) : string.Empty) +
                   "</tt:SupportedPTZSpaces><tt:MaximumNumberOfPresets>8</tt:MaximumNumberOfPresets><tt:HomeSupported>true</tt:HomeSupported>" +
                   "</tptz:PTZNode></tptz:GetNodeResponse>";
        if (body.Contains("GetPresets"))
            return "<tptz:GetPresetsResponse><tptz:Preset token=\"1\"><tt:Name>정문</tt:Name></tptz:Preset>" +
                   "<tptz:Preset token=\"2\"><tt:Name>후문</tt:Name></tptz:Preset></tptz:GetPresetsResponse>";
        if (body.Contains("RelativeMove")) return "<tptz:RelativeMoveResponse/>";
        if (body.Contains("AbsoluteMove")) return "<tptz:AbsoluteMoveResponse/>";
        if (body.Contains("GetStatus"))
        {
            if (Status is not { } s) return null;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            return "<tptz:GetStatusResponse><tptz:PTZStatus><tt:Position>" +
                   $"<tt:PanTilt x=\"{s.Pan.ToString(inv)}\" y=\"{s.Tilt.ToString(inv)}\" space=\"{AbsGenericUri}\"/>" +
                   $"<tt:Zoom x=\"{s.Zoom.ToString(inv)}\" space=\"http://www.onvif.org/ver10/tptz/ZoomSpaces/PositionGenericSpace\"/>" +
                   "</tt:Position><tt:MoveStatus><tt:PanTilt>IDLE</tt:PanTilt><tt:Zoom>IDLE</tt:Zoom></tt:MoveStatus>" +
                   "<tt:UtcTime>2026-09-30T01:02:03Z</tt:UtcTime></tptz:PTZStatus></tptz:GetStatusResponse>";
        }
        if (body.Contains("ContinuousMove")) return "<tptz:ContinuousMoveResponse/>";
        if (body.Contains("<Stop") || body.Contains(":Stop")) return "<tptz:StopResponse/>";
        return null;
    }

    private static string Profile(string token, int w, int h) =>
        $"<trt:Profiles token=\"{token}\" fixed=\"true\"><tt:Name>{token}</tt:Name>" +
        $"<tt:VideoEncoderConfiguration token=\"E{token}\"><tt:Name>e</tt:Name><tt:UseCount>1</tt:UseCount><tt:Encoding>H264</tt:Encoding>" +
        $"<tt:Resolution><tt:Width>{w}</tt:Width><tt:Height>{h}</tt:Height></tt:Resolution><tt:Quality>5</tt:Quality>" +
        "</tt:VideoEncoderConfiguration></trt:Profiles>";

    private static string Space(string element, string uri, bool twoAxes) =>
        $"<tt:{element}><tt:URI>{uri}</tt:URI><tt:XRange><tt:Min>-1</tt:Min><tt:Max>1</tt:Max></tt:XRange>" +
        (twoAxes ? "<tt:YRange><tt:Min>-1</tt:Min><tt:Max>1</tt:Max></tt:YRange>" : string.Empty) + $"</tt:{element}>";

    private const string TimeBody =
        "<tds:GetSystemDateAndTimeResponse><tds:SystemDateAndTime>" +
        "<tt:DateTimeType>NTP</tt:DateTimeType><tt:DaylightSavings>false</tt:DaylightSavings>" +
        "<tt:UTCDateTime><tt:Time><tt:Hour>1</tt:Hour><tt:Minute>2</tt:Minute><tt:Second>3</tt:Second></tt:Time>" +
        "<tt:Date><tt:Year>2026</tt:Year><tt:Month>9</tt:Month><tt:Day>30</tt:Day></tt:Date></tt:UTCDateTime>" +
        "</tds:SystemDateAndTime></tds:GetSystemDateAndTimeResponse>";

    private static string Envelope(string inner) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<s:Envelope xmlns:s=\"http://www.w3.org/2003/05/soap-envelope\" xmlns:tds=\"http://www.onvif.org/ver10/device/wsdl\"" +
        " xmlns:trt=\"http://www.onvif.org/ver10/media/wsdl\" xmlns:tptz=\"http://www.onvif.org/ver20/ptz/wsdl\"" +
        " xmlns:tt=\"http://www.onvif.org/ver10/schema\"><s:Body>" + inner + "</s:Body></s:Envelope>";

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
        var stream = client.GetStream();
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var head = await ReadHeadAsync(stream).ConfigureAwait(false);
                if (head is null) return;
                var lines = head.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
                var first = lines[0].Split(' ');
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 1; i < lines.Length; i++)
                {
                    int idx = lines[i].IndexOf(':');
                    if (idx > 0) headers[lines[i][..idx].Trim().ToLowerInvariant()] = lines[i][(idx + 1)..].Trim();
                }
                int len = headers.TryGetValue("content-length", out var cl) ? int.Parse(cl) : 0;
                var body = len > 0 ? await ReadExactAsync(stream, len).ConfigureAwait(false) : string.Empty;
                Requests.Enqueue(new Request(first.Length > 1 ? first[1] : "/", headers, body));
                if (Delay > TimeSpan.Zero) await Task.Delay(Delay, _cts.Token).ConfigureAwait(false);
                var soap = Encoding.UTF8.GetBytes(Envelope(Respond(body) ?? TimeBody));
                var headBytes = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/soap+xml; charset=utf-8\r\n" +
                                                        $"Content-Length: {soap.Length}\r\n\r\n");
                await stream.WriteAsync(headBytes, _cts.Token).ConfigureAwait(false);
                await stream.WriteAsync(soap, _cts.Token).ConfigureAwait(false);
            }
        }
        catch { /* 연결 종료 */ }
    }

    private async Task<string?> ReadHeadAsync(NetworkStream s)
    {
        var buf = new List<byte>(1024);
        var one = new byte[1];
        while (true)
        {
            int n = await s.ReadAsync(one, _cts.Token).ConfigureAwait(false);
            if (n == 0) return null;
            buf.Add(one[0]);
            int c = buf.Count;
            if (c >= 4 && buf[c - 4] == '\r' && buf[c - 3] == '\n' && buf[c - 2] == '\r' && buf[c - 1] == '\n')
                return Encoding.ASCII.GetString(buf.ToArray());
        }
    }

    private async Task<string> ReadExactAsync(NetworkStream s, int len)
    {
        var buf = new byte[len];
        int read = 0;
        while (read < len)
        {
            int n = await s.ReadAsync(buf.AsMemory(read, len - read), _cts.Token).ConfigureAwait(false);
            if (n == 0) throw new EndOfStreamException();
            read += n;
        }
        return Encoding.UTF8.GetString(buf);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener.Stop();
        try { await _accept.ConfigureAwait(false); } catch { }
        _cts.Dispose();
    }
}
