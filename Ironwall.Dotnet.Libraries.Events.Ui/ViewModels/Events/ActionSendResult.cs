namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;

/****************************************************************************
   Purpose      : 조치보고 한 건을 보낸 '진짜' 결과.
                  bool 하나로는 "만들었다" 와 "멱등 가드가 막아서 안 보냈다" 가 구분되지 않아
                  조치 트레이가 후자를 '적용' 으로 셌다(N-07 적대 검토 R1).
   Created By   : GHLee
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>조치보고 전송 한 번의 결말.</summary>
public enum ActionSendOutcome
{
    /// <summary>서버에 조치가 <b>새로 만들어졌다</b>.</summary>
    Created,

    /// <summary>멱등 가드가 막았다 — <b>이 호출은 아무것도 만들지 않았다</b>(다른 경로가 같은 이벤트를 보고 중).</summary>
    GuardSkipped,

    /// <summary>서버가 거절했거나 닿지 못했다.</summary>
    Failed,

    /// <summary>취소됐다 — 보냈는지 여부를 이 자리에서는 알 수 없다.</summary>
    Cancelled,
}

/// <summary>전송 결과 + 사람이 읽는 까닭 + 만들어진 조치 Id.</summary>
/// <param name="Outcome">결말.</param>
/// <param name="Reason">실패 · 스킵의 까닭 한 줄(성공이면 null).</param>
/// <param name="ActionId">만들어진 조치의 서버 Id(0 이면 없음).</param>
public readonly record struct ActionSendResult(ActionSendOutcome Outcome, string? Reason = null, int ActionId = 0)
{
    /// <summary>
    /// 옛 <c>SendAction</c> 의 bool 과 같은 뜻 — 다이얼로그는 "닫아도 되는가" 로 읽는다.
    /// 가드가 막은 것도 <b>다른 경로가 보고 중</b>이라 다이얼로그는 닫아야 하므로 true 다(지금 동작 보존).
    /// </summary>
    public bool CanCloseDialog => Outcome is ActionSendOutcome.Created or ActionSendOutcome.GuardSkipped;

    public static ActionSendResult Created(int actionId) => new(ActionSendOutcome.Created, null, actionId);

    public static ActionSendResult GuardSkipped(string reason) => new(ActionSendOutcome.GuardSkipped, reason);

    public static ActionSendResult Failed(string reason) => new(ActionSendOutcome.Failed, reason);
}
