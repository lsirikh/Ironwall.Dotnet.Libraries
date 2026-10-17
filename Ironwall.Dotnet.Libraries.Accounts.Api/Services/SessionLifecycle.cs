using Ironwall.Dotnet.Libraries.Accounts.Api.Handlers;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Services;

/// <summary>
/// <see cref="ISessionLifecycle"/> 구현(SingleInstance). Interlocked once-guard 로 강제 로그아웃을 1회만 수행.
/// 토큰·권한 Clear 후 이벤트는 lock/try 밖에서 발화(재진입·구독자 예외 격리).
/// </summary>
public class SessionLifecycle : ISessionLifecycle
{
    private readonly ITokenStorageService _tokenStore;
    private readonly IPermissionService _permission;
    private readonly ILogService? _log;
    private int _loggingOut;   // 0=idle, 1=in-progress (Interlocked once-guard)
    private Timer? _expiryTimer;   // T1: 세션 만료 능동 감지 타이머
    private readonly object _timerGate = new();   // 타이머 교체는 로그인·갱신·발화 스레드가 겹친다

    public SessionLifecycle(ITokenStorageService tokenStore, IPermissionService permission, ILogService? log = null)
    {
        _tokenStore = tokenStore;
        _permission = permission;
        _log = log;
        _tokenStore.TokensRenewed += OnTokensRenewed;   // refresh/재발급 시 만료 타이머 재무장(token-refresh-08)
    }

    /// <summary>토큰 갱신 시 새 exp 로 만료 타이머 재무장(token-refresh-08 — 갱신 후에도 옛 exp 로 강제 로그아웃되던 실버그 차단).
    /// 강제 로그아웃 진행 중이면 무시(폐기 세션 부활 방지).</summary>
    private void OnTokensRenewed()
    {
        if (_loggingOut != 0) return;
        try { ArmExpiryTimer(); }
        catch (Exception ex) { _log?.Warning($"[SessionLifecycle] TokensRenewed 재무장 실패: {ex.Message}"); }
    }

    public event Action<EnumRevokeReason>? ForceLogoutRequested;

    public void ForceLogoutOnce(EnumRevokeReason reason)
    {
        // 동시/연속(NATS revoke + 401 + 수동) 트리거에도 1회만 — 이중 CloseAllWindows/팝업/깜빡임 방지(FR-FL-04)
        if (Interlocked.CompareExchange(ref _loggingOut, 1, 0) != 0)
        {
            _log?.Info($"[SessionLifecycle] 강제 로그아웃 중복 무시 (reason={reason})");
            return;
        }

        try
        {
            _tokenStore.Clear();   // Generation 증가 → 진행 중 refresh 부활 차단(FR-FL-05)
            _permission.Clear();   // PermissionsChanged 발화 → PTZ/장비/이벤트/맵 게이팅 즉시 재평가(FR-EN-11)
            DisarmExpiryTimer();   // T1: 만료 타이머 해제(로그아웃됐으니 불필요)
            _log?.Info($"[SessionLifecycle] 강제 로그아웃 수행 (reason={reason})");
        }
        catch (Exception ex)
        {
            _log?.Error($"[SessionLifecycle] Clear 실패: {ex.Message}");
        }

        // 발화는 try/lock 밖 — 구독자(GIS PTZ 정지·스트림 해제 / 셸 가림막·로그인 전환)
        try { ForceLogoutRequested?.Invoke(reason); }
        catch (Exception ex) { _log?.Warning($"[SessionLifecycle] ForceLogoutRequested 구독자 예외: {ex.Message}"); }
    }

    public void ResetForLogin()
    {
        Interlocked.Exchange(ref _loggingOut, 0);
        ArmExpiryTimer();   // T1: 새 로그인 → 세션 만료 능동 감지 무장(서버 AUTH_MODE=public라 401 없어도 만료 시 강제 로그아웃)
    }

    /// <summary>
    /// T1: access token 만료(exp) 도달 시 능동적으로 <see cref="ForceLogoutOnce"/>(TokenExpired) 호출.
    /// 서버 401 없이도(AUTH_MODE=public) 만료 세션이 UI에 유령처럼 남는 것을 방지. exp 미상이면 타이머 없음.
    /// </summary>
    private void ArmExpiryTimer()
    {
        DisarmExpiryTimer();
        var exp = _tokenStore.AccessExpiresAtUtc;
        if (exp is null) return;                       // 무만료/DB모드 → 타이머 불요
        var due = exp.Value - DateTime.UtcNow;
        if (due <= TimeSpan.Zero) { OnExpiry(); return; }   // 이미 만료 → 즉시

        // System.Threading.Timer 의 dueTime 상한 = 4294967294ms(≈49.7일). 초과 시(장수명 토큰,
        // 예: exp가 수년 후) 그대로 넘기면 ArgumentOutOfRangeException 으로 무장 실패 → 능동 만료 감지가
        // 아예 안 걸리던 버그(로그: dueTime '315...' must be ≤ '4294967294'). 상한을 넘으면 능동 타이머를
        // 생략한다 — 그렇게 먼 만료는 실사용 세션 내 도달 불가하고, 실제 만료는 요청 시 401→refresh 로 반응 처리됨.
        const double MaxTimerMs = 4_294_967_294d;
        if (due.TotalMilliseconds > MaxTimerMs)
        {
            _log?.Info($"[SessionLifecycle] 만료가 매우 김(exp={exp.Value:o}, {due.TotalDays:F0}일 후) — 능동 만료 타이머 생략(Timer 한계 초과, 401 반응 처리 위임)");
            return;
        }
        // SSO 세션은 refresh 가 없어 만료 전에 재교환해야 한다(3자 계약 "만료 120초 전 재발급"). 모드는 무장 시점이 아니라
        // **발화 시점**에 본다 — SSO 로그인은 ResetForLogin(여기) 뒤에 훅을 켜므로 무장 시점에는 아직 레거시처럼 보인다.
        // 그래서 항상 만료 120초 전에 깨어나고, 레거시면 만료 시각으로 다시 잔다.
        var wake = ComputeWakeDelay(due);
        ArmTimer(wake, exp.Value);
        _log?.Info($"[SessionLifecycle] 세션 만료 타이머 무장 — exp={exp.Value:o} ({due.TotalMinutes:F1}분 후, 깨어남 {wake.TotalMinutes:F1}분 후)");
    }

    /// <summary>만료 몇 초 전에 깨어나 재교환하는가(3자 계약 C-12 확약 "만료 120초 전 재발급").</summary>
    internal static readonly TimeSpan SsoRenewLead = TimeSpan.FromSeconds(120);

    /// <summary>재교환 사이 최소 간격 — 서버가 수명이 짧은 토큰을 주어도 재교환이 연달아 돌지 않게.</summary>
    internal static readonly TimeSpan SsoRenewMinInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 만료까지 <paramref name="untilExpiry"/> 남았을 때 언제 깨어날지. <c>만료 − 120초</c> 이되 최소 30초 뒤, 만료보다 늦지 않게.
    /// 순수 함수 — 시험 대상.
    /// </summary>
    internal static TimeSpan ComputeWakeDelay(TimeSpan untilExpiry)
    {
        if (untilExpiry <= TimeSpan.Zero) return TimeSpan.Zero;
        var wake = untilExpiry - SsoRenewLead;
        if (wake < SsoRenewMinInterval) wake = SsoRenewMinInterval;
        return wake > untilExpiry ? untilExpiry : wake;
    }

    private void ArmTimer(TimeSpan delay, DateTime expUtc)
    {
        try
        {
            lock (_timerGate)
            {
                DisarmExpiryTimer();
                _expiryTimer = new Timer(_ => _ = OnWakeAsync(expUtc), null, delay, Timeout.InfiniteTimeSpan);
            }
        }
        catch (Exception ex) { _log?.Warning($"[SessionLifecycle] 만료 타이머 무장 실패: {ex.Message}"); }
    }

    /// <summary>
    /// 타이머 발화. SSO 모드면 재교환을 먼저 시도하고, 레거시면 만료 시각에 강제 로그아웃(예전 그대로).
    /// <list type="bullet">
    /// <item>Renewed — 저장소의 <c>TokensRenewed</c> 가 새 exp 로 다시 무장한다(여기서 할 일 없음)</item>
    /// <item>Transient — 만료 전이면 30초 뒤 다시, 시간이 없으면 강제 로그아웃</item>
    /// <item>Terminal — 강제 로그아웃(에이전트 세션 끝 · 미등록 등)</item>
    /// </list>
    /// 예외를 밖으로 내지 않는다(타이머 스레드).
    /// </summary>
    internal async Task OnWakeAsync(DateTime expUtc)
    {
        try
        {
            if (_loggingOut != 0) return;
            var stale = _tokenStore.AccessToken;
            if (string.IsNullOrEmpty(stale)) return;   // 이미 로그아웃
            if (_tokenStore.AccessExpiresAtUtc != expUtc) return;   // 그 사이 토큰이 바뀌었다 — 새 exp 의 타이머가 따로 있다

            SsoReauthOutcome? outcome = null;
            if (BearerAuthHandler.SsoReauthenticator is not null)
            {
                _log?.Info("[SessionLifecycle] SSO 세션 만료 임박 — 재교환 시도");
                outcome = await BearerAuthHandler.RenewSsoAheadAsync(_tokenStore, stale).ConfigureAwait(false);
            }

            var remaining = expUtc - DateTime.UtcNow;
            switch (outcome)
            {
                case null:   // 레거시 — 만료 시각까지 기다렸다 강제 로그아웃(예전 동작)
                    if (remaining > TimeSpan.Zero) { ArmTimer(remaining, expUtc); return; }
                    OnExpiry();
                    return;
                case SsoReauthOutcome.Renewed:
                    _log?.Info("[SessionLifecycle] SSO 만료 전 재교환 성공");
                    return;   // TokensRenewed → ArmExpiryTimer 가 새 exp 로 무장했다
                case SsoReauthOutcome.Transient when remaining > SsoRenewMinInterval:
                    _log?.Warning($"[SessionLifecycle] SSO 만료 전 재교환 일시 실패 — {SsoRenewMinInterval.TotalSeconds:F0}초 뒤 다시 (만료까지 {remaining.TotalSeconds:F0}초)");
                    ArmTimer(SsoRenewMinInterval, expUtc);
                    return;
                case SsoReauthOutcome.Transient when remaining > TimeSpan.Zero:
                    _log?.Warning("[SessionLifecycle] SSO 만료 전 재교환 일시 실패 — 만료 시각에 마지막으로 다시");
                    ArmTimer(remaining, expUtc);
                    return;
                default:
                    _log?.Warning($"[SessionLifecycle] SSO 재교환 실패({outcome}) — 강제 로그아웃");
                    OnExpiry();
                    return;
            }
        }
        catch (Exception ex)
        {
            _log?.Warning($"[SessionLifecycle] 만료 타이머 처리 예외: {ex.GetType().Name} {ex.Message}");
        }
    }

    private void DisarmExpiryTimer()
    {
        lock (_timerGate)
        {
            _expiryTimer?.Dispose();
            _expiryTimer = null;
        }
    }

    private void OnExpiry()
    {
        _log?.Info("[SessionLifecycle] 세션 만료 감지 → 강제 로그아웃(TokenExpired)");
        ForceLogoutOnce(EnumRevokeReason.TokenExpired);
    }

    public event Action? LoginSucceeded;

    public void NotifyLoginSucceeded()
    {
        // 구독자(GIS init/Device fetch) 예외 격리. 토큰·권한은 이미 적용된 상태에서 호출됨.
        try { LoginSucceeded?.Invoke(); }
        catch (Exception ex) { _log?.Warning($"[SessionLifecycle] LoginSucceeded 구독자 예외: {ex.Message}"); }
    }
}
