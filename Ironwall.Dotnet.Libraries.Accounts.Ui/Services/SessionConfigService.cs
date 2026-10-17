using Ironwall.Dotnet.Framework.Services;          // TokenGenerator
using Ironwall.Dotnet.Libraries.Accounts.Models;   // IAccountSetupModel

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Services;

/****************************************************************************
   Purpose      : 세션 설정 단일 소스 — 세션 토글/만료를 TokenGenerator에 반영
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public sealed class SessionConfigService : ISessionConfigService
{
    #region - Ctors -
    public SessionConfigService(TokenGenerator tokenGenerator, IAccountSetupModel setup)
    {
        _tokenGenerator = tokenGenerator;
        _setup = setup;
    }
    #endregion

    #region - Properties -
    public bool IsSession => _setup.IsSession;
    public int  SessionExpiration => _setup.SessionExpiration;

    // 관리자 초기화 비밀번호 기본값 (P-CHK-3: 하드코딩 "12345678" 외부화).
    // 추후 appsettings 바인딩으로 교체 가능.
    public string AdminResetPassword { get; init; } = "12345678";
    #endregion

    #region - Processes -
    public void ApplySession(bool isSession, int expirationMinutes)
    {
        // 설정값만 보존한다. [레거시 제거] TokenGenerator 로컬 타이머는 더 이상 세션 권위가 아니다 —
        //   GOP 모드 세션 만료는 서버 JWT + SessionLifecycle(ArmExpiryTimer)가 단일 권위로 처리한다.
        //   (레거시 30분 타이머가 24h 세션 중 오발화 → 회색지대 로그아웃 사고, 배선 전면 제거.)
        _setup.IsSession = isSession;
        _setup.SessionExpiration = expirationMinutes;
    }
    #endregion

    #region - Attributes -
    private readonly TokenGenerator _tokenGenerator;
    private readonly IAccountSetupModel _setup;
    #endregion
}
