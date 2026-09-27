using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Devices.Units;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;

/****************************************************************************
   Purpose      : IUnitDirectory 구현 — /graph 캐시 + SYNC_UNIT(UnitTopologyChangedMessage) 재조회 (FR-46 · FR-48)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 부대 id → "7중대 · 2대대 › 1연대 › 제○○사단". 지도(심볼 상세 · 우클릭 메뉴)가 <see cref="IUnitDirectory"/> 로 쓴다.
/// </summary>
/// <remarks>
/// <para><b>읽기</b>: 처음 필요할 때 <see cref="EnsureLoadedAsync"/> 로 <c>/graph</c> 를 1회 읽어 트리(<see cref="UnitTreeBuilder"/> — 규칙 사본 없음)를
/// 통째로 캐시한다. <see cref="Describe"/> 는 네트워크에 나가지 않는다.</para>
/// <para><b>갱신</b>: <see cref="UnitTopologyChangedMessage"/>(호스트가 옮긴 <c>SYNC_UNIT</c>)를 구독한다. 알림은 몰려오므로
/// 커널 <see cref="CoalescingTrigger"/>(창 500 ms)로 합쳐 한 번 다시 읽고, 성공하면 <see cref="Changed"/> 를 <b>한 번</b> 발화한다.
/// 아무도 쓰지 않은(한 번도 읽지 않은) 사전은 알림에 서버를 부르지 않는다. 다시 읽기가 실패하면 옛 트리를 그대로 둔다.</para>
/// <para><b>스레드</b>: 알림은 NATS 콜백 스레드에서 올 수 있다 — 처리기는 곧바로 돌아오고(발행자를 막지 않는다) 다시 읽기는 배경에서 돈다.
/// 캐시는 불변 트리 하나를 통째로 바꿔 끼우므로(원자적 참조 교체) 읽는 쪽에 잠금이 없다. <see cref="Changed"/> 는 잠금 밖에서,
/// 주입한 배송(<c>dispatch</c>, 기본 = WPF 디스패처)으로 발화한다.</para>
/// <para><b>DI</b>: 이 파일은 등록하지 않는다 — <c>DeviceUiModule</c> 은 다른 작업이 고치는 중이라 결선 단계에서 <c>IUnitDirectory</c> 로 등록한다.</para>
/// </remarks>
public sealed class UnitDirectory : IUnitDirectory, IHandle<UnitTopologyChangedMessage>, IDisposable
{
    #region - Ctors -
    /// <param name="api">부대 편제 창구(8.0+ 판정 포함). <b>선택 주입</b> — 없으면 <see cref="IsAvailable"/> = <c>false</c>.</param>
    /// <param name="events">있으면 <see cref="UnitTopologyChangedMessage"/> 를 구독한다(<see cref="Dispose"/> 때 해제).</param>
    /// <param name="log">진단 로그.</param>
    /// <param name="delay">합침 창의 지연(시험용 — sleep 없이 창 끝을 정한다).</param>
    /// <param name="dispatch"><see cref="Changed"/> 를 발화할 곳(시험용). 기본은 WPF 디스패처(없으면 그 자리).</param>
    public UnitDirectory(
        IUnitGraphApi? api,
        IEventAggregator? events = null,
        ILogService? log = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        System.Action<System.Action>? dispatch = null)
    {
        _api = api;
        _events = events;
        _log = log;
        _dispatch = dispatch ?? DispatchToUi;
        _reload = new CoalescingTrigger(ReloadAsync, delay: delay,
            onError: ex => _log?.Warning($"[{nameof(UnitDirectory)}] 편제 다시 읽기 실패: {ex.Message}"));
        _events?.SubscribeOnPublishedThread(this);
    }
    #endregion

    #region - IUnitDirectory -
    /// <summary>서버가 부대 편제를 갖는가(8.0+ — 판정은 <see cref="IUnitGraphApi.IsAvailable"/>).</summary>
    public bool IsAvailable => _api?.IsAvailable == true;

    public event EventHandler? Changed;

    public string? Describe(int unitId)
    {
        var tree = _tree;
        var node = tree?.Find(unitId);
        if (tree is null || node is null) return null;

        var path = new List<string>();
        var cursor = node;
        var guard = 0;
        while (cursor.ParentId is int parentId && tree.Find(parentId) is { } parent && guard++ < tree.Count)
        {
            path.Add(parent.Name);
            cursor = parent;
        }
        return path.Count == 0 ? node.Name : $"{node.Name} · {string.Join(" › ", path)}";
    }
    #endregion

    #region - Processes -
    /// <summary>
    /// 편제를 읽어 캐시한다 — 이미 읽었으면 나가지 않는다(<paramref name="force"/> 면 다시). 동시에 불러도 한 번만 나간다.
    /// 실패해도 던지지 않고 옛 캐시를 둔다.
    /// </summary>
    public async Task EnsureLoadedAsync(bool force = false, CancellationToken token = default)
        => await LoadAsync(force, token).ConfigureAwait(false);

    /// <summary><see cref="IUnitDirectory"/> 의 적재 입구 — 아직 읽지 않았을 때만 나간다.</summary>
    Task IUnitDirectory.EnsureLoadedAsync(CancellationToken token) => EnsureLoadedAsync(force: false, token);

    /// <summary>편제가 다른 곳에서 바뀌었다 — 합침 창을 연다(곧바로 돌아온다).</summary>
    public Task HandleAsync(UnitTopologyChangedMessage message, CancellationToken cancellationToken)
    {
        if (_disposed || _tree is null) return Task.CompletedTask;    // 아무도 쓰지 않은 사전을 위해 서버를 부르지 않는다
        _pendingReload = _reload.Pulse();
        return Task.CompletedTask;
    }

    /// <summary>가장 최근 합침 재조회(시험용).</summary>
    internal Task PendingReload => _pendingReload;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _events?.Unsubscribe(this);
        _reload.Cancel();
    }
    #endregion

    #region - Helpers -
    private async Task ReloadAsync(CancellationToken token)
    {
        if (await LoadAsync(force: true, token).ConfigureAwait(false))
            _dispatch(() => Changed?.Invoke(this, EventArgs.Empty));       // 잠금 밖에서 발화
    }

    /// <returns>새로 읽어 캐시를 바꿨으면 <c>true</c>.</returns>
    private async Task<bool> LoadAsync(bool force, CancellationToken token)
    {
        if (!IsAvailable || _api is null) return false;
        if (_tree is not null && !force) return false;

        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_tree is not null && !force) return false;

            var response = await _api.GetGraphAsync(token).ConfigureAwait(false);
            if (!response.Success || response.Data is null)
            {
                _log?.Warning($"[{nameof(UnitDirectory)}] 부대 편제를 읽지 못했습니다 — 이전 이름을 그대로 씁니다: {response.Error?.Message}");
                return false;
            }

            _tree = UnitTreeBuilder.Build(response.Data);
            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex)
        {
            _log?.Warning($"[{nameof(UnitDirectory)}] 부대 편제 읽기 예외: {ex.Message}");
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static void DispatchToUi(System.Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else _ = dispatcher.InvokeAsync(action);
    }
    #endregion

    #region - Attributes -
    private readonly IUnitGraphApi? _api;
    private readonly IEventAggregator? _events;
    private readonly ILogService? _log;
    private readonly System.Action<System.Action> _dispatch;
    private readonly CoalescingTrigger _reload;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile UnitTreeModel? _tree;
    private volatile Task _pendingReload = Task.CompletedTask;
    private volatile bool _disposed;
    #endregion
}
