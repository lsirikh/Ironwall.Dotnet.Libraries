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
        _setup.IsSession = isSession;
        _setup.SessionExpiration = expirationMinutes;
        _tokenGenerator.SetTimerEnable(isSession);     // 세션 타이머 단일 소스
        _tokenGenerator.SetSession(expirationMinutes);
    }
    #endregion

    #region - Attributes -
    private readonly TokenGenerator _tokenGenerator;
    private readonly IAccountSetupModel _setup;
    #endregion
}
