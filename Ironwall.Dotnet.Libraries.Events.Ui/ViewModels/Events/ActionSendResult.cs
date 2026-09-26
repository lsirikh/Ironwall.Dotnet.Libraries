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

    /// <summary>
    /// 서버가 받지 않았다 — 까닭을 <b>고정 한국어 문장</b>으로 낸다. 서버 원문은 화면에 싣지 않고 호출부가 로그로 남긴다
    /// (완성도 수정 패스 문구 규칙: "무엇이 안 됐고 어떻게 하면 되는지"). 상태 코드로 사람이 할 일을 가른다 —
    /// 예전엔 원문을 그대로 붙여 403 · 422 를 가리지 않으려 했는데, 그 원문이 영어 · 필드명이었다.
    /// </summary>
    public static ActionSendResult ServerRefused(int statusCode) => Failed(RefusalText(statusCode));

    /// <summary>상태 코드 → 사람이 할 일.</summary>
    public static string RefusalText(int statusCode) => statusCode switch
    {
        401 => "로그인이 만료되었습니다. 다시 로그인한 뒤 보내세요.",
        403 => "조치보고 권한(이벤트 편집)이 없습니다.",
        404 => "원본 이벤트가 서버에 없습니다. 목록을 [새로 불러오기] 하세요.",
        409 => "같은 조치보고가 이미 처리 중입니다. 잠시 뒤 [새로 불러오기] 하세요.",
        400 or 422 => "서버가 조치 내용을 받지 않았습니다. 내용을 확인하세요.",
        _ =>"서버가 조치보고를 받지 않았습니다. 잠시 뒤 다시 보내세요.",
    };
}
