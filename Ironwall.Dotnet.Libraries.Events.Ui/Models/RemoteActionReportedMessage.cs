namespace Ironwall.Dotnet.Libraries.Events.Ui.Models;

/****************************************************************************
   Purpose      : 원격 조치보고(NATS ACTION_REPORT) 를 받았다 — 이 종류 · 번호의 이벤트는 이미 조치됐다.
                  열린 조치보고 창이 같은 이벤트를 들고 있으면 [확인] 을 끄고 "다른 운영자가 이미 조치했습니다" 를 보인다
                  — 모른 채 [확인] 을 누르면 서버에 두 번째 조치가 생긴다(WP-1 ③).
   Created By   : Claude
   Created On   : 2026-09-30
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
/// <param name="Kind"><see cref="Services.ActionReportKind"/> 값("detection" · "malfunction").</param>
/// <param name="EventId">원본 이벤트 서버 id.</param>
public sealed record RemoteActionReportedMessage(string Kind, int EventId);

/// <summary>원격 조치보고를 받아 카드 목록이 한 일.</summary>
public enum RemoteActionReportOutcome
{
    /// <summary>원본 id 가 없거나 0 이하 — 아무것도 하지 않았다.</summary>
    Invalid,
    /// <summary>종류를 모르는데 같은 번호의 카드가 둘 이상 — 잘못 닫지 않으려고 아무것도 닫지 않았다.</summary>
    Ambiguous,
    /// <summary>목록의 카드를 닫았다(큐 · 심볼도 풀었다).</summary>
    CardClosed,
    /// <summary>카드는 없었지만 큐에 남은 엔트리를 풀었다(심볼 복원).</summary>
    QueueEntryCleared,
    /// <summary>카드도 엔트리도 없어 장비 · 그룹 심볼을 큐 실제 상태로 다시 계산했다.</summary>
    SymbolRefreshed,
    /// <summary>풀 것이 없었다(이미 닫힘 · 이 GIS 가 모르는 이벤트 · 자기 발행분의 메아리).</summary>
    Nothing,
}
