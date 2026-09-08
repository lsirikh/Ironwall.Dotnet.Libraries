using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : 활성 억제 창(정비 창) 폴링 모니터 — GET /active 를 주기 폴링(30s)해
                  현재 억제 중 목록을 노출한다. G-2 활성 억제 배너의 데이터 소스.
                  억제 은폐 방지(안전) — 정비 중임을 상시 인지시키기 위한 SSOT.
                  API 6.3.3: /active 의미가 "지금 회차 진행 중인 창만" 으로 바뀌었다.
   Created By   : GHLee
   Created On   : 2026-08-01
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>활성 억제 창 폴링 모니터. 배너 등 소비자는 <see cref="ActiveChanged"/> 구독 + <see cref="Active"/> 읽기.</summary>
public interface ISuppressionActiveMonitor : IService
{
    /// <summary>현재 활성(진행 중) 억제 창 목록(읽기 전용 스냅샷).</summary>
    IReadOnlyList<EventSuppressionScheduleDto> Active { get; }

    /// <summary>활성 목록 갱신 시 발화(UI 스레드).</summary>
    event Action? ActiveChanged;

    /// <summary>마지막으로 <b>성공한</b> 폴링 시각. 한 번도 성공 못 했으면 null.</summary>
    DateTime? LastSuccessAt { get; }

    /// <summary>
    /// 마지막 성공 이후 TTL(폴링주기 × 3 = 90초)을 넘겼는가.
    /// <para>⚠ stale 이어도 <see cref="Active"/> 를 <b>버리지 않는다</b> — 조용한 소멸은
    /// 이 모니터의 존재 이유(억제 은폐 방지)를 정면으로 배반한다. UI 는 목록과 stale 을 <b>병기</b>한다.</para>
    /// </summary>
    bool IsStale { get; }

    /// <summary>stale 표기용 경과 문구(예: "3분 전").</summary>
    string LastSuccessAgeText { get; }

    /// <summary>
    /// 즉시 1회 폴링을 요청한다(NATS 가속 신호·패널 진입 등).
    /// <para>리딩 엣지 + 트레일링 보장 스로틀 — 쿨다운 안의 요청은 <b>버리지 않고</b> 1건으로 접어
    /// 쿨다운 종료 시점에 발화한다. 마지막 이벤트를 드롭하면 영구 침묵이 된다.</para>
    /// </summary>
    void RequestImmediatePoll(string reason = "");
}

/// <summary>
/// <see cref="ISuppressionActiveMonitor"/> 구현 — DispatcherTimer(30s) 폴링.
/// 로그인 게이팅: 미인증 시 폴링 스킵(무인 상태 서버 호출 방지, 하위호환 tokenStorage=null → 항상 폴링).
/// </summary>
public class SuppressionActiveMonitor : ISuppressionActiveMonitor, IDisposable
{
    #region - Ctors -
    public SuppressionActiveMonitor(
        ILogService? log,
        IEventSuppressionApiService api,
        ITokenStorageService? tokenStorage = null)
    {
        _log = log;
        _api = api;
        _tokenStorage = tokenStorage;
    }
    #endregion

    #region - IService -
    public Task ExecuteAsync(CancellationToken token = default)
    {
        if (_timer != null) return Task.CompletedTask;   // 멱등
        // UI 디스패처에 바인딩(시작 스레드 무관 안전) — 미기동 시 CurrentDispatcher 폴백.
        _dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        _timer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        { Interval = TimeSpan.FromSeconds(POLL_SECONDS) };
        _timer.Tick += async (_, _) => await PollAsync();
        _timer.Start();

        // 트레일링 발화 타이머 — 쿨다운 중 접힌 요청 1건을 뒤늦게 흘려보낸다.
        _trailing = new DispatcherTimer(DispatcherPriority.Background, _dispatcher);
        _trailing.Tick += async (_, _) =>
        {
            _trailing!.Stop();
            _hasPending = false;
            await PollAsync();
        };

        _ = PollAsync();   // 최초 1회 즉시
        _log?.Info($"[{nameof(SuppressionActiveMonitor)}] started — /active {POLL_SECONDS}s 폴링 (TTL {TtlSeconds}s)");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        _timer?.Stop();
        _timer = null;
        _trailing?.Stop();
        _trailing = null;
        _hasPending = false;
        return Task.CompletedTask;
    }
    #endregion

    #region - Poll -
    private async Task PollAsync()
    {
        // 로그인 게이팅 — 미인증 시 서버 호출 안 함(무인 폴링 금지).
        if (_tokenStorage is { IsAuthenticated: false }) return;
        // 로그인 게이팅을 통과한 첫 순간을 stale 기산점으로 삼는다(미인증 대기 시간은 세지 않는다).
        _startedAt ??= DateTime.Now;
        if (_inFlight) return;                              // 재진입 가드
        _inFlight = true;
        try
        {
            var res = await _api.GetActiveSuppressionSchedulesAsync();
            if (!res.Success || res.Data is null)
            {
                NotifyStaleIfNeeded();
                return;
            }
            Apply(res.Data, DateTime.Now);
        }
        catch (Exception ex)
        {
            _log?.Warning($"[{nameof(SuppressionActiveMonitor)}] /active 폴링 실패: {ex.Message}");
            NotifyStaleIfNeeded();
        }
        finally { _inFlight = false; }
    }

    /// <summary>성공 결과 반영 — <b>UI 스레드로 마샬링</b>한다.</summary>
    private void Apply(IReadOnlyList<EventSuppressionScheduleDto> data, DateTime stamp)
    {
        void Commit()
        {
            _active = data;
            _lastSuccessAt = stamp;
            _lastPollAt = stamp;
            _wasStale = false;
            ActiveChanged?.Invoke();
        }

        // ⚠ 지금은 유일 호출자가 DispatcherTimer 라 UI 스레드가 '우연히' 보장된다.
        //    NATS 백그라운드 스레드에서 즉시 폴링을 부르는 순간 그 우연이 깨지므로 명시 마샬링한다.
        if (_dispatcher is { } d && !d.CheckAccess()) d.InvokeAsync(Commit);
        else Commit();
    }

    /// <summary>실패 시 — 목록은 <b>유지</b>하고 stale 전이만 통지한다.</summary>
    private void NotifyStaleIfNeeded()
    {
        _lastPollAt = DateTime.Now;
        if (!IsStale || _wasStale) return;
        _wasStale = true;
        if (_dispatcher is { } d && !d.CheckAccess()) d.InvokeAsync(() => ActiveChanged?.Invoke());
        else ActiveChanged?.Invoke();
    }
    #endregion

    #region - ISuppressionActiveMonitor -
    public IReadOnlyList<EventSuppressionScheduleDto> Active => _active;
    public event Action? ActiveChanged;

    public DateTime? LastSuccessAt => _lastSuccessAt;

    /// <summary>
    /// ⚠ 기준 시각은 <c>_lastSuccessAt ?? _startedAt</c> 이다.
    /// <para>성공 시각만 보면 <b>한 번도 성공하지 못한</b> 폴링(서버 재기동·구버전 404·502)이
    /// 영원히 stale 이 아니게 되어 배너가 통째로 침묵한다 — 그러면 화면이
    /// '억제 0건'과 '서버에 물어본 적 없음'을 구분 불가하게 같은 그림으로 보여준다.</para>
    /// </summary>
    public bool IsStale =>
        SuppressionPollThrottle.IsStale(DateTime.Now, _lastSuccessAt ?? _startedAt, TtlSeconds);

    public string LastSuccessAgeText => SuppressionPollThrottle.DescribeAge(DateTime.Now, _lastSuccessAt);

    public void RequestImmediatePoll(string reason = "")
    {
        if (_timer is null) return;                         // 미기동
        var now = DateTime.Now;
        var decision = SuppressionPollThrottle.Decide(now, _lastPollAt, _hasPending);

        switch (decision)
        {
            case PollDecision.PollNow:
                _lastPollAt = now;
                _log?.Info($"[{nameof(SuppressionActiveMonitor)}] 즉시 폴링({reason})");
                _ = PollAsync();
                break;

            case PollDecision.Defer:
                // 쿨다운 중 — 버리지 않고 1건으로 접어 예약한다.
                _hasPending = true;
                var wait = SuppressionPollThrottle.RemainingCooldown(now, _lastPollAt);
                if (_trailing is { } t)
                {
                    t.Interval = wait <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : wait;
                    t.Stop();
                    t.Start();
                }
                _log?.Info($"[{nameof(SuppressionActiveMonitor)}] 폴링 접음({reason}) — {wait.TotalSeconds:0.0}s 후 발화");
                break;

            case PollDecision.AlreadyScheduled:
                break;
        }
    }
    #endregion

    #region - IDisposable -
    public void Dispose()
    {
        _timer?.Stop();
        _timer = null;
        _trailing?.Stop();
        _trailing = null;
        _hasPending = false;
        ActiveChanged = null;
        GC.SuppressFinalize(this);
    }
    #endregion

    #region - Attributes -
    private const int POLL_SECONDS = 30;
    /// <summary>fail-open TTL — 폴링주기 × 3.</summary>
    public const double TtlSeconds = POLL_SECONDS * 3;

    private readonly ILogService? _log;
    private readonly IEventSuppressionApiService _api;
    private readonly ITokenStorageService? _tokenStorage;

    private DispatcherTimer? _timer;
    private DispatcherTimer? _trailing;
    private Dispatcher? _dispatcher;

    private IReadOnlyList<EventSuppressionScheduleDto> _active = new List<EventSuppressionScheduleDto>();
    private DateTime? _lastSuccessAt;
    /// <summary>stale 기산점 — 한 번도 성공 못 한 경우의 기준.</summary>
    private DateTime? _startedAt;
    private DateTime? _lastPollAt;
    private bool _hasPending;
    private bool _inFlight;
    private bool _wasStale;
    #endregion
}
