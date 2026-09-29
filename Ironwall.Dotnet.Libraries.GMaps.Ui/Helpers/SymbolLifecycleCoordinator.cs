using System.Collections.Concurrent;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/// <summary>한 번의 전량 재등록 뒤 재계산 결과(로그 · 시험용).</summary>
/// <param name="Devices">큐 상태로 다시 칠한 장비 심볼 수.</param>
/// <param name="Groups">큐 상태로 다시 칠한 구역선 수.</param>
/// <param name="Unlinked">연결 장비가 없어 Normal 로 되돌린 심볼 수.</param>
/// <param name="Changed">색이 실제로 바뀐 심볼 수(영속된 옛 값 · 재등록 중 유실된 전이를 바로잡은 것).</param>
public readonly record struct SymbolReconcileResult(int Devices, int Groups, int Unlinked, int Changed);

/// <summary>
/// 지도 심볼 ↔ 이벤트 조회표(<see cref="SymbolEventManager"/>)의 <b>등록 수명</b>을 지도 쪽에서 관리한다.
/// </summary>
/// <remarks>
/// <para><b>왜 지도 쪽인가</b> — 조회표를 비우고 다시 채우는 주체가 지도(<c>MapViewModel.InitializeDeviceSymbolIntegration</c>)다.
/// 그런데 조회표 자체는 비운 뒤 다시 채울 때 "지금 큐에 무엇이 살아 있는가"를 다시 묻지 않았다. 이 클래스가 그 빈칸을 맡는다.</para>
/// <list type="bullet">
/// <item><b>부팅 영속 깜빡임(A1)</b> — 심볼 <c>EventStatus</c> 는 PidsSymbols 테이블에 영속된다. 부팅 자가치유는 ACTIVATED 장비만
/// 큐로 재계산했으므로, ERROR · DEACTIVATED 장비에 남은 <c>Detecting</c> 은 재시작 뒤에도 영원히 펄스했다.
/// → 재등록이 끝나면 <b>모든</b> 장비 · 구역선을 큐(<see cref="IEventQueueManager"/>) 상태로 다시 칠하고,
/// 연결 장비가 없는 심볼은 Normal 로 되돌린다. 영속값은 실시간 경보의 근거가 되지 않는다.</item>
/// <item><b>재등록 유실(A2)</b> — 전량 재등록은 조회표를 먼저 비운다. 그 사이에 온 큐 전이는 "미등록"으로 버려진다.
/// 재등록 뒤 큐 상태로 다시 칠하면 버려진 전이도 복원된다(큐가 진실).</item>
/// <item><b>장비 삭제(A3)</b> — 조회표에는 장비 해제가 없다. 삭제된 장비의 심볼이 마지막 색으로 굳고, 같은 키로 오는 늦은 전이가
/// 계속 그 심볼을 칠한다. → <see cref="UnregisterDevice"/> 가 조회표에서 그 항목을 빼고(<c>SymbolEventManager.UnregisterDeviceSymbol</c>) 심볼 색을 Normal 로 돌린다.</item>
/// <item><b>무음 누락(A5)</b> — 큐 전이가 미등록 장비로 오면 조회표는 로그 없이 버린다. <see cref="ObserveDeviceTransition"/> 이
/// 같은 전이를 곁에서 보고 장비당 한 번 기록한다.</item>
/// </list>
/// <para>스레드: 등록 · 재계산 · 해제는 UI 스레드(지도 마커를 만진다). <see cref="ObserveDeviceTransition"/> 은 큐 콜백 스레드(NATS)에서
/// 온다 — 내부 표는 전부 동시성 사전이고, 심볼 갱신 통지는 마커가 UI 스레드로 넘긴다(<c>GMapPidsMarker</c>).</para>
/// </remarks>
public sealed class SymbolLifecycleCoordinator
{
    private readonly SymbolEventManager _symbolEventManager;
    private readonly IEventQueueManager? _eventQueue;
    private readonly ILogService? _log;

    private readonly ConcurrentDictionary<(int Id, EnumDeviceType Type), IPidsEventCapable> _devices = new();
    private readonly ConcurrentDictionary<int, int> _deviceIdRefCount = new();
    private readonly ConcurrentDictionary<int, IPidsEventCapable> _groups = new();
    private readonly ConcurrentDictionary<(int Id, EnumDeviceType Type), byte> _unmappedReported = new();

    public SymbolLifecycleCoordinator(SymbolEventManager symbolEventManager, IEventQueueManager? eventQueue, ILogService? log)
    {
        _symbolEventManager = symbolEventManager ?? throw new ArgumentNullException(nameof(symbolEventManager));
        _eventQueue = eventQueue;
        _log = log;
    }

    /// <summary>등록된 장비 심볼 수(시험 · 로그용).</summary>
    public int DeviceCount => _devices.Count;

    /// <summary>(Id, 종류) 또는 Id 만으로 등록돼 있는가 — 조회표의 Id 폴백 규칙과 같다.</summary>
    public bool IsRegistered(int deviceId, EnumDeviceType deviceType)
        => _devices.ContainsKey((deviceId, deviceType)) || (_deviceIdRefCount.TryGetValue(deviceId, out var n) && n > 0);

    /// <summary>전량 재등록 시작 — 조회표와 이 표를 함께 비운다.</summary>
    public void BeginRebuild()
    {
        _symbolEventManager.Dispose();
        _devices.Clear();
        _deviceIdRefCount.Clear();
        _groups.Clear();
    }

    /// <summary>장비 심볼 등록 — 조회표 등록 + 이 표 기록. 색은 여기서 칠하지 않는다(<see cref="CompleteRebuild"/> · <see cref="ReconcileDevice"/>).</summary>
    public void RegisterDevice(IBaseDeviceModel device, IPidsEventCapable symbol)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(symbol);
        _symbolEventManager.RegisterDeviceSymbol(device, symbol);
        var key = (device.Id, device.DeviceType);
        if (_devices.TryGetValue(key, out var previous) && ReferenceEquals(previous, symbol)) return;
        if (_devices.TryAdd(key, symbol)) _deviceIdRefCount.AddOrUpdate(device.Id, 1, (_, n) => n + 1);
        else _devices[key] = symbol;
        _unmappedReported.TryRemove(key, out _);
    }

    /// <summary>구역선 등록 — 조회표 등록 + 이 표 기록.</summary>
    public void RegisterGroup(int groupId, IBaseDeviceModel representative, IPidsEventCapable symbol)
    {
        _symbolEventManager.RegisterGroupSymbol(groupId, representative, symbol);
        _groups[groupId] = symbol;
    }

    /// <summary>
    /// 전량 재등록 마무리 — 등록된 모든 장비 · 구역선을 큐 상태로 다시 칠하고, 연결 장비가 없는 심볼은 Normal 로 되돌린다.
    /// </summary>
    /// <param name="unlinkedSymbols">지도에 있으나 이번 재등록에서 장비와 짝지어지지 않은 장비 심볼.</param>
    public SymbolReconcileResult CompleteRebuild(IEnumerable<IPidsEventCapable>? unlinkedSymbols = null)
    {
        int devices = 0, groups = 0, unlinked = 0, changed = 0;

        foreach (var ((id, type), symbol) in _devices)
        {
            devices++;
            if (Apply(symbol, _eventQueue?.GetDeviceState(id, type) ?? EnumCompositeEventStatus.Normal)) changed++;
        }

        foreach (var (groupId, symbol) in _groups)
        {
            groups++;
            if (Apply(symbol, _eventQueue?.GetGroupState(groupId) ?? EnumCompositeEventStatus.Normal)) changed++;
        }

        if (unlinkedSymbols != null)
        {
            foreach (var symbol in unlinkedSymbols)
            {
                if (symbol is null || _devices.Values.Any(s => ReferenceEquals(s, symbol))) continue;
                unlinked++;
                if (Apply(symbol, EnumCompositeEventStatus.Normal)) changed++;
            }
        }

        var result = new SymbolReconcileResult(devices, groups, unlinked, changed);
        _log?.Info($"[심볼 재계산] 큐 기준 재칠 — 장비 {devices} · 구역선 {groups} · 미연결 {unlinked} (색 변경 {changed}, 큐 {(_eventQueue == null ? "없음→Normal" : "있음")})");
        return result;
    }

    /// <summary>장비 한 대를 큐 상태로 다시 칠한다(단건 재등록 경로). 등록돼 있지 않으면 false.</summary>
    public bool ReconcileDevice(int deviceId, EnumDeviceType deviceType)
    {
        if (!_devices.TryGetValue((deviceId, deviceType), out var symbol)) return false;
        Apply(symbol, _eventQueue?.GetDeviceState(deviceId, deviceType) ?? EnumCompositeEventStatus.Normal);
        return true;
    }

    /// <summary>
    /// 장비 삭제 — 조회표에서 이 장비를 끊고(흡수용 빈 모델로 교체) 지도 심볼 색을 Normal 로 되돌린다.
    /// </summary>
    /// <returns>끊어 낸 지도 심볼. 등록돼 있지 않았으면 null.</returns>
    public IPidsEventCapable? UnregisterDevice(int deviceId, EnumDeviceType deviceType)
    {
        if (!_devices.TryRemove((deviceId, deviceType), out var symbol)) return null;
        _deviceIdRefCount.AddOrUpdate(deviceId, 0, (_, n) => Math.Max(0, n - 1));

        // 조회표에서 이 장비를 뺀다(Events.Ui UnregisterDeviceSymbol, WP-1 ㉒) — 종전 임시 방식(같은 키로 흡수용 빈 모델 등록)을 대신한다.
        //   이후 이 키로 오는 늦은 전이 · 문 상태는 어떤 심볼도 칠하지 않고 조회표가 장비당 한 번 경고한다.
        _symbolEventManager.UnregisterDeviceSymbol(deviceId, deviceType);

        Apply(symbol, EnumCompositeEventStatus.Normal);
        _log?.Info($"[심볼 해제] Device({deviceId},{deviceType}) 삭제 — 조회표에서 분리 · 색 Normal 복원: '{symbol.Title}'");
        return symbol;
    }

    /// <summary>
    /// 큐 전이 관찰(<see cref="IEventQueueManager.OnDeviceStateChanged"/> 구독) — 지도에 등록되지 않은 장비의 전이를 <b>장비당 한 번</b> 기록한다.
    /// </summary>
    /// <returns>이번 호출에서 기록했으면 true.</returns>
    public bool ObserveDeviceTransition(int deviceId, EnumDeviceType deviceType, EnumCompositeEventStatus previous, EnumCompositeEventStatus next)
    {
        if (IsRegistered(deviceId, deviceType)) return false;
        if (!_unmappedReported.TryAdd((deviceId, deviceType), 0)) return false;
        _log?.Info($"[심볼 미등록] Device({deviceId},{deviceType}) {previous}→{next} 전이가 지도에 반영되지 않음 — 심볼 미배치/장비 미연결(같은 장비는 한 번만 기록)");
        return true;
    }

    /// <summary>색 축을 세팅하고 갱신을 통지한다. 실제로 바뀌었으면 true.</summary>
    private static bool Apply(IPidsEventCapable symbol, EnumCompositeEventStatus state)
    {
        var before = symbol.EventStatus;
        symbol.CompositeStatus = state;     // setter 가 EventStatus 를 함께 맞춘다(SSOT)
        symbol.SetUpdate();                 // 지도 마커가 UI 스레드로 넘겨 통지한다
        return before != symbol.EventStatus;
    }
}
