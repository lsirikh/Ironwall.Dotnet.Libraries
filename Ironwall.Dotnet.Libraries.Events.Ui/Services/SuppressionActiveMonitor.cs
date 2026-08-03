using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : 활성 억제 창(정비 창) 폴링 모니터 — GET /active 를 주기 폴링(30s)해
                  현재 억제 중 목록을 노출한다. G-2 활성 억제 배너(지도 z6 계기)의 데이터 소스.
                  억제 은폐 방지(안전) — 정비 중임을 상시 인지시키기 위한 SSOT.
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
        var dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(POLL_SECONDS) };
        _timer.Tick += async (_, _) => await PollAsync();
        _timer.Start();
        _ = PollAsync();   // 최초 1회 즉시
        _log?.Info($"[{nameof(SuppressionActiveMonitor)}] started — /active {POLL_SECONDS}s 폴링");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        _timer?.Stop();
        _timer = null;
        return Task.CompletedTask;
    }
    #endregion

    #region - Poll -
    private async Task PollAsync()
    {
        // 로그인 게이팅 — 미인증 시 서버 호출 안 함(무인 폴링 금지).
        if (_tokenStorage is { IsAuthenticated: false }) return;
        try
        {
            var res = await _api.GetActiveSuppressionSchedulesAsync();
            if (!res.Success || res.Data is null) return;
            _active = res.Data;
            ActiveChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _log?.Warning($"[{nameof(SuppressionActiveMonitor)}] /active 폴링 실패(무시): {ex.Message}");
        }
    }
    #endregion

    #region - ISuppressionActiveMonitor -
    public IReadOnlyList<EventSuppressionScheduleDto> Active => _active;
    public event Action? ActiveChanged;
    #endregion

    #region - IDisposable -
    public void Dispose()
    {
        _timer?.Stop();
        _timer = null;
        ActiveChanged = null;
    }
    #endregion

    #region - Attributes -
    private const int POLL_SECONDS = 30;
    private readonly ILogService? _log;
    private readonly IEventSuppressionApiService _api;
    private readonly ITokenStorageService? _tokenStorage;
    private DispatcherTimer? _timer;
    private IReadOnlyList<EventSuppressionScheduleDto> _active = new List<EventSuppressionScheduleDto>();
    #endregion
}
