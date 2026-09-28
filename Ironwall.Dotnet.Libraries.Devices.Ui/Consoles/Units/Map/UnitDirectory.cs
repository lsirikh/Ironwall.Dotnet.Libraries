using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Devices.Units;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;

/****************************************************************************
   Purpose      : IUnitDirectory 구현 — 이름 사전(UnitNameDirectory)과 같은 편제 캐시를 감싼다 (FR-46 · FR-48 · ISSUE-46)
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
/// <para><b>이름 사전 한 벌</b>(v1.3 ISSUE-46) — 따로 캐시하지 않는다. 장비 목록 · 상세의 "소속 부대" 칸이 쓰는 <see cref="UnitNameDirectory"/> 의
/// 편제 트리(<see cref="UnitNameDirectory.Tree"/>)를 그대로 읽는다. 그래서 두 곳이 <b>같은 캐시 · 같은 무효화 시점</b>을 쓴다 — 호스트가 SYNC_UNIT 에
/// <see cref="UnitNameDirectory.Invalidate"/> 를 부르면 이 사전도 같이 새로워지고, 한쪽이 새 이름인데 다른 쪽이 옛 이름인 순간이 없다.</para>
/// <para><b>갱신</b>: 이름 사전이 다시 읽을 때마다(<see cref="UnitNameDirectory.Changed"/>) <see cref="Changed"/> 를 한 번 발화한다(배송 = 기본 WPF 디스패처).
/// <see cref="UnitTopologyChangedMessage"/> 도 구독해 이름 사전을 무효화한다 — 호스트의 무효화와 같은 창(500 ms)에 합쳐져 재조회는 한 번이다.
/// 아무도 읽지 않은 사전은 알림에 서버를 부르지 않는다(이름 사전의 규칙).</para>
/// <para><b>스레드</b>: 알림 처리기는 곧바로 돌아온다. 트리는 불변 인스턴스를 통째로 바꿔 끼우므로 읽는 쪽에 잠금이 없다.</para>
/// <para><b>DI</b>: <c>IUnitDirectory</c> <b>로만</b> 등록한다(<c>IUnitTopologyCache</c> 로 등록하면 같은 알림에 이중 재조회) — 레인 A 몫.</para>
/// </remarks>
public sealed class UnitDirectory : IUnitDirectory, IHandle<UnitTopologyChangedMessage>, IDisposable
{
    #region - Ctors -
    /// <param name="names">장비 목록 · 상세와 함께 쓰는 이름 사전(싱글턴).</param>
    /// <param name="events">있으면 <see cref="UnitTopologyChangedMessage"/> 를 구독한다(<see cref="Dispose"/> 때 해제).</param>
    /// <param name="log">진단 로그.</param>
    /// <param name="dispatch"><see cref="Changed"/> 를 발화할 곳(시험용). 기본은 WPF 디스패처(없으면 그 자리).</param>
    public UnitDirectory(UnitNameDirectory names, IEventAggregator? events = null, ILogService? log = null, System.Action<System.Action>? dispatch = null)
    {
        _names = names ?? throw new ArgumentNullException(nameof(names));
        _events = events;
        _log = log;
        _dispatch = dispatch ?? DispatchToUi;
        _names.Changed += OnNamesChanged;
        _events?.SubscribeOnPublishedThread(this);
    }
    #endregion

    #region - IUnitDirectory -
    /// <summary>서버가 부대 편제를 갖고(8.0+) 편제 창구가 있다.</summary>
    public bool IsAvailable => _names.IsAvailable;

    public event EventHandler? Changed;

    public string? Describe(int unitId)
    {
        var tree = _names.Tree;
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

    /// <summary>이름 사전이 아직 읽지 않았으면 읽게 한다(이미 읽었으면 나가지 않는다 · 실패해도 던지지 않는다).</summary>
    public Task EnsureLoadedAsync(CancellationToken token = default) => _names.EnsureLoadedAsync(token);
    #endregion

    #region - IHandle -
    /// <summary>편제가 다른 곳에서 바뀌었다 — 이름 사전을 무효화한다(합침 창을 열고 곧바로 돌아온다).</summary>
    public Task HandleAsync(UnitTopologyChangedMessage message, CancellationToken cancellationToken)
    {
        if (!_disposed) _names.Invalidate();
        return Task.CompletedTask;
    }
    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _events?.Unsubscribe(this);
        _names.Changed -= OnNamesChanged;
    }

    #region - Helpers -
    private void OnNamesChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;
        try { _dispatch(() => Changed?.Invoke(this, EventArgs.Empty)); }
        catch (Exception ex) { _log?.Warning($"[{nameof(UnitDirectory)}] 변경 알림 실패: {ex.Message}"); }
    }

    private static void DispatchToUi(System.Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else _ = dispatcher.InvokeAsync(action);
    }
    #endregion

    #region - Attributes -
    private readonly UnitNameDirectory _names;
    private readonly IEventAggregator? _events;
    private readonly ILogService? _log;
    private readonly System.Action<System.Action> _dispatch;
    private volatile bool _disposed;
    #endregion
}
