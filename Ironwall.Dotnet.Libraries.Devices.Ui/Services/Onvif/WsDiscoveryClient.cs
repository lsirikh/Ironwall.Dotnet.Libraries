using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Services.Onvif;

/****************************************************************************
   Purpose      : ONVIF 카메라 검색(WS-Discovery Probe → ProbeMatches) — 카메라 등록 화면 [ONVIF 검색]
   Created On   : 2026-10-04
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>검색으로 찾은 ONVIF 장치 하나(ProbeMatch 한 건, 같은 EndpointReference 는 합친다).</summary>
public sealed record DiscoveredOnvifDevice(
    string EndpointAddress,
    IReadOnlyList<string> XAddrs,
    IReadOnlyList<string> Types,
    IReadOnlyList<string> Scopes)
{
    private const string ScopeName = "onvif://www.onvif.org/name/";
    private const string ScopeHardware = "onvif://www.onvif.org/hardware/";
    private const string ScopeType = "onvif://www.onvif.org/type/";

    /// <summary>device_service 주소 — IPv4 http 를 먼저(카메라는 IPv6 · 링크로컬 주소를 함께 싣기도 한다).</summary>
    public Uri? DeviceService
    {
        get
        {
            var uris = XAddrs.Select(x => Uri.TryCreate(x, UriKind.Absolute, out var u) ? u : null)
                             .Where(u => u is not null && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps))
                             .Cast<Uri>()
                             .ToList();
            return uris.FirstOrDefault(u => u.HostNameType == UriHostNameType.IPv4) ?? uris.FirstOrDefault();
        }
    }

    public string? Host => DeviceService?.Host;
    public int Port => DeviceService?.Port ?? 80;

    /// <summary>scope name/ — 카메라가 알리는 이름(없으면 null).</summary>
    public string? Name => ScopeValue(ScopeName);

    /// <summary>scope hardware/ — 모델 힌트(조회 전 표시용).</summary>
    public string? Hardware => ScopeValue(ScopeHardware);

    /// <summary>scope type/ptz 가 있으면 true. 없다고 고정형이라는 뜻은 아니다(조회로 확정).</summary>
    public bool PtzHint => Scopes.Any(s => s.StartsWith(ScopeType, StringComparison.OrdinalIgnoreCase)
                                           && s[ScopeType.Length..].Contains("ptz", StringComparison.OrdinalIgnoreCase));

    private string? ScopeValue(string prefix)
    {
        var s = Scopes.FirstOrDefault(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (s is null || s.Length == prefix.Length) return null;
        try { return Uri.UnescapeDataString(s[prefix.Length..]); }
        catch (UriFormatException) { return s[prefix.Length..]; }
    }
}

/// <summary>검색 범위 · 시간.</summary>
public sealed class WsDiscoveryProbeOptions
{
    public const int DefaultPort = 3702;
    public static readonly IPAddress MulticastGroup = IPAddress.Parse("239.255.255.250");

    /// <summary>응답을 기다리는 시간(WS-Discovery 기본 응답 지연 상한 500ms + 여유).</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>239.255.255.250:3702 로 인터페이스마다 보낸다(루프백 포함).</summary>
    public bool Multicast { get; set; } = true;

    public int MulticastPort { get; set; } = DefaultPort;

    /// <summary>직접 묻는 대상(시험 · 멀티캐스트가 막힌 망에서 알려진 주소).</summary>
    public IList<IPEndPoint> UnicastTargets { get; } = new List<IPEndPoint>();
}

public interface IWsDiscoveryClient
{
    /// <summary>Probe 를 보내고 <see cref="WsDiscoveryProbeOptions.Window"/> 동안 온 응답을 모은다. 실패해도 던지지 않는다(취소 제외).</summary>
    Task<IReadOnlyList<DiscoveredOnvifDevice>> ProbeAsync(WsDiscoveryProbeOptions options, CancellationToken ct = default);
}

/// <summary>
/// WS-Discovery 2005/04 클라이언트(ONVIF Core §7). Types=dn:NetworkVideoTransmitter 로 물어 카메라만 답하게 한다.
/// 소켓 하나(임시 포트)로 보내고 받는다 — 응답은 보낸 주소로 유니캐스트로 온다.
/// </summary>
public sealed class WsDiscoveryClient : IWsDiscoveryClient
{
    public const string NsSoap = "http://www.w3.org/2003/05/soap-envelope";
    public const string NsWsa = "http://schemas.xmlsoap.org/ws/2004/08/addressing";
    public const string NsWsd = "http://schemas.xmlsoap.org/ws/2005/04/discovery";
    public const string NsDn = "http://www.onvif.org/ver10/network/wsdl";

    public async Task<IReadOnlyList<DiscoveredOnvifDevice>> ProbeAsync(WsDiscoveryProbeOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var messageId = "urn:uuid:" + Guid.NewGuid();
        var probe = Encoding.UTF8.GetBytes(BuildProbe(messageId));
        var found = new Dictionary<string, DiscoveredOnvifDevice>(StringComparer.OrdinalIgnoreCase);

        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
        udp.MulticastLoopback = true;   // 같은 PC 의 장치(가짜 카메라 팜)도 받게
        try { udp.Client.IOControl(-1744830452 /* SIO_UDP_CONNRESET */, new byte[] { 0 }, null); }
        catch (Exception ex) when (ex is SocketException or PlatformNotSupportedException) { /* 윈도우 외 — 무시 */ }

        using var window = CancellationTokenSource.CreateLinkedTokenSource(ct);
        window.CancelAfter(options.Window);
        var receive = ReceiveAsync(udp, messageId, found, window.Token);

        // UDP 는 잃을 수 있다 — WS-Discovery 권고대로 한 번 더 보낸다(같은 MessageID, 중복 응답은 합친다).
        for (int round = 0; round < 2 && !window.IsCancellationRequested; round++)
        {
            await SendAsync(udp, probe, options).ConfigureAwait(false);
            try { await Task.Delay(TimeSpan.FromMilliseconds(250), window.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }

        await receive.ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        lock (found)
            return found.Values.OrderBy(d => d.Host, StringComparer.OrdinalIgnoreCase).ThenBy(d => d.Port).ToList();
    }

    private static async Task SendAsync(UdpClient udp, byte[] probe, WsDiscoveryProbeOptions options)
    {
        if (options.Multicast)
        {
            var group = new IPEndPoint(WsDiscoveryProbeOptions.MulticastGroup, options.MulticastPort);
            foreach (var local in MulticastInterfaces())
            {
                try
                {
                    udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, local.GetAddressBytes());
                    await udp.SendAsync(probe, group).ConfigureAwait(false);
                }
                catch (SocketException) { /* 멀티캐스트 안 되는 인터페이스 — 다음 */ }
            }
        }
        foreach (var target in options.UnicastTargets)
        {
            try { await udp.SendAsync(probe, target).ConfigureAwait(false); }
            catch (SocketException) { /* 닿지 않는 대상 — 다음 */ }
        }
    }

    /// <summary>루프백 + 켜진 IPv4 인터페이스.</summary>
    private static IEnumerable<IPAddress> MulticastInterfaces()
    {
        yield return IPAddress.Loopback;
        IEnumerable<IPAddress> locals;
        try
        {
            locals = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                            && n.SupportsMulticast)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Select(u => u.Address)
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .ToList();
        }
        catch (NetworkInformationException) { yield break; }
        foreach (var a in locals) yield return a;
    }

    private static async Task ReceiveAsync(UdpClient udp, string messageId, Dictionary<string, DiscoveredOnvifDevice> found, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            UdpReceiveResult rx;
            try { rx = await udp.ReceiveAsync(token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }
            catch (SocketException) { continue; }

            IReadOnlyList<DiscoveredOnvifDevice> matches;
            try { matches = ParseProbeMatches(Encoding.UTF8.GetString(rx.Buffer), messageId); }
            catch (XmlException) { continue; }   // 다른 서비스의 깨진 데이터그램

            lock (found)
                foreach (var m in matches)
                    found[m.EndpointAddress] = found.TryGetValue(m.EndpointAddress, out var prev) ? Merge(prev, m) : m;
        }
    }

    private static DiscoveredOnvifDevice Merge(DiscoveredOnvifDevice a, DiscoveredOnvifDevice b) => a with
    {
        XAddrs = a.XAddrs.Union(b.XAddrs, StringComparer.OrdinalIgnoreCase).ToList(),
        Types = a.Types.Union(b.Types, StringComparer.Ordinal).ToList(),
        Scopes = a.Scopes.Union(b.Scopes, StringComparer.Ordinal).ToList(),
    };

    /// <summary>Probe 메시지(SOAP 1.2 · WS-Addressing 2004/08).</summary>
    public static string BuildProbe(string messageId) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        $"<s:Envelope xmlns:s=\"{NsSoap}\" xmlns:a=\"{NsWsa}\" xmlns:d=\"{NsWsd}\" xmlns:dn=\"{NsDn}\">" +
        "<s:Header>" +
        $"<a:MessageID>{System.Security.SecurityElement.Escape(messageId)}</a:MessageID>" +
        "<a:To>urn:schemas-xmlsoap-org:ws:2005:04:discovery</a:To>" +
        $"<a:Action>{NsWsd}/Probe</a:Action>" +
        "</s:Header>" +
        "<s:Body><d:Probe><d:Types>dn:NetworkVideoTransmitter</d:Types></d:Probe></s:Body>" +
        "</s:Envelope>";

    /// <summary>
    /// ProbeMatches → 장치 목록. Probe 나 Hello 등 다른 메시지면 빈 목록.
    /// <paramref name="relatesTo"/> 를 주면 RelatesTo 가 다른 응답(다른 클라이언트의 검색 응답)은 버린다 — RelatesTo 가 없는 장치는 받는다.
    /// 카메라만: Types 에 NetworkVideoTransmitter 가 없고 XAddrs 도 없는 항목은 버린다.
    /// </summary>
    public static IReadOnlyList<DiscoveredOnvifDevice> ParseProbeMatches(string xml, string? relatesTo = null)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root;
        if (root is null || root.Name.LocalName != "Envelope") return Array.Empty<DiscoveredOnvifDevice>();
        var header = Child(root, "Header");
        var related = header is null ? null : Child(header, "RelatesTo")?.Value.Trim();
        if (relatesTo is not null && !string.IsNullOrEmpty(related) && !string.Equals(related, relatesTo, StringComparison.OrdinalIgnoreCase))
            return Array.Empty<DiscoveredOnvifDevice>();

        var body = Child(root, "Body");
        var matches = body is null ? null : Child(body, "ProbeMatches");
        if (matches is null) return Array.Empty<DiscoveredOnvifDevice>();

        var list = new List<DiscoveredOnvifDevice>();
        foreach (var m in matches.Elements().Where(e => e.Name.LocalName == "ProbeMatch"))
        {
            var address = m.Elements().FirstOrDefault(e => e.Name.LocalName == "EndpointReference")
                           ?.Elements().FirstOrDefault(e => e.Name.LocalName == "Address")?.Value.Trim();
            var xaddrs = Split(Child(m, "XAddrs")?.Value);
            var types = Split(Child(m, "Types")?.Value);
            var scopes = Split(Child(m, "Scopes")?.Value);
            if (xaddrs.Count == 0) continue;
            // "dn:NetworkVideoTransmitter" · 접두어 없이 · 이름공간 URI 를 붙인 꼴 모두 끝 이름으로 본다.
            bool camera = types.Count == 0 || types.Any(t => t.EndsWith("NetworkVideoTransmitter", StringComparison.Ordinal));
            if (!camera) continue;
            list.Add(new DiscoveredOnvifDevice(string.IsNullOrEmpty(address) ? xaddrs[0] : address, xaddrs, types, scopes));
        }
        return list;
    }

    private static XElement? Child(XElement parent, string local) => parent.Elements().FirstOrDefault(e => e.Name.LocalName == local);

    private static List<string> Split(string? s) =>
        string.IsNullOrWhiteSpace(s) ? new List<string>() : s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
}
