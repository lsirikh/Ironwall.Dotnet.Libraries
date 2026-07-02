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

    public SessionLifecycle(ITokenStorageService tokenStore, IPermissionService permission, ILogService? log = null)
    {
        _tokenStore = tokenStore;
        _permission = permission;
        _log = log;
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
        try
        {
            _expiryTimer = new Timer(_ => OnExpiry(), null, due, Timeout.InfiniteTimeSpan);
            _log?.Info($"[SessionLifecycle] 세션 만료 타이머 무장 — exp={exp.Value:o} ({due.TotalMinutes:F1}분 후)");
        }
        catch (Exception ex) { _log?.Warning($"[SessionLifecycle] 만료 타이머 무장 실패: {ex.Message}"); }
    }

    private void DisarmExpiryTimer()
    {
        _expiryTimer?.Dispose();
        _expiryTimer = null;
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
