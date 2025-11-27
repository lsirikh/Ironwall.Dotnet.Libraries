using Newtonsoft.Json;
using Wpf.Pids.Proxy.Master.DTO.Integrations;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// NATS 메시지 Body 전체 구조 (Detection 이벤트)
/// <para>origin_event로 DetectionEventDto를 포함하는 래퍼 클래스</para>
/// </summary>
public class DetectionExEventDto
{
    /// <summary>
    /// 이벤트 명칭
    /// </summary>
    [JsonProperty("name_event")]
    public string NameEvent { get; set; } = string.Empty;

    /// <summary>
    /// 이벤트 구분 (예: "DETECT_SENSOR_WITH_CAMERA")
    /// </summary>
    [JsonProperty("category_event")]
    public string CategoryEvent { get; set; } = string.Empty;

    /// <summary>
    /// 원본 탐지 이벤트 정보
    /// </summary>
    [JsonProperty("origin_event")]
    public DetectionEventDto OriginEvent { get; set; } = new DetectionEventDto();

    /// <summary>
    /// 카메라별 프리셋 정보 및 영상 URL 정보 (live/record RTSP)
    /// </summary>
    [JsonProperty("camera_presets")]
    public List<CameraEventPresetDto> CameraPresets = new List<CameraEventPresetDto>();
}
