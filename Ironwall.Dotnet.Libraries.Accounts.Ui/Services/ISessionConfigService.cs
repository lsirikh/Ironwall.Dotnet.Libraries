namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Services;

/****************************************************************************
   Purpose      : 세션 설정 추상화 (TokenGenerator 직접 결합 제거)
   Notes        : AccountSetupPanelViewModel 이 세션 토글/만료 설정을 이 서비스로 위임.
                  AdminResetPassword = 관리자 비밀번호 초기화 기본값 외부화(C-2/OQ-2, 하드코딩 제거).
****************************************************************************/
public interface ISessionConfigService
{
    bool IsSession { get; }
    int  SessionExpiration { get; }                            // 분 단위

    /// <summary>관리자 강제 초기화 기본 비밀번호(EditorDialog 하드코딩 "12345678" 외부화).</summary>
    string AdminResetPassword { get; }

    /// <summary>세션 토글/만료를 적용하고 TokenGenerator에 반영.</summary>
    void ApplySession(bool isSession, int expirationMinutes);
}
