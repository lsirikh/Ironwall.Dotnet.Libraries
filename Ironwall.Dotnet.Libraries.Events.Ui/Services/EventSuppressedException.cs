namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;

/****************************************************************************
   Purpose      : 이벤트 생성이 정비 창(억제 스케줄)에 걸려 서버가 202 로 받기만 하고 저장하지 않았다(G40, 명세 §3.3 · §6.8.10).
                  실패가 아니다 — 패널은 실패 건수에 넣지 않고, Draft 를 정리한 뒤 "정비 창(schedule_id N)에 걸려 저장되지 않았습니다" 를 보인다.
                  종전엔 202 의 data 가 비어 있어 "생성 실패" 예외로 흘렀고, Draft 가 남아 저장할 때마다 다시 보내졌다.
   Created By   : Claude
   Created On   : 2026-10-21
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public sealed class EventSuppressedException : Exception
{
    public EventSuppressedException(int? scheduleId, string? serverMessage)
        : base($"억제 창(schedule_id={scheduleId?.ToString() ?? "없음"})에 걸려 저장되지 않음: {serverMessage}")
    {
        ScheduleId = scheduleId;
        ServerMessage = serverMessage;
    }

    /// <summary>억제한 스케줄 id. 서버가 <c>null</c> 을 줄 수 있다(<c>integer|null</c>).</summary>
    public int? ScheduleId { get; }

    /// <summary>서버 <c>message</c> 원문(로그용).</summary>
    public string? ServerMessage { get; }

    /// <summary>
    /// 패널 통지용 한 줄 — 억제된 건이 없으면 <c>null</c>.
    /// 예: "정비 창(schedule_id 3)에 걸려 저장되지 않았습니다." · "2건이 정비 창(schedule_id 3, 5)에 걸려 저장되지 않았습니다."
    /// </summary>
    public static string? Summarize(IReadOnlyCollection<EventSuppressedException> suppressed)
    {
        if (suppressed.Count == 0) return null;
        var ids = suppressed.Where(s => s.ScheduleId.HasValue).Select(s => s.ScheduleId!.Value).Distinct().ToList();
        var window = ids.Count > 0 ? $"정비 창(schedule_id {string.Join(", ", ids)})" : "정비 창";
        var head = suppressed.Count == 1 ? "" : $"{suppressed.Count}건이 ";
        return $"{head}{window}에 걸려 저장되지 않았습니다.";
    }
}
