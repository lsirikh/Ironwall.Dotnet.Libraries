using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DummyCameras.Infra;

namespace DummyCameras.Onvif;

/// <summary>
/// WS-Discovery (ONVIF Core §7, WS-Discovery 2005/04) target service for the whole farm: answers a Probe with one
/// ProbeMatches message per camera (XAddrs = the camera's device_service), so an ONVIF "search" finds the dummies.
/// One socket serves every camera (a real LAN has one device per IP; here all share 127.0.0.1 and differ by port).
/// <list type="bullet">
/// <item>Multicast (default CLI): bind 0.0.0.0:3702 (shared - Windows' own discovery service may hold it too), join
/// 239.255.255.250, answer only probes coming from this PC (a search here finds them; the LAN never sees 127.0.0.1 cameras).</item>
/// <item>Unicast only (tests): bind 127.0.0.1:0 - directed probes to <see cref="Port"/>; 3702 and multicast are untouched.</item>
/// </list>
/// Every probe is logged to onvif-requests.jsonl (cam = "farm", op = "Probe", result = matches sent).
/// </summary>
public sealed class WsDiscoveryResponder : IAsyncDisposable
{
    public const int DefaultPort = 3702;
    public static readonly IPAddress MulticastGroup = IPAddress.Parse("239.255.255.250");

    public const string NsSoap = "http://www.w3.org/2003/05/soap-envelope";
    public const string NsWsa = "http://schemas.xmlsoap.org/ws/2004/08/addressing";
    public const string NsWsd = "http://schemas.xmlsoap.org/ws/2005/04/discovery";
    public const string NsDn = "http://www.onvif.org/ver10/network/wsdl";
    public const string NsTds = "http://www.onvif.org/ver10/device/wsdl";
    private const string ActionProbe = NsWsd + "/Probe";
    private const string ActionProbeMatches = NsWsd + "/ProbeMatches";
    private const string ToAnonymous = "http://schemas.xmlsoap.org/ws/2004/08/addressing/role/anonymous";

    private readonly Func<IReadOnlyList<CameraSpec>> _cameras;
    private readonly JsonlLog _log;
    private readonly UdpClient _udp;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    private long _probeCount;

    /// <param name="cameras">Current camera list (read on every probe).</param>
    /// <param name="port">UDP port (3702 = standard · 0 = free port, tests).</param>
    /// <param name="multicast">true = bind any address and join 239.255.255.250; false = loopback unicast only.</param>
    public WsDiscoveryResponder(Func<IReadOnlyList<CameraSpec>> cameras, JsonlLog log, int port = DefaultPort, bool multicast = true)
    {
        _cameras = cameras;
        _log = log;
        _udp = new UdpClient(AddressFamily.InterNetwork);
        _udp.Client.ExclusiveAddressUse = false;
        _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udp.Client.Bind(new IPEndPoint(multicast ? IPAddress.Any : IPAddress.Loopback, port));
        IsMulticast = multicast;
        if (multicast) Join();
        Port = ((IPEndPoint)_udp.Client.LocalEndPoint!).Port;
        _loop = Task.Run(LoopAsync);
    }

    public int Port { get; }
    public bool IsMulticast { get; }
    public long ProbeCount => Interlocked.Read(ref _probeCount);

    /// <summary>Stable per-camera endpoint reference (same camera index + port → same urn:uuid across runs).</summary>
    public static string EndpointAddress(CameraSpec cam)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"ironwall-dummycam-{cam.Index}-{cam.OnvifPort}"));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);   // RFC 4122 version 3 (name based, MD5)
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return "urn:uuid:" + new Guid(hash).ToString();
    }

    /// <summary>
    /// Join the group on loopback and on every local IPv4 interface: a client's multicast may leave through any of them
    /// (multicast loopback brings it back to this PC). Which probes are ANSWERED is decided by <see cref="IsLocal"/>.
    /// </summary>
    private void Join()
    {
        _udp.JoinMulticastGroup(MulticastGroup, IPAddress.Loopback);
        foreach (var address in LocalIPv4())
        {
            try { _udp.JoinMulticastGroup(MulticastGroup, address); }
            catch (SocketException) { /* interface without multicast - skip */ }
        }
    }

    private static IEnumerable<IPAddress> LocalIPv4() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Select(u => u.Address)
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork);

    /// <summary>
    /// Loopback farm: only probes sent from THIS PC are answered. The XAddrs say 127.0.0.1, so answering another PC on the
    /// LAN would advertise cameras it cannot reach (and put fake cameras in a field operator's search).
    /// </summary>
    private static bool IsLocal(IPAddress from)
    {
        if (IPAddress.IsLoopback(from)) return true;
        try { return LocalIPv4().Contains(from); }
        catch (NetworkInformationException) { return false; }
    }

    private async Task LoopAsync()
    {
        var ct = _cts.Token;
        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult rx;
            try { rx = await _udp.ReceiveAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }
            catch (SocketException) { continue; }   // ICMP port unreachable from an earlier reply (WSAECONNRESET)
            if (!IsLocal(rx.RemoteEndPoint.Address)) continue;

            string? messageId;
            bool wanted;
            try
            {
                var probe = ParseProbe(Encoding.UTF8.GetString(rx.Buffer));
                if (probe is null) continue;   // Hello / Bye / Resolve / other services' traffic - not ours to answer
                (messageId, wanted) = probe.Value;
            }
            catch (XmlException) { continue; }

            Interlocked.Increment(ref _probeCount);
            var cams = wanted ? _cameras() : Array.Empty<CameraSpec>();
            foreach (var cam in cams)
            {
                var bytes = Encoding.UTF8.GetBytes(ProbeMatches(cam, messageId));
                try { await _udp.SendAsync(bytes, rx.RemoteEndPoint, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
                catch (Exception ex) when (ex is SocketException or ObjectDisposedException) { break; }
            }
            _log.Write(new Dictionary<string, object?>
            {
                ["ts"] = DateTime.UtcNow.ToString("O"),
                ["cam"] = "farm",
                ["port"] = Port,
                ["path"] = "udp",
                ["op"] = "Probe",
                ["result"] = wanted ? $"ok matches={cams.Count}" : "ignored (types)",
                ["from"] = rx.RemoteEndPoint.Address.ToString(),
            });
        }
    }

    /// <summary>
    /// A WS-Discovery Probe → (MessageID, do our types match). null when the datagram is not a Probe.
    /// Types: empty or any of NetworkVideoTransmitter / Device (any prefix) match - field cameras answer both.
    /// </summary>
    public static (string? MessageId, bool Wanted)? ParseProbe(string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root;
        if (root is null || root.Name.LocalName != "Envelope") return null;
        var body = root.Elements().FirstOrDefault(e => e.Name.LocalName == "Body");
        var probe = body?.Elements().FirstOrDefault(e => e.Name.LocalName == "Probe");
        if (probe is null) return null;
        var header = root.Elements().FirstOrDefault(e => e.Name.LocalName == "Header");
        var messageId = header?.Elements().FirstOrDefault(e => e.Name.LocalName == "MessageID")?.Value.Trim();
        var types = probe.Elements().FirstOrDefault(e => e.Name.LocalName == "Types")?.Value ?? string.Empty;
        var names = types.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Contains(':') ? t[(t.LastIndexOf(':') + 1)..] : t)
            .ToList();
        bool wanted = names.Count == 0 || names.Any(n => n is "NetworkVideoTransmitter" or "Device");
        return (messageId, wanted);
    }

    /// <summary>One ProbeMatches message for a camera (SOAP 1.2, WS-Addressing 2004/08).</summary>
    public static string ProbeMatches(CameraSpec cam, string? relatesTo)
    {
        var scopes = string.Join(' ', new[]
        {
            "onvif://www.onvif.org/name/" + Uri.EscapeDataString(cam.Name),
            "onvif://www.onvif.org/location/loopback",
            "onvif://www.onvif.org/hardware/" + (cam.IsPtz ? "DUMMY-PTZ" : "DUMMY-FIXED"),
            "onvif://www.onvif.org/type/" + (cam.IsPtz ? "ptz" : "video_encoder"),
            "onvif://www.onvif.org/Profile/Streaming",
        });
        return
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            $"<s:Envelope xmlns:s=\"{NsSoap}\" xmlns:a=\"{NsWsa}\" xmlns:d=\"{NsWsd}\" xmlns:dn=\"{NsDn}\" xmlns:tds=\"{NsTds}\">" +
            "<s:Header>" +
            $"<a:MessageID>urn:uuid:{Guid.NewGuid()}</a:MessageID>" +
            (string.IsNullOrEmpty(relatesTo) ? string.Empty : $"<a:RelatesTo>{OnvifXml.Esc(relatesTo)}</a:RelatesTo>") +
            $"<a:To>{ToAnonymous}</a:To>" +
            $"<a:Action>{ActionProbeMatches}</a:Action>" +
            "</s:Header>" +
            "<s:Body><d:ProbeMatches><d:ProbeMatch>" +
            $"<a:EndpointReference><a:Address>{EndpointAddress(cam)}</a:Address></a:EndpointReference>" +
            "<d:Types>dn:NetworkVideoTransmitter tds:Device</d:Types>" +
            $"<d:Scopes>{OnvifXml.Esc(scopes)}</d:Scopes>" +
            $"<d:XAddrs>{OnvifXml.Esc(cam.DeviceServiceUrl)}</d:XAddrs>" +
            "<d:MetadataVersion>1</d:MetadataVersion>" +
            "</d:ProbeMatch></d:ProbeMatches></s:Body></s:Envelope>";
    }

    /// <summary>The Probe a client sends (used by tests that talk to the responder without the GIS client).</summary>
    public static string Probe(string messageId, string types = "dn:NetworkVideoTransmitter") =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        $"<s:Envelope xmlns:s=\"{NsSoap}\" xmlns:a=\"{NsWsa}\" xmlns:d=\"{NsWsd}\" xmlns:dn=\"{NsDn}\" xmlns:tds=\"{NsTds}\">" +
        $"<s:Header><a:MessageID>{messageId}</a:MessageID><a:To>urn:schemas-xmlsoap-org:ws:2005:04:discovery</a:To><a:Action>{ActionProbe}</a:Action></s:Header>" +
        $"<s:Body><d:Probe><d:Types>{types}</d:Types></d:Probe></s:Body></s:Envelope>";

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _udp.Dispose();
        try { await _loop.ConfigureAwait(false); } catch { /* shutting down */ }
        _cts.Dispose();
    }
}
