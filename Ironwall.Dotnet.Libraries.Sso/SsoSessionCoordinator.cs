using Ironwall.Dotnet.Libraries.Accounts.Api.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Api.Handlers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

namespace Ironwall.Dotnet.Libraries.Sso;

/// <summary>
/// SSO 세션 조정자 — 에이전트 토큰 → GOP 교환 → 토큰 저장소를 잇는다(SSO PRD FR-03 · FR-04 · FR-06).
///
/// <para><b>두 진입점</b>:</para>
/// <list type="bullet">
/// <item><see cref="TrySignInAsync"/> — 앱 시작 때. 성공이면 <b>로그인 화면을 건너뛴다</b>. 실패는 값으로 돌려줘
///       호출부(로그인 화면)가 안내문을 고르게 한다. 에이전트가 없으면 기존 아이디/비밀번호 로그인으로 폴백.</item>
/// <item><see cref="ReauthenticateAsync"/> — 401 복구. <see cref="BearerAuthHandler.SsoReauthenticator"/> 로 꽂힌다
///       (<see cref="Enable"/>). 교환은 refresh 를 주지 않으므로 이게 없으면 401 한 번에 강제 로그아웃이다.</item>
/// </list>
///
/// <para><b>매번 새 토큰</b>: 두 경로 모두 들고 있던 SSO 토큰을 다시 내지 않고 <see cref="ISsoAgentGateway.SignInAsync"/>
/// 로 새로 받는다 — 교환은 같은 <c>jti</c> 를 두 번 받지 않는다(규칙 #8, <c>TOKEN_REPLAYED</c>).</para>
///
/// <para><b>스레드</b>: <see cref="ReauthenticateAsync"/> 는 <see cref="BearerAuthHandler"/> 의 전역 single-flight 락 안에서
/// 불리므로 동시에 두 번 돌지 않는다. <see cref="TrySignInAsync"/> 는 시작 때 한 번 불리는 것을 전제로 한다.</para>
/// </summary>
public sealed class SsoSessionCoordinator
{
    /// <summary>
    /// 교환이 <c>retryable=true</c> 로 거절했을 때 새 토큰으로 다시 해 보는 횟수.
    /// <b>1회로 묶는다</b> — 무한 재교환(「GOP 교환 허용」 꺼진 앱 등)을 구조적으로 막는다.
    /// </summary>
    private const int MAX_EXCHANGE_RETRY = 1;

    private readonly ISsoAgentGateway _agent;
    private readonly IAccountApiService _api;
    private readonly ITokenStorageService _store;
    private readonly ILogService? _log;
    private readonly ISsoLoginCompleter? _completer;

    /// <summary>훅에 꽂는 델리게이트 — 한 번 만들어 두고 같은 인스턴스로 꽂고 뗀다(누가 꽂았는지 가리기 위해).</summary>
    private readonly Func<CancellationToken, Task<SsoReauthOutcome>> _hook;

    /// <param name="completer">
    /// 로그인 마무리(권한 적용 · 로그인 게이팅 알림). 앱에서는 <b>반드시</b> 준다 — 없으면 토큰만 넣고 끝나
    /// 권한·GIS 초기화가 돌지 않는다. <c>null</c> 은 시험·진단용이다.
    /// </param>
    public SsoSessionCoordinator(ISsoAgentGateway agent, IAccountApiService api, ITokenStorageService store,
                                 ILogService? log = null, ISsoLoginCompleter? completer = null)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _log = log;
        _completer = completer;
        _hook = ReauthenticateAsync;
    }

    /// <summary>
    /// <see cref="BearerAuthHandler.SsoReauthenticator"/> 에 이 조정자를 꽂는다 — 이후 모든 도메인의 401 이 재교환으로 간다.
    /// SSO 로그인에 성공한 뒤에만 부른다(아이디/비밀번호 로그인 세션에는 refresh 가 있으니 레거시 경로가 맞다).
    /// </summary>
    public void Enable() => BearerAuthHandler.SsoReauthenticator = _hook;

    /// <summary>훅을 떼어 레거시(refresh) 모드로 돌린다 — 로그아웃·아이디/비밀번호 재로그인 때.</summary>
    public void Disable()
    {
        // 다른 조정자가 꽂은 훅까지 지우지 않는다.
        if (ReferenceEquals(BearerAuthHandler.SsoReauthenticator, _hook))
            BearerAuthHandler.SsoReauthenticator = null;
    }

    /// <summary>훅이 지금 이 조정자를 가리키는가.</summary>
    public bool IsEnabled => ReferenceEquals(BearerAuthHandler.SsoReauthenticator, _hook);

    // ══ 시작 로그인 ═══════════════════════════════════════════════════

    /// <summary>
    /// 앱 시작 때 SSO 로 로그인을 시도한다. 성공이면 토큰 저장소가 채워지고 <see cref="Enable"/> 까지 끝난 상태다.
    /// </summary>
    public async Task<SsoSignInResult> TrySignInAsync(CancellationToken ct = default)
    {
        // 에이전트 없는 PC 는 상시 경로다 — 파이프 연결 대기(최대 1.5초) 없이 곧바로 평소 로그인 화면.
        if (!_agent.IsAgentPresent())
            return new SsoSignInResult { Status = SsoSignInStatus.AgentUnavailable, Message = "SSO 에이전트가 실행 중이 아닙니다" };

        var agent = await _agent.SignInAsync(ct).ConfigureAwait(false);
        if (!agent.IsOk)
        {
            _log?.Info($"[SSO] 에이전트 로그인 불가 — {agent.Status}: {agent.Detail}");
            return SsoSignInResult.FromAgent(agent);
        }

        var exchange = await ExchangeWithRetryAsync(agent.AccessToken!, ct).ConfigureAwait(false);
        if (!exchange.Success)
        {
            _log?.Warning($"[SSO] 교환 거절 — status={exchange.StatusCode} code={exchange.ErrorCode} reason={exchange.Reason} retryable={exchange.Retryable}");
            return SsoSignInResult.FromExchange(exchange);
        }

        var data = exchange.Data!;

        // 마무리는 비밀번호 로그인과 같은 코드(토큰 → 권한 → 가드 재무장 → 로그인 게이팅 알림)를 탄다.
        // 교환은 refresh 를 주지 않는다 — null 로 들어가 BearerAuthHandler 가 SSO 경로(재교환)를 타게 된다.
        AuthOutcome? auth = null;
        if (_completer is not null)
        {
            auth = _completer.CompleteSsoLogin(data);
            if (!auth.Success)
            {
                _log?.Warning($"[SSO] 교환은 됐으나 로그인 마무리 실패 — {auth.ErrorCode}: {auth.Message}");
                return new SsoSignInResult { Status = SsoSignInStatus.Failed, Message = auth.Message ?? auth.ErrorCode ?? "로그인 마무리 실패" };
            }
        }
        else
        {
            _store.SetTokens(data.AccessToken, refreshToken: null, sessionId: data.SessionId);
        }

        Enable();
        _log?.Info($"[SSO] 로그인 완료 — user={data.User?.LoginId} session={data.SessionId}");
        return SsoSignInResult.SignedIn(data, auth);
    }

    // ══ 401 복구 ═════════════════════════════════════════════════════

    /// <summary>
    /// 401 복구 — 새 SSO 토큰을 받아 재교환하고 저장소를 갱신한다(FR-06).
    /// <para>저장소 세대(<c>Generation</c>)를 시작 때 잡아 두고 <c>SetTokensIfGeneration</c> 으로 넣는다 —
    /// 재교환 도중 강제 로그아웃(<c>Clear</c>)이 끼어들면 <b>폐기된 세션을 되살리지 않는다</b>(FR-FL-05 와 같은 규칙).</para>
    /// </summary>
    public async Task<SsoReauthOutcome> ReauthenticateAsync(CancellationToken ct)
    {
        // ★ 재교환은 '살아 있는 세션의 갱신' 이지 '새 로그인' 이 아니다.
        //   로그아웃(Clear) 뒤에 남은 백그라운드 요청이 401 을 받으면 여기로 온다 — 그때 에이전트 세션이
        //   살아 있다고 재교환하면 **사용자가 로그아웃했는데 몰래 다시 로그인**된다. 저장소가 비었으면 거절한다.
        if (!_store.IsAuthenticated)
        {
            _log?.Info("[SSO] 재교환 거절 — 저장소에 세션이 없다(로그아웃 뒤). 새 로그인은 로그인 화면에서만 한다");
            return SsoReauthOutcome.Terminal;
        }

        var gen = _store.Generation;

        var agent = await _agent.SignInAsync(ct).ConfigureAwait(false);
        if (!agent.IsOk)
        {
            var outcome = agent.Status switch
            {
                // 에이전트 세션이 끝났다(사용자 로그아웃·에이전트 재시작) — 재교환해도 소용없다. 로그인 화면으로.
                SsoAgentStatus.NoActiveSession => SsoReauthOutcome.Terminal,
                SsoAgentStatus.NotRegistered => SsoReauthOutcome.Terminal,
                // 에이전트가 잠깐 내려갔거나(업데이트 중 등) 서버에 못 닿았다 — 세션을 죽이지 않는다.
                _ => SsoReauthOutcome.Transient,
            };
            _log?.Warning($"[SSO] 재교환 — 에이전트 {agent.Status} → {outcome} ({agent.Detail})");
            return outcome;
        }

        var exchange = await ExchangeWithRetryAsync(agent.AccessToken!, ct).ConfigureAwait(false);
        if (exchange.Success)
        {
            var data = exchange.Data!;
            if (_store.SetTokensIfGeneration(gen, data.AccessToken, refreshToken: null, sessionId: data.SessionId))
            {
                _log?.Info($"[SSO] 재교환 완료 — session={data.SessionId}");
                return SsoReauthOutcome.Renewed;
            }

            _log?.Warning("[SSO] 재교환 성공했으나 그 사이 세션이 폐기됨(generation 변경) — 되살리지 않는다");
            return SsoReauthOutcome.Terminal;
        }

        var result = exchange.Retryable ? SsoReauthOutcome.Transient : SsoReauthOutcome.Terminal;
        _log?.Warning($"[SSO] 재교환 거절 — code={exchange.ErrorCode} reason={exchange.Reason} retryable={exchange.Retryable} → {result}");
        return result;
    }

    // ══ 공통 ═════════════════════════════════════════════════════════

    /// <summary>
    /// 교환하고, <c>retryable=true</c> 면 <b>새 토큰으로</b> 최대 <see cref="MAX_EXCHANGE_RETRY"/> 회 더 해 본다.
    /// 429(<c>Retry-After</c>)는 곧바로 다시 하지 않는다 — 기다려야 하는 실패라 그대로 돌려준다.
    /// </summary>
    private async Task<SsoExchangeResult> ExchangeWithRetryAsync(string firstToken, CancellationToken ct)
    {
        var result = await _api.SsoExchangeAsync(firstToken, ct).ConfigureAwait(false);

        for (var i = 0; i < MAX_EXCHANGE_RETRY && !result.Success && result.Retryable && result.StatusCode != 429; i++)
        {
            // 들고 있던 토큰을 다시 내면 TOKEN_REPLAYED — 반드시 새로 받는다.
            var fresh = await _agent.SignInAsync(ct).ConfigureAwait(false);
            if (!fresh.IsOk) break;
            result = await _api.SsoExchangeAsync(fresh.AccessToken!, ct).ConfigureAwait(false);
        }

        return result;
    }
}

/// <summary>시작 로그인 결과 — 로그인 화면이 이것으로 안내문을 고른다.</summary>
public sealed class SsoSignInResult
{
    public SsoSignInStatus Status { get; init; }

    /// <summary>사람이 읽을 사유(진단·안내).</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>성공 시 교환 응답(사용자 스냅샷 포함).</summary>
    public SsoExchangeResponseDataDto? Data { get; init; }

    /// <summary>
    /// 성공 시 로그인 마무리 결과 — 비밀번호 로그인의 <c>AuthenticateAsync</c> 와 같은 모양이라
    /// 로그인 화면이 같은 코드로 계정 표시·토큰을 채운다. 조정자에 마무리 경계가 없으면 <c>null</c>.
    /// </summary>
    public AuthOutcome? Auth { get; init; }

    /// <summary>
    /// 로그인 화면에 보여줄 안내 한 줄. 조용한 폴백·성공이면 빈 문자열(안내 불필요).
    /// 사람이 다음에 무엇을 해야 하는지를 말한다 — 원인 코드가 아니라 행동.
    /// </summary>
    public string Guidance => Status switch
    {
        SsoSignInStatus.NeedsAgentLogin => "SSO 에이전트에서 로그인하면 다음부터 이 화면을 건너뜁니다.",
        SsoSignInStatus.NeedsAdminRegistration => "이 프로그램이 SSO 에 등록되지 않았습니다. 관리자에게 문의하세요.",
        SsoSignInStatus.ConsentPending => "SSO 에이전트 창에서 이 프로그램의 연결을 허용해 주세요.",
        SsoSignInStatus.TemporarilyUnavailable => "SSO 서버에 잠시 연결할 수 없습니다. 아이디로 로그인하세요.",
        SsoSignInStatus.ExchangeRejected => $"SSO 로그인이 거절되었습니다 — {Message}",
        SsoSignInStatus.Failed => "SSO 로그인에 실패했습니다. 아이디로 로그인하세요.",
        _ => string.Empty,
    };

    /// <summary>로그인 화면을 건너뛰어도 되는가.</summary>
    public bool CanSkipLoginScreen => Status == SsoSignInStatus.SignedIn;

    /// <summary>
    /// 아이디/비밀번호 로그인 화면을 <b>평소처럼</b> 보여주면 되는가 — 에이전트가 없는 PC 등.
    /// <c>false</c> 인 실패는 안내가 필요하다(에이전트 로그인 · 관리자 등록 · 허용 창).
    /// </summary>
    public bool IsSilentFallback => Status is SsoSignInStatus.AgentUnavailable or SsoSignInStatus.ServerNotSupported;

    public static SsoSignInResult SignedIn(SsoExchangeResponseDataDto data, AuthOutcome? auth = null)
        => new() { Status = SsoSignInStatus.SignedIn, Data = data, Auth = auth };

    internal static SsoSignInResult FromAgent(SsoAgentResult a) => new()
    {
        Status = a.Status switch
        {
            SsoAgentStatus.Unavailable => SsoSignInStatus.AgentUnavailable,
            SsoAgentStatus.NoActiveSession => SsoSignInStatus.NeedsAgentLogin,
            SsoAgentStatus.NotRegistered => SsoSignInStatus.NeedsAdminRegistration,
            SsoAgentStatus.ConsentRequired => SsoSignInStatus.ConsentPending,
            SsoAgentStatus.UpstreamUnavailable => SsoSignInStatus.TemporarilyUnavailable,
            _ => SsoSignInStatus.Failed,
        },
        Message = a.Detail,
    };

    internal static SsoSignInResult FromExchange(SsoExchangeResult e) => new()
    {
        Status = e.StatusCode switch
        {
            // 서버에 교환 경로가 아직 없다(구판 · 교환 미배포) — 오류가 아니라 '이 서버는 SSO 를 모른다'.
            // 평소 로그인 화면으로 조용히 간다. 서버가 배포하는 순간 설정 없이 SSO 가 켜진다.
            404 => SsoSignInStatus.ServerNotSupported,
            429 => SsoSignInStatus.TemporarilyUnavailable,
            0 or >= 500 when e.Retryable => SsoSignInStatus.TemporarilyUnavailable,
            _ => SsoSignInStatus.ExchangeRejected,
        },
        Message = string.IsNullOrWhiteSpace(e.Message) ? $"{e.ErrorCode} ({e.Reason})" : e.Message!,
    };
}

/// <summary>시작 로그인 결과 분류 — 로그인 화면의 안내문과 1:1.</summary>
public enum SsoSignInStatus
{
    /// <summary>성공 — 로그인 화면을 건너뛴다.</summary>
    SignedIn,

    /// <summary>에이전트가 없다(미설치·미실행·판본 불일치) — 평소 아이디/비밀번호 화면. 안내 불필요.</summary>
    AgentUnavailable,

    /// <summary>
    /// GOP 서버에 교환 경로가 없다(<c>404</c> — 교환 미배포 판) — 평소 아이디/비밀번호 화면. 안내 불필요.
    /// 서버가 교환을 배포하면 설정 없이 SSO 가 켜진다.
    /// </summary>
    ServerNotSupported,

    /// <summary>에이전트에 로그인 세션이 없다 — "에이전트에서 로그인하세요" + 아이디/비밀번호 폴백.</summary>
    NeedsAgentLogin,

    /// <summary>이 앱이 SSO 에 등록돼 있지 않다 — "관리자에게 앱 등록 요청" + 아이디/비밀번호 폴백.</summary>
    NeedsAdminRegistration,

    /// <summary>에이전트가 사용자 허용을 기다린다 — "에이전트 창에서 허용해 주세요".</summary>
    ConsentPending,

    /// <summary>SSO 서버·GOP 일시 장애 또는 요청 과다 — "잠시 뒤 다시" + 아이디/비밀번호 폴백.</summary>
    TemporarilyUnavailable,

    /// <summary>GOP 가 교환을 거절했다(계정 비활성·잠금, 허용 목록 밖 앱 등) — 사유를 보여주고 아이디/비밀번호 폴백.</summary>
    ExchangeRejected,

    /// <summary>그 밖.</summary>
    Failed,
}
