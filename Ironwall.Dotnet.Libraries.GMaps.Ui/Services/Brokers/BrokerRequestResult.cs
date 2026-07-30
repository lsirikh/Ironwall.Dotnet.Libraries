namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers;

/// <summary>
/// REQ 실패 사유 분류.
/// 전송계층(NatsService.RequestAsync)이 타임아웃/무구독자를 null 하나로 반환하므로
/// 클라이언트 단에서는 NoResponse로 통합 분류한다(세부는 전송계층 로그로 구분).
/// </summary>
public enum EnumBrokerFailure
{
    /// <summary>실패 아님 (Success=true)</summary>
    None = 0,
    /// <summary>클라이언트 사전 검증 실패 — 발행 자체를 하지 않음</summary>
    Invalid = 1,
    /// <summary>응답 없음 — 타임아웃 또는 대상 서비스 무구독(no-responders)</summary>
    NoResponse = 2,
    /// <summary>서버 거부 — RSP success=false (Message에 서버 사유)</summary>
    Rejected = 3,
    /// <summary>RSP 수신했으나 봉투 해석 실패</summary>
    ParseError = 4,
    /// <summary>호출 측 취소(CancellationToken)</summary>
    Cancelled = 5,
}

/// <summary>
/// GIS→매니저 제어 REQ의 표준 결과 (GIS.md v1.5.2 §6.4/§6.9).
/// 서비스는 이 결과를 반환만 하고, 팝업/상태 표시는 호출부(ViewModel) 책임.
/// </summary>
public class BrokerRequestResult
{
    /// <summary>RSP success=true 수신 여부</summary>
    public bool Success { get; init; }

    /// <summary>실패 사유 분류 (성공 시 None)</summary>
    public EnumBrokerFailure Reason { get; init; }

    /// <summary>서버 RSP message 원문 (성공/거부 공통, 없으면 빈 문자열)</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>RSP의 req_id (수신 못 했으면 null)</summary>
    public string? ReqId { get; init; }

    /// <summary>사용자 안내용 한글 문구 — §6.9 서버 message 패턴 매핑 결과</summary>
    public string UserMessage { get; init; } = string.Empty;

    public static BrokerRequestResult Ok(string message, string? reqId) => new()
    {
        Success = true,
        Reason = EnumBrokerFailure.None,
        Message = message,
        ReqId = reqId,
        UserMessage = string.Empty,
    };

    public static BrokerRequestResult Fail(EnumBrokerFailure reason, string userMessage, string serverMessage = "", string? reqId = null) => new()
    {
        Success = false,
        Reason = reason,
        Message = serverMessage,
        ReqId = reqId,
        UserMessage = userMessage,
    };
}
