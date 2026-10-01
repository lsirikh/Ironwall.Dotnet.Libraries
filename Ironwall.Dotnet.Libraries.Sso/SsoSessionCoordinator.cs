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
    private readonly ISessionLifecycle? _lifecycle;

    /// <summary>훅에 꽂는 델리게이트 — 한 번 만들어 두고 같은 인스턴스로 꽂고 뗀다(누가 꽂았는지 가리기 위해).</summary>
    private readonly Func<CancellationToken, Task<SsoReauthOutcome>> _hook;

    /// <summary>에이전트 사건 구독(<see cref="ISsoAgentGateway.Watch"/>). SSO 로그인 동안만 열려 있다.</summary>
    private IAsyncDisposable? _watch;
    private readonly object _watchGate = new();

    /// <summary>사건으로 인한 로그아웃 진행 중(1) — 같은 끝을 알리는 사건이 겹쳐 와도 한 번만 처리한다.</summary>
    private int _signingOut;

    /// <summary>사건으로 로그아웃됐을 때 다음 로그인 화면에 보여 줄 안내 — <see cref="TakeSignOutNotice"/> 가 한 번 꺼낸다.</summary>
    private string? _signOutNotice;

    /// <param name="completer">
    /// 로그인 마무리(권한 적용 · 로그인 게이팅 알림). 앱에서는 <b>반드시</b> 준다 — 없으면 토큰만 넣고 끝나
    /// 권한·GIS 초기화가 돌지 않는다. <c>null</c> 은 시험·진단용이다.
    /// </param>
    /// <param name="lifecycle">
    /// 강제 로그아웃 단일 진입점 — 에이전트 사건(이 앱만 로그아웃 · 세션 끝)이 오면 이걸로 로그인 화면에 보낸다.
    /// <c>null</c> 이면 토큰만 지운다(시험·진단용).
    /// </param>
    public SsoSessionCoordinator(ISsoAgentGateway agent, IAccountApiService api, ITokenStorageService store,
                                 ILogService? log = null, ISsoLoginCompleter? completer = null,
                                 ISessionLifecycle? lifecycle = null)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _log = log;
        _completer = completer;
        _lifecycle = lifecycle;
        _hook = ReauthenticateAsync;
    }

    /// <summary>
    /// <see cref="BearerAuthHandler.SsoReauthenticator"/> 에 이 조정자를 꽂는다 — 이후 모든 도메인의 401 이 재교환으로 간다.
    /// SSO 로그인에 성공한 뒤에만 부른다(아이디/비밀번호 로그인 세션에는 refresh 가 있으니 레거시 경로가 맞다).
    /// </summary>
    public void Enable() => BearerAuthHandler.SsoReauthenticator = _hook;

    /// <summary>훅을 떼어 레거시(refresh) 모드로 돌린다 — 로그아웃·아이디/비밀번호 재로그인 때. 에이전트 사건 구독도 멈춘다.</summary>
    public void Disable()
    {
        // 다른 조정자가 꽂은 훅까지 지우지 않는다.
        if (ReferenceEquals(BearerAuthHandler.SsoReauthenticator, _hook))
            BearerAuthHandler.SsoReauthenticator = null;
        StopWatch();
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
        return await CompleteSignInAsync(agent, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 로그인 화면의 <b>"SSO 로 로그인"</b> 단추 — SDK <c>SignInInteractiveAsync</c>.
    /// <para>에이전트에 세션이 없거나 이 앱이 "이 앱만 로그아웃" 으로 막혀 있으면 <b>에이전트가 사람에게 묻는다</b>(로그인 창 · 다시 허락).
    /// 사람이 마칠 때까지 기다리므로 오래 걸릴 수 있다 — 호출부는 <paramref name="ct"/> 로 취소할 수 있게 한다.
    /// 시작 때 자동으로 부르지 않는다(켜질 때마다 창이 뜨면 사람은 왜 떴는지 모른다 — SDK 규칙).</para>
    /// </summary>
    public async Task<SsoSignInResult> TrySignInInteractiveAsync(CancellationToken ct = default)
    {
        var agent = await _agent.SignInInteractiveAsync(ct).ConfigureAwait(false);
        return await CompleteSignInAsync(agent, ct).ConfigureAwait(false);
    }

    /// <summary>에이전트 결과 → 교환 → 로그인 마무리 → 재교환 훅 · 사건 구독. 시작 로그인과 단추가 함께 쓴다.</summary>
    private async Task<SsoSignInResult> CompleteSignInAsync(SsoAgentResult agent, CancellationToken ct)
    {
        if (!agent.IsOk)
        {
            _log?.Info($"[SSO] 에이전트 로그인 불가 — {agent.Status}: {agent.Detail}");
            return SsoSignInResult.FromAgent(agent);
        }

        // 기다리는 사이 사람이 비밀번호로 로그인했다 — 그 세션을 덮지 않는다(단추는 몇 분씩 기다릴 수 있다).
        if (_store.IsAuthenticated)
        {
            _log?.Info("[SSO] 에이전트 토큰을 받았으나 이미 다른 경로로 로그인돼 있다 — 교환하지 않는다");
            return new SsoSignInResult { Status = SsoSignInStatus.Failed, Message = "이미 로그인돼 있습니다" };
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
        Interlocked.Exchange(ref _signingOut, 0);
        StartWatch();
        _log?.Info($"[SSO] 로그인 완료 — user={data.User?.LoginId} session={data.SessionId}");
        return SsoSignInResult.SignedIn(data, auth);
    }

    // ══ 에이전트 사건 (Watch) ════════════════════════════════════════

    /// <summary>
    /// 사건이 오면 할 일 — 순수 판정, 시험 대상.
    /// <para><c>session-ended</c>(모든 앱) · <c>signed-out</c>(이 앱만) 은 모두 <b>이 GIS 의 SSO 로그인이 끝났다</b>는 뜻이다.
    /// 모르는 사건은 무시한다 — 새 판의 에이전트가 사건을 늘려도 GIS 가 엉뚱하게 로그아웃하지 않게.</para>
    /// </summary>
    internal static SsoEventAction Decide(SsoAgentEvent e)
    {
        var blocked = string.Equals(e.Reason, "blocked", StringComparison.OrdinalIgnoreCase);
        if (string.Equals(e.Event, "signed-out", StringComparison.OrdinalIgnoreCase))
            return blocked ? SsoEventAction.AppBlocked : SsoEventAction.AppSignedOut;
        if (string.Equals(e.Event, "session-ended", StringComparison.OrdinalIgnoreCase))
        {
            if (blocked) return SsoEventAction.PcBlocked;
            return string.Equals(e.Reason, "revoked", StringComparison.OrdinalIgnoreCase)
                ? SsoEventAction.SessionRevoked
                : SsoEventAction.SessionEnded;
        }
        return SsoEventAction.Ignore;
    }

    /// <summary>사건으로 로그아웃된 뒤 로그인 화면에 보여 줄 안내(사람이 읽는 다음 행동).</summary>
    internal static string NoticeFor(SsoEventAction a) => a switch
    {
        SsoEventAction.AppSignedOut => "SSO 에이전트에서 이 프로그램을 로그아웃했습니다. 다시 쓰려면 [SSO 로 로그인] 을 누르세요.",
        SsoEventAction.AppBlocked => "관리자가 이 프로그램의 SSO 로그인을 막았습니다. 관리자에게 문의하세요.",
        SsoEventAction.SessionEnded => "SSO 에이전트에서 로그아웃되었습니다.",
        SsoEventAction.SessionRevoked => "SSO 세션이 서버에서 종료되었습니다. 다시 로그인하세요.",
        SsoEventAction.PcBlocked => "관리자가 이 PC 의 SSO 로그인을 막았습니다. 관리자에게 문의하세요.",
        _ => string.Empty,
    };

    /// <summary>사건으로 로그아웃됐다면 그 안내를 한 번 꺼낸다(로그인 화면이 열릴 때). 없으면 <c>null</c>.</summary>
    public string? TakeSignOutNotice() => Interlocked.Exchange(ref _signOutNotice, null);

    /// <summary>에이전트 사건 구독이 열려 있는가(시험 · 진단).</summary>
    public bool IsWatching { get { lock (_watchGate) return _watch is not null; } }

    private void StartWatch()
    {
        lock (_watchGate)
        {
            if (_watch is not null) return;
            try
            {
                _watch = _agent.Watch(OnAgentEvent, ex => _log?.Info($"[SSO] 에이전트 사건 구독 끊김(SDK 가 다시 붙는다): {ex.GetType().Name} {ex.Message}"));
            }
            catch (Exception ex)
            {
                // 구독을 못 열어도 로그인은 유지한다 — 만료 전 재교환 · 401 이 뒤늦게라도 끝을 알려 준다.
                _log?.Warning($"[SSO] 에이전트 사건 구독을 열지 못했다: {ex.GetType().Name} {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 구독을 멈춘다. <b>기다리지 않는다</b> — SDK 의 DisposeAsync 는 사건 루프가 끝날 때까지 기다리는데,
    /// 사건 콜백 → 강제 로그아웃 → 로그인 화면 → <see cref="Disable"/> 로 여기에 다시 오면 그 루프 안이라 기다리면 멈춘다.
    /// </summary>
    private void StopWatch()
    {
        IAsyncDisposable? w;
        lock (_watchGate) { w = _watch; _watch = null; }
        if (w is null) return;
        _ = DisposeQuietlyAsync(w);
    }

    private async Task DisposeQuietlyAsync(IAsyncDisposable w)
    {
        try { await w.DisposeAsync().ConfigureAwait(false); }
        catch (Exception ex) { _log?.Info($"[SSO] 사건 구독 정리 중 예외(무시): {ex.GetType().Name} {ex.Message}"); }
    }

    /// <summary>SDK 사건 콜백 — UI 스레드가 아니다. 예외를 밖으로 내지 않는다.</summary>
    private void OnAgentEvent(SsoAgentEvent e)
    {
        try
        {
            var action = Decide(e);
            _log?.Info($"[SSO] 에이전트 사건 — event={e.Event} reason={e.Reason} at={e.At:o} → {action}");
            if (action == SsoEventAction.Ignore) return;
            _ = SignOutFromAgentAsync(action);
        }
        catch (Exception ex)
        {
            _log?.Warning($"[SSO] 에이전트 사건 처리 예외: {ex.GetType().Name} {ex.Message}");
        }
    }

    /// <summary>
    /// 에이전트가 이 GIS 의 SSO 로그인이 끝났다고 알렸다 — <b>GIS 의 GOP 세션은 GIS 가 스스로 끝낸다</b>.
    /// <para>"이 앱만 로그아웃" 은 back-channel 을 보내지 않으므로(SSO 회신 Q1) 서버 세션이 살아 있다 — 그대로 두면
    /// 토큰이 수명 끝까지 유효하다. 그래서 GOP <c>POST /api/auth/logout</c> 을 먼저 보내고(자기 세션 하나만 끝난다),
    /// 강제 로그아웃 단일 진입점으로 화면을 로그인으로 돌린다. 앱을 닫지는 않는다(계약 C-12).</para>
    /// <para>전체 로그아웃이면 back-channel 로 GOP 세션이 이미 끝나 있어 로그아웃 요청이 실패할 수 있다 — 무시하고 진행한다.</para>
    /// </summary>
    internal async Task SignOutFromAgentAsync(SsoEventAction action)
    {
        if (Interlocked.CompareExchange(ref _signingOut, 1, 0) != 0) return;   // 겹친 사건 — 한 번만
        try
        {
            if (!_store.IsAuthenticated) return;   // 이미 로그아웃(사람 · 401 · 만료)

            Volatile.Write(ref _signOutNotice, NoticeFor(action));

            try
            {
                var r = await _api.LogoutAsync().ConfigureAwait(false);
                _log?.Info($"[SSO] 에이전트 사건으로 GOP 로그아웃 — success={r?.Success}");
            }
            catch (Exception ex)
            {
                _log?.Info($"[SSO] GOP 로그아웃 실패(진행) — {ex.GetType().Name} {ex.Message}");
            }

            // 훅부터 뗀다 — 화면 전환 사이에 나간 요청이 401 을 받아도 재교환으로 되살리지 않게.
            if (ReferenceEquals(BearerAuthHandler.SsoReauthenticator, _hook))
                BearerAuthHandler.SsoReauthenticator = null;

            if (_lifecycle is not null)
                _lifecycle.ForceLogoutOnce(EnumRevokeReason.SessionRevoked);
            else
                _store.Clear();
        }
        catch (Exception ex)
        {
            _log?.Warning($"[SSO] 에이전트 사건 로그아웃 처리 예외: {ex.GetType().Name} {ex.Message}");
        }
        finally
        {
            StopWatch();
        }
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
        // 에이전트 3.10.13 부터 "이 앱만 로그아웃" 으로 막힌 경우도 같은 NoActiveSession 으로 온다(문구로만 갈림 —
        // SSO 3.10.13 통보). 글자 맞추기로 가르지 않고 두 경우 모두 맞는 행동을 말한다.
        SsoSignInStatus.NeedsAgentLogin => "SSO 에이전트에 로그인돼 있지 않거나 이 프로그램이 로그아웃된 상태입니다. [SSO 로 로그인] 을 누르세요.",
        SsoSignInStatus.NeedsAdminRegistration => "이 프로그램이 SSO 에 등록되지 않았습니다. 관리자에게 문의하세요.",
        SsoSignInStatus.ConsentPending => "SSO 에이전트 창에서 이 프로그램의 연결을 허용해 주세요.",
        SsoSignInStatus.TemporarilyUnavailable => "SSO 서버에 잠시 연결할 수 없습니다. 아이디로 로그인하세요.",
        SsoSignInStatus.ExchangeRejected => $"SSO 로그인이 거절되었습니다 — {Message}",
        SsoSignInStatus.Failed => "SSO 로그인에 실패했습니다. 아이디로 로그인하세요.",
        _ => string.Empty,
    };

    /// <summary>
    /// 사람이 <b>"SSO 로 로그인" 단추</b>를 눌렀을 때의 안내 — 조용한 폴백도 말로 알려야 한다(눌렀는데 아무 일도 없으면 고장으로 읽힌다).
    /// </summary>
    public string ButtonGuidance => Status switch
    {
        SsoSignInStatus.SignedIn => string.Empty,
        SsoSignInStatus.AgentUnavailable => "SSO 에이전트가 실행 중이 아닙니다. 에이전트를 켠 뒤 다시 누르세요.",
        SsoSignInStatus.ServerNotSupported => "이 서버는 아직 SSO 로그인을 지원하지 않습니다. 아이디로 로그인하세요.",
        SsoSignInStatus.NeedsAgentLogin => "SSO 에이전트에서 로그인을 마치지 못했습니다. 다시 누르거나 아이디로 로그인하세요.",
        _ => Guidance,
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

/// <summary>에이전트 사건 판정 — <see cref="SsoSessionCoordinator.Decide"/>.</summary>
public enum SsoEventAction
{
    /// <summary>모르는 사건 — 아무 것도 하지 않는다.</summary>
    Ignore,
    /// <summary><c>signed-out</c> · <c>logout</c> — 에이전트에서 "이 앱만 로그아웃".</summary>
    AppSignedOut,
    /// <summary><c>signed-out</c> · <c>blocked</c> — 관리자가 이 앱을 막음.</summary>
    AppBlocked,
    /// <summary><c>session-ended</c> · <c>logout</c> — 전체 로그아웃(에이전트 · 다른 앱 · 웹).</summary>
    SessionEnded,
    /// <summary><c>session-ended</c> · <c>revoked</c> — 서버가 세션을 끝냄(관리자 · GOP 제재).</summary>
    SessionRevoked,
    /// <summary><c>session-ended</c> · <c>blocked</c> — 관리자가 이 PC 를 막음.</summary>
    PcBlocked,
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
