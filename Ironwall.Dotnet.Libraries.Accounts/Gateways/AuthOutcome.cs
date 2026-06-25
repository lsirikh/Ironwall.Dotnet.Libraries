namespace Ironwall.Dotnet.Libraries.Accounts.Gateways;

/****************************************************************************
   Purpose      : 로그인 결과 (성공=AuthResult / 실패=에러코드+사유)
   Notes        : 기존 null 반환은 실패 사유(UNAUTHORIZED/LOCKED/FORBIDDEN…)를 못 실어
                  로그인 UI가 모든 실패를 generic 메시지로 붕괴시켰음(GOP-00 G1/§2.4.3).
                  AuthOutcome 으로 서버 Error.Code/Message/LockReason 을 VM 까지 전달한다.
****************************************************************************/
public sealed record AuthOutcome(bool Success, AuthResult? Result, string? ErrorCode, string? Message, string? LockReason)
{
    public static AuthOutcome Ok(AuthResult result) => new(true, result, null, null, null);

    public static AuthOutcome Fail(string code, string? message = null, string? lockReason = null)
        => new(false, null, code, message, lockReason);
}
