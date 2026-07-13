namespace Ironwall.Dotnet.Libraries.Accounts.Gateways;

/****************************************************************************
   Purpose      : 로그인 결과 (성공=AuthResult / 실패=에러코드+사유)
   Notes        : 기존 null 반환은 실패 사유(UNAUTHORIZED/LOCKED/FORBIDDEN…)를 못 실어
                  로그인 UI가 모든 실패를 generic 메시지로 붕괴시켰음(GOP-00 G1/§2.4.3).
                  AuthOutcome 으로 서버 Error.Code/Message/LockReason 을 VM 까지 전달한다.
****************************************************************************/
public sealed record AuthOutcome(bool Success, AuthResult? Result, string? ErrorCode, string? Message, string? LockReason,
    int? FailedCount = null, int? Threshold = null, int? Remaining = null, bool? Locked = null)
{
    public static AuthOutcome Ok(AuthResult result) => new(true, result, null, null, null);

    /// <summary>인증 실패. v6.3: 서버 잠금정책 잔여(error.details {failed_count,threshold,remaining,locked})를 실어
    /// 로그인 UI가 "N회 중 M회 실패, K회 남음"·잠금 안내를 표시한다(계정 열거 방지 위해 미존재 계정은 details=null=generic).</summary>
    public static AuthOutcome Fail(string code, string? message = null, string? lockReason = null,
        int? failedCount = null, int? threshold = null, int? remaining = null, bool? locked = null)
        => new(false, null, code, message, lockReason, failedCount, threshold, remaining, locked);
}
