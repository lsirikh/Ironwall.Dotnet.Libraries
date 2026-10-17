namespace Ironwall.Dotnet.Libraries.Events.Ui.Models;
/****************************************************************************
   Purpose      : SYNC_DETECTION{UPDATED} 재조회 결과를 실시간 탐지 카드에 전달하는 브릿지 메시지.
                  DetectionSyncNatsService(NATS/백그라운드)가 UI 스레드에서 발행 →
                  EventCardListPanelViewModel이 EventId로 활성 카드를 찾아 썸네일 in-place 갱신.
   Created By   : GHLee
   Created On   : 2026-07-31
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 탐지 이벤트(EventId=서버 detection id)의 회전 후 썸네일/프레임 크기가 갱신되었음을 알리는 메시지.
/// GET /events/detections/{id}의 detail.thumbnail/frame_width/frame_height를 실어 나른다.
/// </summary>
public class DetectionThumbnailSyncedMessage
{
    /// <summary>서버 탐지 이벤트 ID(= body.resource_id, EventEntry.EventId, Card.Model.Id).</summary>
    public int EventId { get; }

    /// <summary>회전 후 썸네일 URL(detail.thumbnail). null이면 표시 없음.</summary>
    public string? Thumbnail { get; }

    /// <summary>AI 추론 프레임 폭(px, detail.frame_width). optional.</summary>
    public int? FrameWidth { get; }

    /// <summary>AI 추론 프레임 높이(px, detail.frame_height). optional.</summary>
    public int? FrameHeight { get; }

    public DetectionThumbnailSyncedMessage(int eventId, string? thumbnail, int? frameWidth, int? frameHeight)
    {
        EventId = eventId;
        Thumbnail = thumbnail;
        FrameWidth = frameWidth;
        FrameHeight = frameHeight;
    }
}
