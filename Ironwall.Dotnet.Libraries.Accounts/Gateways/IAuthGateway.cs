using Ironwall.Dotnet.Monitoring.Models.Accounts;

namespace Ironwall.Dotnet.Libraries.Accounts.Gateways;

/****************************************************************************
   Purpose      : 로그인/세션 인증 게이트웨이 (데이터 접근 추상화)
   Notes        : Db 어댑터 = 로컬 인증 + TokenGenerator, Api 어댑터(GOP-00) = 서버 JWT.
                  VM(LoginPanelViewModel)은 이 인터페이스에만 의존 → API 전환 시 재편집 0.
****************************************************************************/
public interface IAuthGateway
{
    /// <summary>아이디/비밀번호로 인증. 성공/실패(에러코드·사유)를 <see cref="AuthOutcome"/>로 반환(GOP-00 G1).</summary>
    Task<AuthOutcome> AuthenticateAsync(string username, string password, CancellationToken ct = default);

    /// <summary>가장 최근 로그인 기록(저장된 아이디 복원용).</summary>
    Task<ILoginModel?> GetLatestLoginAsync(CancellationToken ct = default);

    /// <summary>로그인 성공 로그 기록.</summary>
    Task RecordLoginAsync(string username, bool isUsernameSaved, CancellationToken ct = default);

    /// <summary>로그아웃. Direct=no-op / Api(GOP)=서버 logout best-effort + 토큰 폐기(GOP-00 G2).</summary>
    Task LogoutAsync(CancellationToken ct = default);
}
