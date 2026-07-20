using Ironwall.Dotnet.Libraries.Base.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Services;

/// <summary>
/// Grant Scheduling v5.2 FR-GS-03 — grant 만료(valid_until) 능동 재조회로 UI 실시간 컷오프.
/// 로그인/로그아웃 구독 관리. 앱 <see cref="IService"/> 러너가 기동/종료.
/// </summary>
public interface IPermissionRefreshService
{
    /// <summary>로그인/로그아웃 구독 시작(멱등).</summary>
    void Start();
    /// <summary>구독 해제 + 타이머 정지.</summary>
    void Stop();
}

/// <summary>
/// FR-GS-03 구현. 로그인 → <c>/me/permissions</c> 재조회(Refresh) → <c>valid_until</c> 타이머 무장.
/// 타이머 발화 → 재조회 → 재무장(체인). 로그아웃 → 정지.
/// <para>보안 권위는 서버(만료 시 매 요청 403). 본 서비스는 UI 선제 비활성 목적 — fail-safe: 권한 확대 없음,
/// 재조회 실패 시 세션을 지우지 않고 서버 403 에 위임(다음 로그인/타이머에 재시도).</para>
/// <para>스레드: 타이머 콜백=스레드풀. Refresh 는 PermissionService 가 <c>_gate</c> 로 직렬화, PermissionsChanged 는
/// lock 밖 발화 → 구독 VM 이 UI 마샬(기존 게이팅 패턴).</para>
/// </summary>
public sealed class PermissionRefreshService : IPermissionRefreshService, IService, IDisposable
{
    private readonly IAccountApiService _api;
    private readonly IPermissionService _perm;
    private readonly ISessionLifecycle _session;
    private readonly IClock _clock;
    private readonly ILogService? _log;

    private readonly object _armGate = new();
    private Timer? _timer;
    private int _started;

    /// <summary>만료 직후 발화(서버가 확실히 만료 처리하도록 소량 버퍼).</summary>
    internal static readonly TimeSpan FireBuffer = TimeSpan.FromSeconds(2);
    /// <summary>재조회 폭주 방지(디바운스) — 이미 임박/경과 시 최소 간격 후 재조회.</summary>
    internal static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(5);
    /// <summary>먼 미래 만료는 상한마다 재확인(장기 타이머 드리프트·오버플로 회피).</summary>
    internal static readonly TimeSpan MaxDelay = TimeSpan.FromHours(6);

    public PermissionRefreshService(IAccountApiService api, IPermissionService perm, ISessionLifecycle session,
        IClock clock, ILogService? log = null)
    {
        _api = api;
        _perm = perm;
        _session = session;
        _clock = clock;
        _log = log;
    }

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1) return;
        _session.LoginSucceeded += OnLogin;
        _session.ForceLogoutRequested += OnLogout;
        _log?.Info("[PermRefresh] 시작(로그인/로그아웃 구독).");
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0) return;
        _session.LoginSucceeded -= OnLogin;
        _session.ForceLogoutRequested -= OnLogout;
        Disarm();
        _log?.Info("[PermRefresh] 정지.");
    }

    // IService — 앱 러너가 기동/종료
    public Task ExecuteAsync(CancellationToken token = default) { Start(); return Task.CompletedTask; }
    public Task StopAsync(CancellationToken token = default) { Stop(); return Task.CompletedTask; }

    private void OnLogin() => _ = RefreshAndRearmAsync();          // fire-and-forget(내부 예외 흡수)
    private void OnLogout(EnumRevokeReason _) => Disarm();          // 강제 로그아웃 시 권한은 SessionLifecycle이 Clear, 타이머만 해제

    /// <summary>재조회 + Refresh + 다음 due 계산 후 타이머 재무장. 반환=다음 due(null=상시/실패). 테스트 진입점(내부).</summary>
    internal async Task<TimeSpan?> RefreshAndRearmAsync()
    {
        try
        {
            var res = await _api.GetMyPermissionsAsync().ConfigureAwait(false);
            if (!res.Success || res.Data is null)
            {
                // fail-safe: 권한 확대 없음, 세션 Clear 안 함(일시 실패는 서버 403이 최종 방어). 다음 로그인/타이머에 재시도.
                _log?.Warning($"[PermRefresh] /me/permissions 실패 → 보류(서버 403 최종). code={res.Error?.Code}");
                return null;
            }
            _perm.Refresh(res.Data);   // 토큰/valid_until/clockSkew 갱신 + PermissionsChanged → UI 재게이팅
            var due = ComputeDue(_perm.ValidUntil, _perm.ClockSkew);
            Arm(due);
            return due;
        }
        catch (Exception ex)
        {
            _log?.Warning($"[PermRefresh] 재조회 예외 흡수: {ex.Message}");
            return null;
        }
    }

    /// <summary>다음 재조회까지 지연. null=상시(만료없음)→타이머 없음.
    /// 서버 valid_until 을 클라 시각으로 보정(−clockSkew) + <see cref="FireBuffer"/>, [<see cref="MinInterval"/>,<see cref="MaxDelay"/>] 클램프.</summary>
    internal TimeSpan? ComputeDue(DateTimeOffset? validUntil, TimeSpan clockSkew)
    {
        if (validUntil is not { } vu) return null;
        // 만료의 client-instant = serverInstant − clockSkew. due = 그 시점 − 현재.
        var due = vu.UtcDateTime - _clock.UtcNow - clockSkew + FireBuffer;
        if (due < MinInterval) due = MinInterval;
        if (due > MaxDelay) due = MaxDelay;
        return due;
    }

    private void Arm(TimeSpan? due)
    {
        lock (_armGate)
        {
            _timer?.Dispose();
            _timer = null;
            if (due is not { } d) return;
            try { _timer = new Timer(_ => _ = RefreshAndRearmAsync(), null, d, Timeout.InfiniteTimeSpan); }
            catch (Exception ex) { _log?.Warning($"[PermRefresh] 타이머 무장 실패: {ex.Message}"); }
        }
    }

    private void Disarm()
    {
        lock (_armGate) { _timer?.Dispose(); _timer = null; }
    }

    public void Dispose() => Stop();
}
