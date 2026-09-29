using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Monitoring.Models.Comms;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Helpers;

/****************************************************************************
   Purpose      : 조치보고 NATS 발행 요청(SendActionRequestMessage)을 만드는 <b>한 곳</b>(WP-1 ⑳).
                  카드(탐지 · 장애) · 전체 조치보고 · 자동 조치보고 · 자동복구 · 조치 트레이(카드 경유)가 모두 이 함수를 지난다.
                  서버 201 의 data 원문을 실어 보내 호스트가 그것을 ACTION_REPORT body 로 <b>그대로</b> 싣게 한다
                  (브로커 명세 v2.0.7 §6.4 "GET /events/actions/{id} data 그대로 · 장비 블록 조립 금지").
   Created By   : Claude
   Created On   : 2026-09-30
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public static class ActionReportMessages
{
    /// <param name="response">조치 생성 응답(성공한 것). <see cref="ApiResponse{T}.RawData"/> 가 body 원문이다.</param>
    /// <param name="eventId">원본 이벤트 서버 id.</param>
    /// <param name="eventType">원본 이벤트 유형(RTSP_EVENTCALL OFF 캐시 열쇠).</param>
    /// <param name="content">조치 내용.</param>
    /// <param name="user">보고자(<see cref="ActionReportRules.FormatActor"/> 모양).</param>
    /// <param name="origin">원본 이벤트 모델 — 원문이 없을 때 호스트가 옛 조립으로 폴백하는 재료.</param>
    public static SendActionRequestMessage Create(ApiResponse<ActionEventDto>? response, int eventId, EnumEventType eventType,
                                                  string? content, string? user, IExEventModel? origin)
        => new()
        {
            EventId = eventId,
            EventType = eventType,
            ActionDetails = content,
            ActionUser = user,
            ActionTime = DateTime.Now,
            OriginEvent = origin,                                  // 폴백 조립 재료(원문이 없을 때만 쓰인다)
            ActionId = response?.Data?.Id ?? 0,                    // 생성된 Action DB ID
            ServerActionJson = response?.RawData?.ToString(Formatting.None),
        };
}
