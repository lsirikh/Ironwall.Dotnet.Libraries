using System.Globalization;
using System.Security;
using DummyCameras.Infra;
using DummyCameras.Ptz;

namespace DummyCameras.Onvif;

/// <summary>
/// ONVIF response bodies (SOAP 1.2). Element ORDER follows the ONVIF schemas (onvif.xsd / devicemgmt / media / ptz):
/// the svcutil-generated proxies our client uses (XmlSerializer with explicit Order) skip out-of-order elements silently.
/// </summary>
internal static class OnvifXml
{
    public const string NsSoap = "http://www.w3.org/2003/05/soap-envelope";
    public const string NsTds = "http://www.onvif.org/ver10/device/wsdl";
    public const string NsTrt = "http://www.onvif.org/ver10/media/wsdl";
    public const string NsTptz = "http://www.onvif.org/ver20/ptz/wsdl";
    public const string NsTt = "http://www.onvif.org/ver10/schema";
    public const string NsTer = "http://www.onvif.org/ver10/error";

    public const string SpaceAbsPt = "http://www.onvif.org/ver10/tptz/PanTiltSpaces/PositionGenericSpace";
    public const string SpaceAbsZ = "http://www.onvif.org/ver10/tptz/ZoomSpaces/PositionGenericSpace";
    public const string SpaceRelPt = "http://www.onvif.org/ver10/tptz/PanTiltSpaces/TranslationGenericSpace";
    public const string SpaceRelZ = "http://www.onvif.org/ver10/tptz/ZoomSpaces/TranslationGenericSpace";
    public const string SpaceContPt = "http://www.onvif.org/ver10/tptz/PanTiltSpaces/VelocityGenericSpace";
    public const string SpaceContZ = "http://www.onvif.org/ver10/tptz/ZoomSpaces/VelocityGenericSpace";
    public const string SpaceSpeedPt = "http://www.onvif.org/ver10/tptz/PanTiltSpaces/GenericSpeedSpace";
    public const string SpaceSpeedZ = "http://www.onvif.org/ver10/tptz/ZoomSpaces/ZoomGenericSpeedSpace";

    public const string MainProfile = "Profile_1";
    public const string SubProfile = "Profile_2";
    public const string PtzNodeToken = "PTZNODE_1";
    public const string PtzConfigToken = "PTZCFG_1";

    public static string Envelope(string inner) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        $"<s:Envelope xmlns:s=\"{NsSoap}\" xmlns:tds=\"{NsTds}\" xmlns:trt=\"{NsTrt}\" xmlns:tptz=\"{NsTptz}\" xmlns:tt=\"{NsTt}\" xmlns:ter=\"{NsTer}\">" +
        "<s:Body>" + inner + "</s:Body></s:Envelope>";

    /// <summary>SOAP 1.2 fault. <paramref name="code"/> = "Sender" | "Receiver", subcode e.g. "ter:NotAuthorized".</summary>
    public static string Fault(string code, string subcode, string? subSubcode, string reason) =>
        Envelope(
            "<s:Fault><s:Code><s:Value>s:" + code + "</s:Value>" +
            "<s:Subcode><s:Value>" + subcode + "</s:Value>" +
            (subSubcode is null ? string.Empty : "<s:Subcode><s:Value>" + subSubcode + "</s:Value></s:Subcode>") +
            "</s:Subcode></s:Code>" +
            "<s:Reason><s:Text xml:lang=\"en\">" + Esc(reason) + "</s:Text></s:Reason></s:Fault>");

    public static string Esc(string? s) => SecurityElement.Escape(s ?? string.Empty) ?? string.Empty;

    public static string F(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

    // ── Device ────────────────────────────────────────────────────────────

    public static string SystemDateAndTime(DateTime utc) =>
        "<tds:GetSystemDateAndTimeResponse><tds:SystemDateAndTime>" +
        "<tt:DateTimeType>NTP</tt:DateTimeType><tt:DaylightSavings>false</tt:DaylightSavings>" +
        "<tt:TimeZone><tt:TZ>UTC0</tt:TZ></tt:TimeZone>" +
        $"<tt:UTCDateTime><tt:Time><tt:Hour>{utc.Hour}</tt:Hour><tt:Minute>{utc.Minute}</tt:Minute><tt:Second>{utc.Second}</tt:Second></tt:Time>" +
        $"<tt:Date><tt:Year>{utc.Year}</tt:Year><tt:Month>{utc.Month}</tt:Month><tt:Day>{utc.Day}</tt:Day></tt:Date></tt:UTCDateTime>" +
        "</tds:SystemDateAndTime></tds:GetSystemDateAndTimeResponse>";

    public static string DeviceInformation(CameraSpec cam) =>
        "<tds:GetDeviceInformationResponse>" +
        "<tds:Manufacturer>Ironwall DummyCam</tds:Manufacturer>" +
        $"<tds:Model>{(cam.IsPtz ? "DUMMY-PTZ" : "DUMMY-FIXED")}</tds:Model>" +
        "<tds:FirmwareVersion>1.0.0</tds:FirmwareVersion>" +
        $"<tds:SerialNumber>DUMMY-{cam.Index:0000}</tds:SerialNumber>" +
        $"<tds:HardwareId>dummycam-{cam.Index}</tds:HardwareId>" +
        "</tds:GetDeviceInformationResponse>";

    /// <summary>categories: "All" | "Device" | "Media" | "PTZ" | ... Returns null when a requested service does not exist.</summary>
    public static string? Capabilities(CameraSpec cam, IReadOnlyCollection<string> categories)
    {
        bool all = categories.Count == 0 || categories.Contains("All");
        bool Want(string c) => all || categories.Contains(c);
        if (!all && categories.All(c => c is not ("Device" or "Media" or "PTZ"))) return null;
        if (!all && Want("PTZ") && !cam.IsPtz && !Want("Media") && !Want("Device")) return null;   // ter:NoSuchService

        var sb = new System.Text.StringBuilder("<tds:GetCapabilitiesResponse><tds:Capabilities>");
        if (Want("Device"))
            sb.Append("<tt:Device><tt:XAddr>").Append(cam.DeviceServiceUrl).Append("</tt:XAddr>")
              .Append("<tt:Network><tt:IPFilter>false</tt:IPFilter><tt:ZeroConfiguration>false</tt:ZeroConfiguration><tt:IPVersion6>false</tt:IPVersion6><tt:DynDNS>false</tt:DynDNS></tt:Network>")
              .Append("<tt:System><tt:DiscoveryResolve>false</tt:DiscoveryResolve><tt:DiscoveryBye>false</tt:DiscoveryBye><tt:RemoteDiscovery>false</tt:RemoteDiscovery>")
              .Append("<tt:SystemBackup>false</tt:SystemBackup><tt:SystemLogging>false</tt:SystemLogging><tt:FirmwareUpgrade>false</tt:FirmwareUpgrade>")
              .Append("<tt:SupportedVersions><tt:Major>2</tt:Major><tt:Minor>60</tt:Minor></tt:SupportedVersions></tt:System>")
              .Append("</tt:Device>");
        if (Want("Media"))
            sb.Append("<tt:Media><tt:XAddr>").Append(cam.MediaServiceUrl).Append("</tt:XAddr>")
              .Append("<tt:StreamingCapabilities><tt:RTPMulticast>false</tt:RTPMulticast><tt:RTP_TCP>true</tt:RTP_TCP><tt:RTP_RTSP_TCP>true</tt:RTP_RTSP_TCP></tt:StreamingCapabilities>")
              .Append("</tt:Media>");
        if (Want("PTZ") && cam.IsPtz)
            sb.Append("<tt:PTZ><tt:XAddr>").Append(cam.PtzServiceUrl).Append("</tt:XAddr></tt:PTZ>");
        sb.Append("</tds:Capabilities></tds:GetCapabilitiesResponse>");
        return sb.ToString();
    }

    public static string Services(CameraSpec cam)
    {
        static string Svc(string ns, string xaddr) =>
            $"<tds:Service><tds:Namespace>{ns}</tds:Namespace><tds:XAddr>{xaddr}</tds:XAddr>" +
            "<tds:Version><tt:Major>2</tt:Major><tt:Minor>60</tt:Minor></tds:Version></tds:Service>";
        return "<tds:GetServicesResponse>" +
               Svc(NsTds, cam.DeviceServiceUrl) +
               Svc(NsTrt, cam.MediaServiceUrl) +
               (cam.IsPtz ? Svc(NsTptz, cam.PtzServiceUrl) : string.Empty) +
               "</tds:GetServicesResponse>";
    }

    public static string Scopes(CameraSpec cam)
    {
        static string S(string item) => $"<tds:Scopes><tt:ScopeDef>Fixed</tt:ScopeDef><tt:ScopeItem>{Esc(item)}</tt:ScopeItem></tds:Scopes>";
        return "<tds:GetScopesResponse>" +
               S("onvif://www.onvif.org/name/" + Uri.EscapeDataString(cam.Name)) +
               S("onvif://www.onvif.org/location/loopback") +
               S("onvif://www.onvif.org/hardware/DummyCam") +
               S("onvif://www.onvif.org/type/" + (cam.IsPtz ? "ptz" : "video_encoder")) +
               S("onvif://www.onvif.org/Profile/Streaming") +
               "</tds:GetScopesResponse>";
    }

    public static string NetworkInterfaces(CameraSpec cam) =>
        "<tds:GetNetworkInterfacesResponse><tds:NetworkInterfaces token=\"eth0\">" +
        "<tt:Enabled>true</tt:Enabled>" +
        $"<tt:Info><tt:Name>eth0</tt:Name><tt:HwAddress>02:00:00:00:00:{cam.Index:X2}</tt:HwAddress><tt:MTU>1500</tt:MTU></tt:Info>" +
        "</tds:NetworkInterfaces></tds:GetNetworkInterfacesResponse>";

    public static string Hostname(CameraSpec cam) =>
        $"<tds:GetHostnameResponse><tds:HostnameInformation><tt:FromDHCP>false</tt:FromDHCP><tt:Name>{cam.Id}</tt:Name></tds:HostnameInformation></tds:GetHostnameResponse>";

    // ── Media ─────────────────────────────────────────────────────────────

    public static IEnumerable<(string Token, int W, int H, string Uri)> ProfileList(CameraSpec cam)
    {
        yield return (MainProfile, cam.MainWidth, cam.MainHeight, cam.MainRtspUrl);
        if (cam.HasSub) yield return (SubProfile, cam.SubWidth, cam.SubHeight, cam.SubRtspUrl);
    }

    public static string Profile(CameraSpec cam, string token, int w, int h, string element = "trt:Profiles")
    {
        int bitrate = w >= 1280 ? 1500 : 300;
        var sb = new System.Text.StringBuilder();
        sb.Append('<').Append(element).Append(" token=\"").Append(token).Append("\" fixed=\"true\">")
          .Append("<tt:Name>").Append(token == MainProfile ? "mainStream" : "subStream").Append("</tt:Name>")
          .Append("<tt:VideoSourceConfiguration token=\"VSC_1\"><tt:Name>VideoSource_1</tt:Name><tt:UseCount>2</tt:UseCount>")
          .Append("<tt:SourceToken>VS_1</tt:SourceToken>")
          .Append($"<tt:Bounds x=\"0\" y=\"0\" width=\"{cam.MainWidth}\" height=\"{cam.MainHeight}\"/></tt:VideoSourceConfiguration>")
          .Append($"<tt:VideoEncoderConfiguration token=\"VEC_{token}\"><tt:Name>{token}_enc</tt:Name><tt:UseCount>1</tt:UseCount>")
          .Append("<tt:Encoding>H264</tt:Encoding>")
          .Append($"<tt:Resolution><tt:Width>{w}</tt:Width><tt:Height>{h}</tt:Height></tt:Resolution>")
          .Append("<tt:Quality>5</tt:Quality>")
          .Append($"<tt:RateControl><tt:FrameRateLimit>{cam.Fps}</tt:FrameRateLimit><tt:EncodingInterval>1</tt:EncodingInterval><tt:BitrateLimit>{bitrate}</tt:BitrateLimit></tt:RateControl>")
          .Append($"<tt:H264><tt:GovLength>{cam.Fps * 2}</tt:GovLength><tt:H264Profile>Main</tt:H264Profile></tt:H264>")
          .Append("<tt:Multicast><tt:Address><tt:Type>IPv4</tt:Type><tt:IPv4Address>0.0.0.0</tt:IPv4Address></tt:Address><tt:Port>0</tt:Port><tt:TTL>1</tt:TTL><tt:AutoStart>false</tt:AutoStart></tt:Multicast>")
          .Append("<tt:SessionTimeout>PT60S</tt:SessionTimeout></tt:VideoEncoderConfiguration>");
        if (cam.IsPtz) sb.Append(PtzConfiguration("tt:PTZConfiguration"));
        sb.Append("</").Append(element).Append('>');
        return sb.ToString();
    }

    public static string Profiles(CameraSpec cam) =>
        "<trt:GetProfilesResponse>" + string.Concat(ProfileList(cam).Select(p => Profile(cam, p.Token, p.W, p.H))) + "</trt:GetProfilesResponse>";

    public static string StreamUri(string uri) =>
        "<trt:GetStreamUriResponse><trt:MediaUri>" +
        $"<tt:Uri>{Esc(uri)}</tt:Uri><tt:InvalidAfterConnect>false</tt:InvalidAfterConnect>" +
        "<tt:InvalidAfterReboot>false</tt:InvalidAfterReboot><tt:Timeout>PT0S</tt:Timeout>" +
        "</trt:MediaUri></trt:GetStreamUriResponse>";

    public static string VideoSources(CameraSpec cam) =>
        "<trt:GetVideoSourcesResponse><trt:VideoSources token=\"VS_1\">" +
        $"<tt:Framerate>{cam.Fps}</tt:Framerate><tt:Resolution><tt:Width>{cam.MainWidth}</tt:Width><tt:Height>{cam.MainHeight}</tt:Height></tt:Resolution>" +
        "</trt:VideoSources></trt:GetVideoSourcesResponse>";

    // ── PTZ ───────────────────────────────────────────────────────────────

    public static string PtzConfiguration(string element) =>
        $"<{element} token=\"{PtzConfigToken}\"><tt:Name>PTZ</tt:Name><tt:UseCount>2</tt:UseCount>" +
        $"<tt:NodeToken>{PtzNodeToken}</tt:NodeToken>" +
        $"<tt:DefaultAbsolutePantTiltPositionSpace>{SpaceAbsPt}</tt:DefaultAbsolutePantTiltPositionSpace>" +
        $"<tt:DefaultAbsoluteZoomPositionSpace>{SpaceAbsZ}</tt:DefaultAbsoluteZoomPositionSpace>" +
        $"<tt:DefaultRelativePanTiltTranslationSpace>{SpaceRelPt}</tt:DefaultRelativePanTiltTranslationSpace>" +
        $"<tt:DefaultRelativeZoomTranslationSpace>{SpaceRelZ}</tt:DefaultRelativeZoomTranslationSpace>" +
        $"<tt:DefaultContinuousPanTiltVelocitySpace>{SpaceContPt}</tt:DefaultContinuousPanTiltVelocitySpace>" +
        $"<tt:DefaultContinuousZoomVelocitySpace>{SpaceContZ}</tt:DefaultContinuousZoomVelocitySpace>" +
        $"<tt:DefaultPTZSpeed><tt:PanTilt x=\"1\" y=\"1\" space=\"{SpaceSpeedPt}\"/><tt:Zoom x=\"1\" space=\"{SpaceSpeedZ}\"/></tt:DefaultPTZSpeed>" +
        "<tt:DefaultPTZTimeout>PT5S</tt:DefaultPTZTimeout>" +
        $"<tt:PanTiltLimits><tt:Range><tt:URI>{SpaceAbsPt}</tt:URI>{Range("XRange", -1, 1)}{Range("YRange", -1, 1)}</tt:Range></tt:PanTiltLimits>" +
        $"<tt:ZoomLimits><tt:Range><tt:URI>{SpaceAbsZ}</tt:URI>{Range("XRange", 0, 1)}</tt:Range></tt:ZoomLimits>" +
        $"</{element}>";

    public static string Configurations() =>
        "<tptz:GetConfigurationsResponse>" + PtzConfiguration("tptz:PTZConfiguration") + "</tptz:GetConfigurationsResponse>";

    public static string Configuration() =>
        "<tptz:GetConfigurationResponse>" + PtzConfiguration("tptz:PTZConfiguration") + "</tptz:GetConfigurationResponse>";

    private static string Range(string el, double min, double max) =>
        $"<tt:{el}><tt:Min>{F(min)}</tt:Min><tt:Max>{F(max)}</tt:Max></tt:{el}>";

    private static string Space2D(string el, string uri, double min, double max) =>
        $"<tt:{el}><tt:URI>{uri}</tt:URI>{Range("XRange", min, max)}{Range("YRange", min, max)}</tt:{el}>";

    private static string Space1D(string el, string uri, double min, double max) =>
        $"<tt:{el}><tt:URI>{uri}</tt:URI>{Range("XRange", min, max)}</tt:{el}>";

    public static string Node(string element, int maxPresets) =>
        $"<{element} token=\"{PtzNodeToken}\" FixedHomePosition=\"false\"><tt:Name>PTZNode</tt:Name><tt:SupportedPTZSpaces>" +
        Space2D("AbsolutePanTiltPositionSpace", SpaceAbsPt, -1, 1) +
        Space1D("AbsoluteZoomPositionSpace", SpaceAbsZ, 0, 1) +
        Space2D("RelativePanTiltTranslationSpace", SpaceRelPt, -1, 1) +
        Space1D("RelativeZoomTranslationSpace", SpaceRelZ, -1, 1) +
        Space2D("ContinuousPanTiltVelocitySpace", SpaceContPt, -1, 1) +
        Space1D("ContinuousZoomVelocitySpace", SpaceContZ, -1, 1) +
        Space1D("PanTiltSpeedSpace", SpaceSpeedPt, 0, 1) +
        Space1D("ZoomSpeedSpace", SpaceSpeedZ, 0, 1) +
        $"</tt:SupportedPTZSpaces><tt:MaximumNumberOfPresets>{maxPresets}</tt:MaximumNumberOfPresets><tt:HomeSupported>true</tt:HomeSupported>" +
        $"</{element}>";

    public static string Position(PtzVector p) =>
        $"<tt:PanTilt x=\"{F(p.Pan)}\" y=\"{F(p.Tilt)}\" space=\"{SpaceAbsPt}\"/><tt:Zoom x=\"{F(p.Zoom)}\" space=\"{SpaceAbsZ}\"/>";

    public static string Status(PtzSnapshot s, DateTime utc) =>
        "<tptz:GetStatusResponse><tptz:PTZStatus>" +
        "<tt:Position>" + Position(s.Position) + "</tt:Position>" +
        $"<tt:MoveStatus><tt:PanTilt>{(s.PanTiltMoving ? "MOVING" : "IDLE")}</tt:PanTilt><tt:Zoom>{(s.ZoomMoving ? "MOVING" : "IDLE")}</tt:Zoom></tt:MoveStatus>" +
        $"<tt:UtcTime>{utc:yyyy-MM-ddTHH:mm:ss.fffZ}</tt:UtcTime>" +
        "</tptz:PTZStatus></tptz:GetStatusResponse>";

    public static string Presets(IEnumerable<PtzPreset> presets) =>
        "<tptz:GetPresetsResponse>" +
        string.Concat(presets.Select(p =>
            $"<tptz:Preset token=\"{Esc(p.Token)}\"><tt:Name>{Esc(p.Name)}</tt:Name><tt:PTZPosition>{Position(p.Position)}</tt:PTZPosition></tptz:Preset>")) +
        "</tptz:GetPresetsResponse>";
}
