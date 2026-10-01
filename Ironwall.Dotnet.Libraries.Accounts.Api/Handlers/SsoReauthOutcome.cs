namespace Ironwall.Dotnet.Libraries.Accounts.Api.Handlers;

/// <summary>
/// SSO 모드 재교환 결과 — <see cref="BearerAuthHandler.SsoReauthenticator"/> 가 돌려준다(SSO PRD FR-06).
/// </summary>
public enum SsoReauthOutcome
{
    /// <summary>새 토큰을 저장소에 넣었다 — 핸들러가 원요청을 재시도한다.</summary>
    Renewed,

    /// <summary>
    /// 일시 실패 — 세션을 유지하고 다음 요청·사용자 재시도에 맡긴다.
    /// 서버 <c>retryable=true</c>(만료·<c>TOKEN_REPLAYED</c>·<c>TOO_MANY_REQUESTS</c>·JWKS 장애) 중
    /// 이번 재교환이 끝내 실패한 경우, 그리고 네트워크 장애.
    /// </summary>
    Transient,

    /// <summary>
    /// 종단 실패 — 저장소를 비우고 세션 만료를 알린다(로그인 화면).
    /// 서버 <c>retryable=false</c>(계정 비활성·잠금, 허용 목록 밖 앱, 서명·<c>aud</c> 불량),
    /// 그리고 SSO 에이전트 세션이 끝난 경우(<c>NoActiveSession</c>).
    /// </summary>
    Terminal,
}
