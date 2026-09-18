using Ironwall.Dotnet.Libraries.Base.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Api.Services;
/****************************************************************************
   Purpose      : 서버 계약 세대 프로브의 부팅 확보 훅 — FR-08 활성화 경로
   Created By   : GHLee
   Created On   : 9/18/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// <see cref="IServerContractProbe"/> 를 <b>부팅 시 1회</b> 확보시키는 활성화 훅.
/// </summary>
/// <remarks>
/// <para><b>왜 필요한가</b> — 프로브 구현·DI 등록·선택 주입까지 다 있어도
/// <see cref="IServerContractProbe.ResolveAsync"/> 를 <b>부르는 코드가 없으면</b> 캐시가 영원히 비고
/// <see cref="IServerContractProbe.Contract"/> 는 폴백 <see cref="EnumServerContract.V6_3"/> 에 고정된다
/// (= 버전 분기 전체가 죽은 코드). 이 서비스가 그 호출부다.</para>
///
/// <para><b>왜 이 방식인가(레포 관용구 준수)</b> — 이 레포의 "앱 기동 직후 1회 실행" 정본은
/// <see cref="IService"/> + <c>WithMetadata("Order", n)</c> 다.
/// <c>ParentBootstrapper.Start()</c>(<c>Libraries.Base/ParentBootstrapper.cs:64-83</c>)가
/// <c>IEnumerable&lt;Meta&lt;IService&gt;&gt;</c> 를 <c>Order</c> 오름차순으로 정렬해 <c>ExecuteAsync</c> 를 순차 await 한다.
/// <c>Autofac.IStartable</c> 은 레포에 선례가 <b>0건</b>이고 컨테이너 빌드 시점 <b>동기</b> 실행이라
/// async HTTP 확보에는 부적합하다(sync-over-async 교착 위험). Caliburn.Micro <c>IHandle&lt;&gt;</c> 는
/// EventAggregator 발행 주체가 있어야 하고 부팅 시점 보장이 없어 역시 부적합하다.
/// 따라서 <b>새 패턴을 만들지 않고</b> 기존 <see cref="IService"/> 러너에 올라탄다.</para>
///
/// <para><b>왜 이 시점인가</b> — <c>openapi.json</c> 은 <b>무인증 200</b> 이라 로그인·토큰이 필요 없다
/// (세션 축출 위험도 없다). 반대로 계약 세대를 모르면 <b>첫 장비 쓰기부터</b> 본문이 틀린다
/// (6.3 은 <c>type_device</c> 필수 / 7.0+ 는 금지). 그래서 로그인 이후가 아니라
/// <b>서비스 러너의 최선두</b>(<see cref="BOOT_ORDER"/>)에 둔다 — 어떤 API 소비자보다 먼저 끝난다.</para>
///
/// <para><b>기동을 막지 않는다(요구 3)</b> — 러너가 <c>ExecuteAsync</c> 를 순차 await 하므로
/// 네트워크가 블랙홀이면 프로브 HttpClient 타임아웃(기본 10초) × 후보수만큼 부팅이 멈출 수 있다.
/// 그래서 <see cref="BUDGET_SEC"/> 예산만 기다리고, 예산을 넘기면 <b>백그라운드로 넘기고 즉시 반환</b>한다.
/// 확보는 그 뒤에도 계속되어(러너 잔여 서비스 + <c>Task.Delay(3000)</c> + Provider 초기화 구간) 캐시를 채운다.
/// 실패하면 프로브가 폴백 <see cref="EnumServerContract.V6_3"/> 을 유지하므로 앱은 정상 기동한다.
/// <c>async void</c>·<c>.Result</c>·<c>.Wait()</c> 는 쓰지 않고, 배경 Task 는 반드시 관측한다
/// (UnobservedTaskException 방지).</para>
///
/// <para><b>중복 등록 안전</b> — <see cref="ApiModule"/> 은 도메인별로 여러 번 등록된다(DeviceApi·EventApi·ReportApi…).
/// 등록부에서 <c>IfNotRegistered</c> 로 1개만 살리지만, 설령 여러 개가 살아도
/// ① 인스턴스별 <see cref="_started"/> 멱등 가드 ② <see cref="IServerContractProbe.ResolveAsync"/> 자체가 멱등
/// ③ 이미 <see cref="IServerContractProbe.IsResolved"/> 인 프로브는 건너뜀 — 이 3중으로 중복 왕복이 없다.</para>
/// </remarks>
public sealed class ServerContractBootService : IService, IDisposable
{
    #region - Ctors -
    /// <param name="log">로그 서비스(선택).</param>
    /// <param name="probes">
    /// 컨테이너에 등록된 <b>모든</b> 프로브. <see cref="ApiModule"/> 이 <c>setup</c> 1:1 로 인스턴스를 만들기 때문에
    /// 도메인(DeviceApi·EventApi·ReportApi…)마다 별개 싱글턴이다 — 기본(default) 하나만 확보하면
    /// <c>ResolveNamed</c> 소비자가 폴백에 남는다. 그래서 전부 확보한다(동시 실행이라 지연은 1건분).
    /// </param>
    /// <param name="budgetSeconds">부팅을 붙잡아 둘 최대 시간(초). 0 이하면 기다리지 않고 바로 백그라운드로 넘긴다.</param>
    public ServerContractBootService(ILogService? log,
                                     IEnumerable<IServerContractProbe> probes,
                                     int budgetSeconds = BUDGET_SEC)
    {
        _log = log;
        _probes = (probes ?? Enumerable.Empty<IServerContractProbe>())
                  .Where(p => p is not null)
                  .ToList();
        _budget = TimeSpan.FromSeconds(budgetSeconds > 0 ? budgetSeconds : 0);
    }
    #endregion
    #region - Implementation of Interface -
    /// <summary>
    /// 부팅 확보. <b>예외를 던지지 않는다</b> — 어떤 실패도 폴백 <see cref="EnumServerContract.V6_3"/> 으로 흡수한다.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken token = default)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1) return;   // 멱등 — 중복 등록·재호출 방어

        try
        {
            if (_probes.Count == 0)
            {
                _log?.Warning($"[ServerContract] 등록된 프로브가 없습니다 — 계약 분기가 폴백 {EnumServerContract.V6_3} 로 동작합니다.");
                return;
            }

            var pending = _probes.Where(p => !p.IsResolved).ToList();
            if (pending.Count == 0)
            {
                _log?.Info("[ServerContract] 이미 확보되어 있어 부팅 확보를 건너뜁니다.");
                return;
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _cts = cts;
            var linked = cts.Token;

            // 프로브는 인스턴스별 SemaphoreSlim 으로 자기 조회를 직렬화하므로 동시 실행이 안전하다.
            // 동시에 돌려 지연이 프로브 수에 비례하지 않게 한다(요구 3).
            var all = Task.WhenAll(pending.Select(p => SafeResolveAsync(p, linked)));

            if (_budget > TimeSpan.Zero)
            {
                var finished = await Task.WhenAny(all, QuietDelayAsync(_budget, linked)).ConfigureAwait(false);
                if (ReferenceEquals(finished, all))
                {
                    foreach (var probe in pending) LogOutcome(probe);
                    return;
                }
            }

            if (linked.IsCancellationRequested)
            {
                _log?.Warning($"[ServerContract] 확보가 취소되었습니다(종료 중). 폴백 {EnumServerContract.V6_3} 유지.");
            }
            else
            {
                _log?.Warning($"[ServerContract] 확보가 {_budget.TotalSeconds:0.#}초 예산 안에 끝나지 않았습니다 — "
                            + $"부팅을 계속하고 백그라운드에서 마무리합니다. 그 사이 호출은 폴백 {EnumServerContract.V6_3} 경로로 나갑니다.");
            }

            ObserveInBackground(all, pending);   // 배경 Task 관측(UnobservedTaskException 방지)
        }
        catch (Exception ex)
        {
            // 이 훅이 부팅을 깨뜨리는 일은 없어야 한다(NFR-02).
            _log?.Warning($"[ServerContract] 부팅 확보 훅에서 예외를 흡수했습니다: {ex.Message}");
        }
    }

    /// <summary>앱 종료 — 진행 중인 확보를 취소한다(네트워크 대기로 종료가 늦어지지 않게).</summary>
    public Task StopAsync(CancellationToken token = default)
    {
        CancelQuietly();
        return Task.CompletedTask;
    }
    #endregion
    #region - Overrides -
    public void Dispose()
    {
        CancelQuietly();
        try { _cts?.Dispose(); } catch { /* 종료 경로 — 삼킨다 */ }
        _cts = null;
    }
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    /// <summary>프로브 1개 확보. 프로브는 원래 예외를 안 던지지만 방어적으로 한 번 더 감싼다.</summary>
    private async Task SafeResolveAsync(IServerContractProbe probe, CancellationToken token)
    {
        try
        {
            await probe.ResolveAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 종료·예산 취소 — 폴백 유지로 충분하다.
        }
        catch (Exception ex)
        {
            _log?.Warning($"[ServerContract] 프로브 확보 실패(흡수): {ex.Message}");
        }
    }

    /// <summary>취소를 예외로 새지 않게 감싼 지연(예산 타이머).</summary>
    private static async Task QuietDelayAsync(TimeSpan delay, CancellationToken token)
    {
        try { await Task.Delay(delay, token).ConfigureAwait(false); }
        catch (OperationCanceledException) { /* 종료 — 예산 만료와 동일 취급 */ }
    }

    /// <summary>예산 초과분을 배경에서 마무리하고 결과를 로그로 남긴다. Task 를 반드시 관측한다.</summary>
    private void ObserveInBackground(Task all, List<IServerContractProbe> pending)
    {
        _ = all.ContinueWith(t =>
        {
            try
            {
                if (t.IsFaulted)
                {
                    _log?.Warning($"[ServerContract] 배경 확보에서 예외(흡수): {t.Exception?.GetBaseException().Message}");
                    return;
                }
                if (t.IsCanceled) return;
                foreach (var probe in pending) LogOutcome(probe);
            }
            catch { /* 로깅 실패까지 번지지 않게 */ }
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private void LogOutcome(IServerContractProbe probe)
    {
        if (probe.IsResolved)
            _log?.Info($"[ServerContract] 확보 완료 — raw='{probe.RawVersion}' → {probe.Contract}");
        else
            _log?.Warning($"[ServerContract] 확보 실패 — 폴백 {probe.Contract} 로 동작합니다.");
    }

    private void CancelQuietly()
    {
        try { _cts?.Cancel(); }
        catch (ObjectDisposedException) { /* Dispose 경합 */ }
        catch (Exception ex) { _log?.Warning($"[ServerContract] 확보 취소 중 예외(흡수): {ex.Message}"); }
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    #endregion
    #region - Attributes -
    /// <summary>
    /// 서비스 러너 정렬 키. 어떤 API 소비자보다 <b>먼저</b> 돌아야 하므로 음수를 쓴다
    /// (현재 최소값은 <c>Db2Module</c> 의 1 · <c>AccountUiModule</c> 의 10).
    /// </summary>
    public const int BOOT_ORDER = -1000;

    /// <summary>부팅을 붙잡아 둘 기본 예산(초). 프로브 HttpClient 타임아웃(기본 10초)보다 짧아야 의미가 있다.</summary>
    public const int BUDGET_SEC = 5;

    private readonly ILogService? _log;
    private readonly List<IServerContractProbe> _probes;
    private readonly TimeSpan _budget;

    private CancellationTokenSource? _cts;
    private int _started;
    #endregion
}
