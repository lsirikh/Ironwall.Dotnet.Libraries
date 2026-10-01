namespace Ironwall.Dotnet.Libraries.Sso;

/// <summary>
/// SSO 에이전트(명명 파이프 · SALP)와의 경계 — SSO PRD FR-01.
/// <para>조정자(<see cref="SsoSessionCoordinator"/>)는 SDK 를 직접 보지 않고 이 경계만 본다 —
/// 에이전트 없이 헤드리스로 시험하기 위해서다. 실제 구현은 <see cref="SsoAgentGateway"/>.</para>
/// </summary>
public interface ISsoAgentGateway
{
    /// <summary>
    /// 에이전트 파이프가 지금 있는가 — <b>값싼 확인</b>(연결하지 않는다).
    /// <para>SDK 의 파이프 연결은 파이프가 생길 때까지 최대 1.5초 기다린다. 에이전트가 없는 PC(상시 경로, T28)가
    /// 시작마다 그만큼 멈추지 않도록, 시작 로그인은 이걸 먼저 보고 없으면 곧바로 평소 로그인 화면으로 간다.</para>
    /// <para><c>true</c> 가 연결 성공을 보장하지는 않는다 — 실제 판정은 <see cref="SignInAsync"/> 가 한다.</para>
    /// </summary>
    bool IsAgentPresent();

    /// <summary>
    /// 창 없이 새 SSO 앱 토큰을 받는다 — SDK <c>SignInAsync([])</c>.
    /// <para>에이전트 안에서 <c>/handoff/issue</c>(자기 앞) → <c>/handoff/redeem</c> → SSO 서버 live 교환으로 가고,
    /// <b>호출마다 새 <c>jti</c></b> 의 토큰이 나온다(실측) — 그래서 교환 규칙 #8(<c>jti</c> 1회)과 충돌하지 않는다.
    /// 갱신·401 복구에서 들고 있던 토큰을 다시 내지 말고 반드시 이걸 다시 부른다.</para>
    /// </summary>
    Task<SsoAgentResult> SignInAsync(CancellationToken ct = default);

    /// <summary>
    /// 사용자에게 로그인 창을 띄워서라도 토큰을 받는다 — SDK <c>SignInInteractiveAsync()</c>.
    /// 에이전트에 세션이 없을 때(<see cref="SsoAgentStatus.NoActiveSession"/>) 로그인 화면에서 부른다.
    /// </summary>
    Task<SsoAgentResult> SignInInteractiveAsync(CancellationToken ct = default);
}

/// <summary>에이전트가 돌려준 결과. 실패도 <b>예외가 아니라 값</b>이다(SDK 3.10.6).</summary>
public sealed record SsoAgentResult(SsoAgentStatus Status, string? AccessToken, string Detail)
{
    public bool IsOk => Status == SsoAgentStatus.Ok && !string.IsNullOrEmpty(AccessToken);

    public static SsoAgentResult Ok(string token) => new(SsoAgentStatus.Ok, token, string.Empty);
    public static SsoAgentResult Fail(SsoAgentStatus status, string detail) => new(status, null, detail);
}

/// <summary>
/// GIS 가 갈라야 하는 에이전트 상태 — SDK <c>SsoOutcome</c> 를 <b>GIS 의 대응</b> 기준으로 접었다.
/// </summary>
public enum SsoAgentStatus
{
    /// <summary>토큰 수령.</summary>
    Ok,

    /// <summary>
    /// 에이전트를 쓸 수 없다 — <c>AgentNotRunning</c> · <c>VersionMismatch</c> · <c>SignInUnsupported</c>.
    /// ➡ <b>기존 아이디/비밀번호 로그인으로 폴백</b>(FR-01 · S2). 에이전트는 사용자 트레이 앱이라
    /// 이 경로는 예외가 아니라 상시 경로다(T28).
    /// </summary>
    Unavailable,

    /// <summary>
    /// 에이전트에 로그인 세션이 없다 — <c>NoActiveSession</c>.
    /// ➡ "에이전트에서 로그인하세요" 또는 <see cref="ISsoAgentGateway.SignInInteractiveAsync"/>. 재시도 루프 금지.
    /// </summary>
    NoActiveSession,

    /// <summary>
    /// 이 앱이 등록돼 있지 않다 — <c>UnknownClient</c>(와이어 <c>invalid_client</c>).
    /// ➡ "관리자에게 앱 등록 요청". 3.10.7 부터 에이전트가 로그인 전이면 <c>NoActiveSession</c> 으로 갈라 주므로
    /// 여기 오는 것은 로그인된 에이전트가 정말 모르는 경우다(T29).
    /// </summary>
    NotRegistered,

    /// <summary>
    /// 사용자 허용을 기다린다 — <c>ConsentRequired</c>. 서명 없는 빌드의 첫 실행 등(T26 · T30).
    /// ➡ 허용 창 안내 후 재시도.
    /// </summary>
    ConsentRequired,

    /// <summary>일시 장애 — <c>UpstreamUnavailable</c>(SSO 서버·GOP 에 닿지 못함). ➡ 잠시 뒤 재시도.</summary>
    UpstreamUnavailable,

    /// <summary>그 밖 — <c>SignInDenied</c> · <c>SignInTimedOut</c> · <c>InvalidTicket</c> · <c>Unknown</c> 등.</summary>
    Failed,
}
