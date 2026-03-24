using Newtonsoft.Json;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public class CameraUrlsModel : ICameraUrlsModel
{
    [JsonProperty("homepage_url", Order = 1)]
    public string? HomepageUrl { get; set; }

    [JsonProperty("onvif_device_service", Order = 2)]
    public string? OnvifDeviceService { get; set; }

    [JsonProperty("rtsp_main", Order = 3)]
    public string? RtspMain { get; set; }

    [JsonProperty("rtsp_sub", Order = 4)]
    public string? RtspSub { get; set; }

    [JsonProperty("webrtc_main", Order = 5)]
    public string? WebrtcMain { get; set; }

    [JsonProperty("snapshot_ch1", Order = 6)]
    public string? SnapshotCh1 { get; set; }
}
