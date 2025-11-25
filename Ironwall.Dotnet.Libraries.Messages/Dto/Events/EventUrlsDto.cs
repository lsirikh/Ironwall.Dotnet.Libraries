using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 이벤트 영상 URL 정보
/// </summary>
public class EventUrlsDto
{
    /// <summary>
    /// 실시간 영상 RTSP URL
    /// </summary>
    [JsonProperty("live")]
    public string Live { get; set; } = string.Empty;

    /// <summary>
    /// 녹화 영상 RTSP URL (재생 구간 포함)
    /// </summary>
    [JsonProperty("record")]
    public string Record { get; set; } = string.Empty;
}
