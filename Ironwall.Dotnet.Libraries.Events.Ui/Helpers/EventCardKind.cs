using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Helpers;

/****************************************************************************
   Purpose      : 이벤트 카드의 '종류'(탐지 · 장애) — 카드 · 큐 엔트리 · 원격 조치보고를 같은 열쇠로 맞춘다.
                  서버는 탐지와 장애를 <b>따로 번호 매긴다</b>(탐지 7번과 장애 7번이 함께 있을 수 있다).
                  그래서 숫자 id 하나로 카드를 고르면 엉뚱한 종류의 카드를 닫거나 EntryId 를 넘겨준다(WP-1 ②).
                  열쇠 값은 <see cref="ActionReportKind"/>(= 브로커 from_event.category_event 값 "detection" · "malfunction")와 같다.
   Created By   : Claude
   Created On   : 2026-09-30
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public static class EventCardKind
{
    /// <summary>카드의 종류. 탐지 · 장애 카드가 아니면 <c>null</c>.</summary>
    public static string? Of(EventCardBaseViewModel? card) => card switch
    {
        MalfunctionEventCardViewModel => ActionReportKind.Malfunction,
        DetectionEventCardViewModel => ActionReportKind.Detection,
        _ => null,
    };

    /// <summary>큐 엔트리 · 이벤트 유형의 종류 — 장애(Fault)만 장애, 나머지(침입 · 접점 …)는 탐지.</summary>
    public static string Of(EnumEventType eventType)
        => eventType == EnumEventType.Fault ? ActionReportKind.Malfunction : ActionReportKind.Detection;

    /// <summary>
    /// 원격 조치보고(ACTION_REPORT) 의 원본 종류 — <c>from_event.category_event</c> 를 먼저, 없으면 <c>type_event</c> 로 가른다.
    /// 둘 다 못 쓰면 <c>null</c>(모른다 — 부르는 쪽은 번호가 하나로 정해질 때만 닫는다).
    /// </summary>
    public static string? FromActionReport(string? categoryEvent, string? typeEvent)
    {
        if (string.Equals(categoryEvent, ActionReportKind.Detection, StringComparison.OrdinalIgnoreCase)) return ActionReportKind.Detection;
        if (string.Equals(categoryEvent, ActionReportKind.Malfunction, StringComparison.OrdinalIgnoreCase)) return ActionReportKind.Malfunction;

        if (!Enum.TryParse<EnumEventType>(typeEvent, ignoreCase: true, out var type)) return null;
        return type switch
        {
            EnumEventType.Fault => ActionReportKind.Malfunction,
            EnumEventType.Action or EnumEventType.Connection or EnumEventType.None => null,
            _ => ActionReportKind.Detection,
        };
    }

    /// <summary>원격 조치보고의 원본 이벤트 유형(<c>type_event</c>) — 못 읽으면 종류의 기본 유형(장애=Fault · 탐지=Intrusion).</summary>
    public static EnumEventType? EventTypeOf(string? kind, string? typeEvent)
    {
        if (Enum.TryParse<EnumEventType>(typeEvent, ignoreCase: true, out var type) && type != EnumEventType.None) return type;
        return kind switch
        {
            ActionReportKind.Malfunction => EnumEventType.Fault,
            ActionReportKind.Detection => EnumEventType.Intrusion,
            _ => null,
        };
    }

    /// <summary>자동화 식별자 가운데 토막 — "Detection" · "Malfunction" (계층형 <c>Events.Card.{kind}.{eventId}</c>).</summary>
    public static string AutomationSegment(string? kind) => kind switch
    {
        ActionReportKind.Detection => "Detection",
        ActionReportKind.Malfunction => "Malfunction",
        _ => "Event",
    };
}
