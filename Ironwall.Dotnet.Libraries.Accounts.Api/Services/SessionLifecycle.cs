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

    public void ResetForLogin() => Interlocked.Exchange(ref _loggingOut, 0);

    public event Action? LoginSucceeded;

    public void NotifyLoginSucceeded()
    {
        // 구독자(GIS init/Device fetch) 예외 격리. 토큰·권한은 이미 적용된 상태에서 호출됨.
        try { LoginSucceeded?.Invoke(); }
        catch (Exception ex) { _log?.Warning($"[SessionLifecycle] LoginSucceeded 구독자 예외: {ex.Message}"); }
    }
}
