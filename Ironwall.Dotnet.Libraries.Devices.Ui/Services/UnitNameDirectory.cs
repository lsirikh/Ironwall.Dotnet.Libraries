using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Services;
/****************************************************************************
   Purpose      : unit_id → 부대 이름 읽기 전용 사전 — 장비 목록·상세의 "소속 부대" 칸이 쓴다(D-14)
   Created By   : GHLee
   Created On   : 9/23/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// <c>unit_id</c>(서버 8.0+) → 부대 이름을 1회 전체 적재해 캐시하는 <b>읽기 전용</b> 사전.
/// </summary>
/// <remarks>
/// <para><b>왜 <see cref="IUnitScopeService"/> 와 다른가</b> — 그쪽은 "이 클라이언트가 속한 부대" 딱 하나만
/// 코드→id 로 해석해 쓰기(<c>unit_id</c> 주입)에 쓴다. 여기는 <b>서버에 있는 모든 부대</b>의 id→이름이 필요하다
/// (장비는 이 클라이언트가 아닌 다른 부대에도 배정될 수 있다 — 목록·상세는 그 값을 그대로 보여줘야 한다).
/// 쓰기 경로(<c>Helpers/UnitScopeGate.cs</c>)는 건드리지 않는다 — 이 클래스는 표시 전용이다.</para>
/// <para><b>왜 <see cref="IService"/> 가 아닌가</b> — <c>CatalogService</c> 와 같은 이유다: 부팅 때 읽지 않고
/// 화면이 처음 필요로 할 때 1회 읽는다(로그인 전에는 토큰이 없다). 8.0 미만이면 애초에 서버를 부르지 않는다.</para>
/// </remarks>
public sealed class UnitNameDirectory : IUnitTopologyCache
{
    #region - Ctors -
    /// <param name="api">부대 편제 창구. <b>선택 주입</b> — 없으면 해석을 포기한다.</param>
    /// <param name="probe">서버 계약 세대 프로브. <b>선택 주입</b> — 없으면 6.3 으로 간주해 서버를 부르지 않는다.</param>
    /// <param name="log">진단 로그(선택).</param>
    /// <param name="delay">무효화 알림을 합치는 창의 지연(시험용). 생략하면 실제 시간.</param>
    public UnitNameDirectory(IUnitGraphApi? api = null, IServerContractProbe? probe = null, ILogService? log = null,
                             Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _api = api;
        _probe = probe;
        _log = log;
        // SYNC_UNIT 는 몰려온다 — 창 안의 무효화를 다시 읽기 한 번으로 합친다.
        _reload = new CoalescingTrigger(token => EnsureLoadedAsync(token), delay: delay,
                                        onError: ex => _log?.Warning($"[{nameof(UnitNameDirectory)}] 다시 읽기 실패: {ex.Message}"));
    }
    #endregion

    #region - Properties -
    /// <summary>서버가 부대 편제 축을 갖는가(8.0+). ⚠ 비교는 <c>&gt;=</c> — 9.0 이 와도 유지된다.</summary>
    public bool IsUnitEra => (_probe?.Contract ?? EnumServerContract.V6_3) >= EnumServerContract.V8_0;
    #endregion

    #region - Processes -
    /// <summary>캐시에서 즉시 읽는다 — 없으면 <c>null</c>(호출부가 폴백을 결정한다). 네트워크에 나가지 않는다.</summary>
    public string? TryGetName(int unitId) => _names.TryGetValue(unitId, out var name) ? name : null;

    /// <summary>
    /// 캐시한 부대 전체(편제 순서) — 상세의 "소속 부대" 선택지가 쓴다. 아직 못 읽었으면 빈 목록(네트워크에 나가지 않는다).
    /// </summary>
    public IReadOnlyList<(int Id, string Name)> Snapshot() => _ordered;

    /// <summary>
    /// <paramref name="unitId"/> 를 이름으로 푼다 — 배정 없음은 "미배치", 캐시에 있으면 이름,
    /// 없고 아직 못 채웠으면 <b>원값 id 문자열</b>을 우선 돌려주고 배경에서 채운 뒤 <paramref name="onResolved"/> 를 부른다.
    /// </summary>
    /// <remarks>이름을 지어내지 않는다 — 못 찾으면 항상 원값(id) 아니면 "미배치"다.</remarks>
    public string Display(int? unitId, Action? onResolved = null)
    {
        if (unitId is not { } id) return Unassigned;

        var cached = TryGetName(id);
        if (cached != null) return cached;

        if (onResolved != null)
        {
            _ = EnsureLoadedAsync().ContinueWith(
                _ => Caliburn.Micro.Execute.OnUIThread(onResolved),
                TaskScheduler.Default);
        }
        return id.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 부대 전체를 1회 읽어 캐시한다(멱등 — 이미 채웠으면 다시 나가지 않는다).
    /// 실패해도 예외를 던지지 않는다 — 스로틀(<see cref="RETRY_INTERVAL_MS"/>) 뒤 다음 호출에서 다시 시도한다.
    /// </summary>
    public async Task EnsureLoadedAsync(CancellationToken token = default)
    {
        if (!IsUnitEra || _api is null || _loaded) return;
        if (_lastAttemptTick != 0 && Environment.TickCount64 - _lastAttemptTick < RETRY_INTERVAL_MS) return;

        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_loaded) return;
            if (_lastAttemptTick != 0 && Environment.TickCount64 - _lastAttemptTick < RETRY_INTERVAL_MS) return;
            _lastAttemptTick = Environment.TickCount64;

            var response = await _api.GetGraphAsync(token).ConfigureAwait(false);
            if (!response.Success || response.Data == null)
            {
                _log?.Warning($"[{nameof(UnitNameDirectory)}] 부대 목록을 읽지 못해 이름을 채우지 못했습니다 — 목록·상세에는 id 가 대신 보입니다.");
                return;
            }

            var tree = UnitTreeBuilder.Build(response.Data);
            // 이름이 빈 노드는 캐시에 넣지 않는다 — TryGetName 이 null 을 돌려줘야 호출부가 id 로 폴백한다
            // ("이름을 지어내지 않는다"의 반대쪽: 빈 문자열을 이름인 양 보이지도 않는다).
            var ordered = new List<(int Id, string Name)>();
            foreach (var node in tree.Ordered)
            {
                if (string.IsNullOrWhiteSpace(node.Name)) continue;
                _names[node.Id] = node.Name;
                ordered.Add((node.Id, node.Name));
            }
            // 다른 곳에서 지워진 부대는 사전에서도 뺀다 — 남겨 두면 없는 부대의 이름이 계속 보인다.
            var live = new HashSet<int>(ordered.Select(o => o.Id));
            foreach (var gone in _names.Keys.Where(k => !live.Contains(k)).ToList()) _names.TryRemove(gone, out _);
            _ordered = ordered;
            _loaded = true;
        }
        catch (OperationCanceledException)
        {
            // 종료 중 — 무시
        }
        catch (Exception ex)
        {
            _log?.Warning($"[{nameof(UnitNameDirectory)}] 부대 이름 적재 실패: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 다른 곳에서 편제가 바뀌었다(서버 <c>SYNC_UNIT</c>) — 부대 이름 · 목록을 다시 읽는다.
    /// </summary>
    /// <remarks>
    /// 이미 한 번 채운 적이 있을 때만 배경에서 다시 읽는다(창 500 ms 로 합친다) — 아무도 쓰지 않은 사전을 위해 서버를 부르지 않는다.
    /// 다시 읽는 동안에도 옛 이름은 그대로 보인다(빈 칸 · id 로 깜빡이지 않는다).
    /// </remarks>
    public void Invalidate()
    {
        _lastAttemptTick = 0;              // 재시도 스로틀도 풀어 준다 — 서버가 바뀌었다고 알려 준 참이다
        if (!_loaded) return;
        _loaded = false;
        _pendingReload = _reload.Pulse();
    }

    /// <summary>가장 최근 배경 다시 읽기(시험용).</summary>
    internal Task PendingReload => _pendingReload;
    #endregion

    #region - Attributes -
    /// <summary>부대가 배정되지 않은 장비의 표시 문구.</summary>
    public const string Unassigned = "미배치";

    /// <summary>해석 실패 후 재시도 최소 간격(ms) — 그리드 한 화면에 수백 행이 있어도 서버를 수백 번 부르지 않는다.</summary>
    private const long RETRY_INTERVAL_MS = 60_000;

    private readonly IUnitGraphApi? _api;
    private readonly IServerContractProbe? _probe;
    private readonly ILogService? _log;
    private readonly ConcurrentDictionary<int, string> _names = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile bool _loaded;
    private volatile IReadOnlyList<(int Id, string Name)> _ordered = Array.Empty<(int, string)>();
    private long _lastAttemptTick;
    private readonly CoalescingTrigger _reload;
    private Task _pendingReload = Task.CompletedTask;
    #endregion
}
