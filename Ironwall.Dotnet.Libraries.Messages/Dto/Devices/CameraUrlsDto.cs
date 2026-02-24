using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 카메라 URL DTO (중첩 구조)
/// </summary>
public class CameraUrlsDto
{
    [JsonProperty("homepage")]
    public CameraHomepageDto? Homepage { get; set; }

    [JsonProperty("onvif")]
    public CameraOnvifDto? Onvif { get; set; }

    [JsonProperty("streams")]
    public CameraStreamsDto? Streams { get; set; }

    [JsonProperty("snapshot")]
    public CameraSnapshotDto? Snapshot { get; set; }
}

public class CameraHomepageDto
{
    [JsonProperty("url")]
    public string Url { get; set; } = string.Empty;
}

public class CameraOnvifDto
{
    [JsonProperty("device_service")]
    public string DeviceService { get; set; } = string.Empty;
}

public class CameraStreamsDto
{
    [JsonProperty("rtsp")]
    public CameraRtspDto? Rtsp { get; set; }

    [JsonProperty("webrtc")]
    public CameraWebrtcDto? Webrtc { get; set; }
}

public class CameraRtspDto
{
    [JsonProperty("main")]
    public string Main { get; set; } = string.Empty;

    [JsonProperty("sub")]
    public string Sub { get; set; } = string.Empty;
}

public class CameraWebrtcDto
{
    [JsonProperty("main")]
    public string Main { get; set; } = string.Empty;
}

public class CameraSnapshotDto
{
    [JsonProperty("ch1")]
    public string Ch1 { get; set; } = string.Empty;
}
