using Ironwall.Dotnet.Libraries.Messages.Defines.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;

namespace Ironwall.Dotnet.Libraries.Messages.Helpers;

/// <summary>
/// DetectionExEventDto 전용 확장 메서드
/// <para>NATS 메시지 Body 구조에 맞는 인스턴스화 패턴 제공</para>
/// </summary>
public static class DetectionExEventDtoHelper
{
    /// <summary>
    /// DetectionEventDto를 DetectionExEventDto로 래핑
    /// </summary>
    /// <param name="origin">원본 DetectionEventDto</param>
    /// <param name="eventName">이벤트 명칭 (예: "침입탐지-001")</param>
    /// <param name="category">이벤트 카테고리 (예: "DETECT_SENSOR_WITH_CAMERA")</param>
    /// <param name="liveUrl">실시간 RTSP URL (선택)</param>
    /// <param name="recordUrl">녹화 RTSP URL (선택)</param>
    /// <returns>DetectionExEventDto 인스턴스</returns>
    public static DetectionExEventDto ToDetectionExEvent(
        this DetectionEventDto origin,
        string eventName,
        string category,
        string? liveUrl = null,
        string? recordUrl = null)
    {
        return new DetectionExEventDto
        {
            NameEvent = eventName,
            CategoryEvent = category,
            OriginEvent = origin,
            Urls = CreateEventUrls(liveUrl, recordUrl)
        };
    }

    /// <summary>
    /// EventUrlsDto 생성 헬퍼
    /// </summary>
    /// <param name="liveUrl">실시간 RTSP URL</param>
    /// <param name="recordUrl">녹화 RTSP URL</param>
    /// <returns>EventUrlsDto 인스턴스</returns>
    public static EventUrlsDto CreateEventUrls(
        string? liveUrl = null,
        string? recordUrl = null)
    {
        return new EventUrlsDto
        {
            Live = liveUrl ?? string.Empty,
            Record = recordUrl ?? string.Empty
        };
    }

    /// <summary>
    /// DetectionExEventDto → BrokerRequest 변환
    /// </summary>
    /// <param name="dto">DetectionExEventDto</param>
    /// <param name="from">발신자 식별자</param>
    /// <param name="command">NATS 명령어 (기본값: "DETECTION_EX_EVENT")</param>
    /// <returns>BrokerRequest&lt;DetectionExEventDto&gt;</returns>
    public static BrokerRequest<DetectionExEventDto> ToBrokerRequest(
        this DetectionExEventDto dto,
        string from,
        string command = "DETECTION_EX_EVENT")
    {
        return new BrokerRequest<DetectionExEventDto>
        {
            Id = Guid.NewGuid().ToString(),
            TypeMessage = "REQ",
            Command = command,
            From = from,
            Data = dto,
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };
    }
}
