using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using System.Diagnostics;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Managers;
/****************************************************************************
   Purpose      : UUID 기반 글로벌 이벤트 큐 + 그룹 역인덱스 + 공유 타이머
   Created By   : GHLee
   Created On   : 2026-03-08
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class EventQueueManager : IEventQueueManager, IDisposable
{
    #region - Ctors -
    public EventQueueManager(ILogService? log = null, IEventSetupModel? eventSetupModel = null)
    {
        _log = log;
        _eventSetupModel = eventSetupModel;
        _entries = new Dictionary<string, EventEntry>();
        _groupIndex = new Dictionary<int, HashSet<string>>();
        _deviceIndex = new Dictionary<(int Id, EnumDeviceType Type), HashSet<string>>();
    }
    #endregion

    #region - IEventQueueManager -
    public string Enqueue(EventEntry entry, string? externalEntryId = null)
    {
        entry.EntryId = !string.IsNullOrWhiteSpace(externalEntryId)
            ? externalEntryId
            : Guid.NewGuid().ToString();
        entry.EnqueuedAt = DateTime.Now;

        bool isFirstDeviceEvent = false;
        var groupFirstEvents = new List<(int GroupId, EnumEventType EventType)>();
        var groupTransitions = new List<(int GroupId, EnumCompositeEventStatus Prev, EnumCompositeEventStatus Next)>();
        var autoRecoveryIds = new List<string>();
        var devicePrev = EnumCompositeEventStatus.Normal;
        var deviceNext = EnumCompositeEventStatus.Normal;
        bool deviceStateChanged = false;

        lock (_gate)
        {
            var deviceKey = (entry.DeviceId, entry.DeviceType);

            // 1. 영향 그룹 ID 수집 + 자동복구 Fault 엔트리 수집 (scratch 재사용)
            _scratchAffectedGroupIds.Clear();
            if (entry.GroupIds != null)
                foreach (var gid in entry.GroupIds)
                    _scratchAffectedGroupIds.Add(gid);

            _scratchFaultIds.Clear();
            if (entry.EventType == EnumEventType.Intrusion
                && _deviceIndex.TryGetValue(deviceKey, out var existingDevIds))
            {
                foreach (var id in existingDevIds)
                {
                    if (_entries.TryGetValue(id, out var fe) && fe.EventType == EnumEventType.Fault)
                    {
                        _scratchFaultIds.Add(id);
                        if (fe.GroupIds != null)
                            foreach (var gid in fe.GroupIds)
                                _scratchAffectedGroupIds.Add(gid);
                    }
                }
            }

            // 2. prev 상태 스냅샷 (모든 조작 전, scratch 재사용)
            _scratchPrevGroupStates.Clear();
            foreach (var gid in _scratchAffectedGroupIds)
                _scratchPrevGroupStates[gid] = ComputeGroupState(gid);
            devicePrev = ComputeDeviceState(deviceKey);

            // 3. Fault 엔트리 원자 제거 (자동복구)
            foreach (var faultId in _scratchFaultIds)
            {
                if (_entries.TryGetValue(faultId, out var faultEntry))
                {
                    if (faultEntry.GroupIds != null)
                    {
                        foreach (var gid in faultEntry.GroupIds)
                        {
                            if (_groupIndex.TryGetValue(gid, out var gEntryIds))
                            {
                                gEntryIds.Remove(faultId);
                                if (gEntryIds.Count == 0)
                                    _groupIndex.Remove(gid);
                            }
                        }
                    }
                    _entries.Remove(faultId);
                }
                if (_deviceIndex.TryGetValue(deviceKey, out var devIds))
                {
                    devIds.Remove(faultId);
                    if (devIds.Count == 0)
                        _deviceIndex.Remove(deviceKey);
                }
                autoRecoveryIds.Add(faultId);
            }

            // 4. 글로벌 저장소 등록
            _entries[entry.EntryId] = entry;

            // 5. 그룹 역인덱스 등록 (TryGetValue 단일 조회)
            if (entry.GroupIds != null)
            {
                foreach (var groupId in entry.GroupIds)
                {
                    if (!_groupIndex.TryGetValue(groupId, out var groupEntrySet))
                    {
                        groupEntrySet = new HashSet<string>();
                        _groupIndex[groupId] = groupEntrySet;
                    }
                    var wasEmpty = groupEntrySet.Count == 0;
                    groupEntrySet.Add(entry.EntryId);
                    if (wasEmpty)
                        groupFirstEvents.Add((groupId, entry.EventType));
                }
            }

            // 6. 개별 심볼 역인덱스 등록 (TryGetValue 단일 조회)
            if (!_deviceIndex.TryGetValue(deviceKey, out var deviceEntrySet))
            {
                deviceEntrySet = new HashSet<string>();
                _deviceIndex[deviceKey] = deviceEntrySet;
            }
            isFirstDeviceEvent = deviceEntrySet.Count == 0;
            deviceEntrySet.Add(entry.EntryId);

            // 7. next 상태 계산 (모든 등록 후)
            foreach (var gid in _scratchAffectedGroupIds)
            {
                var prev = _scratchPrevGroupStates[gid];
                var next = ComputeGroupState(gid);
                if (prev != next)
                    groupTransitions.Add((gid, prev, next));
            }

            deviceNext = ComputeDeviceState(deviceKey);
            deviceStateChanged = devicePrev != deviceNext;
        }

        // 이벤트 발화는 lock 밖에서 (데드락 방지)
        var onAutoRecovery = OnAutoRecovery;
        var onGroupFirst = OnGroupFirstEvent;
        var onGroupStateChanged = OnGroupStateChanged;
        var onDeviceStateChanged = OnDeviceStateChanged;
        var onDeviceFirst = OnDeviceFirstEvent;
        var onAnyEnqueue = OnAnyEnqueue;

        foreach (var faultId in autoRecoveryIds)
            onAutoRecovery?.Invoke(faultId);

        foreach (var (groupId, eventType) in groupFirstEvents)
            onGroupFirst?.Invoke(groupId, eventType);

        foreach (var (groupId, prev, next) in groupTransitions)
            onGroupStateChanged?.Invoke(groupId, prev, next);

        if (deviceStateChanged)
            onDeviceStateChanged?.Invoke(entry.DeviceId, entry.DeviceType, devicePrev, deviceNext);

        if (isFirstDeviceEvent)
            onDeviceFirst?.Invoke(entry.DeviceId, entry.DeviceType, entry.EventType);

        onAnyEnqueue?.Invoke(entry.EventType);
        RaiseActiveCountChanged();   // 인디케이터 라이브 갱신(GMap_Map_Instruments)

        return entry.EntryId;
    }

    public void Dequeue(string entryId)
    {
        var groupEmptyEvents = new List<int>();
        var groupTransitions = new List<(int GroupId, EnumCompositeEventStatus Prev, EnumCompositeEventStatus Next)>();
        (int DeviceId, EnumDeviceType DeviceType) deviceKey = default;
        bool isDeviceEmpty = false;
        var devicePrev = EnumCompositeEventStatus.Normal;
        var deviceNext = EnumCompositeEventStatus.Normal;
        bool deviceStateChanged = false;

        lock (_gate)
        {
            if (!_entries.TryGetValue(entryId, out var entry)) return;

            deviceKey = (entry.DeviceId, entry.DeviceType);

            // 1. prev 상태 스냅샷 (제거 전) — scratch 재사용 (_gate 보호 범위 내)
            Debug.Assert(Monitor.IsEntered(_gate));
            _scratchPrevGroupStates.Clear();
            if (entry.GroupIds != null)
                foreach (var groupId in entry.GroupIds)
                    _scratchPrevGroupStates[groupId] = ComputeGroupState(groupId);
            devicePrev = ComputeDeviceState(deviceKey);

            // 2. 글로벌 저장소에서 제거
            _entries.Remove(entryId);

            // 3. 그룹 역인덱스에서 제거 + 전이 판단
            if (entry.GroupIds != null)
            {
                foreach (var groupId in entry.GroupIds)
                {
                    if (_groupIndex.TryGetValue(groupId, out var entryIds))
                    {
                        entryIds.Remove(entryId);
                        var next = ComputeGroupState(groupId);

                        if (entryIds.Count == 0)
                        {
                            groupEmptyEvents.Add(groupId);
                            _groupIndex.Remove(groupId);
                        }

                        if (_scratchPrevGroupStates.TryGetValue(groupId, out var prev) && prev != next)
                            groupTransitions.Add((groupId, prev, next));
                    }
                }
            }

            // 4. 개별 심볼 역인덱스에서 제거
            if (_deviceIndex.TryGetValue(deviceKey, out var deviceEntryIds))
            {
                deviceEntryIds.Remove(entryId);
                if (deviceEntryIds.Count == 0)
                {
                    isDeviceEmpty = true;
                    _deviceIndex.Remove(deviceKey);
                }
            }

            deviceNext = ComputeDeviceState(deviceKey);
            deviceStateChanged = devicePrev != deviceNext;
        }

        // 이벤트 발화는 lock 밖에서
        var onGroupEmpty = OnGroupEmpty;
        var onGroupStateChanged = OnGroupStateChanged;
        var onDeviceStateChanged = OnDeviceStateChanged;
        var onDeviceEmpty = OnDeviceEmpty;

        foreach (var groupId in groupEmptyEvents)
            onGroupEmpty?.Invoke(groupId);

        foreach (var (groupId, prev, next) in groupTransitions)
            onGroupStateChanged?.Invoke(groupId, prev, next);

        if (deviceStateChanged)
            onDeviceStateChanged?.Invoke(deviceKey.DeviceId, deviceKey.DeviceType, devicePrev, deviceNext);

        if (isDeviceEmpty)
            onDeviceEmpty?.Invoke(deviceKey.DeviceId, deviceKey.DeviceType);

        RaiseActiveCountChanged();   // 동일-상태 dequeue도 카운트 반영(GMap_Map_Instruments)
    }

    /// <summary>
    /// (EB3) 특정 장비에 속한 모든 이벤트 엔트리를 제거한다. 장비 삭제(NATS DELETED) 시 호출하면
    /// 고아 이벤트 카드가 남지 않는다. _deviceIndex 스냅샷 후 Dequeue 에 위임 — 그룹/디바이스 인덱스,
    /// 상태 전이, 심볼 이벤트 정리를 검증된 단일 경로로 처리한다.
    /// 주(선행의존): 실제 호출 트리거(DeviceNatsSyncService 의 DELETED 분기)는 DeviceApi PRD(C1)에서
    /// 연결된다. 현재는 메서드만 제공(dormant) — 회귀 위험 없음.
    /// </summary>
    public void RemoveByDevice(int deviceId, EnumDeviceType deviceType)
    {
        List<string> entryIds;
        lock (_gate)
        {
            if (!_deviceIndex.TryGetValue((deviceId, deviceType), out var set) || set.Count == 0)
                return;
            entryIds = set.ToList();   // 스냅샷 — Dequeue 가 _deviceIndex 를 변경하므로 먼저 복사
        }

        foreach (var entryId in entryIds)
            Dequeue(entryId);

        _log?.Info($"[EB3] RemoveByDevice: Device({deviceId},{deviceType}) — {entryIds.Count}개 엔트리 제거");
    }

    public void DequeueAll()
    {
        var groupStates = new List<(int GroupId, EnumCompositeEventStatus State)>();
        var deviceKeys = new List<(int Id, EnumDeviceType Type)>();
        var deviceStates = new List<(int Id, EnumDeviceType Type, EnumCompositeEventStatus Prev)>();

        lock (_gate)
        {
            foreach (var groupId in _groupIndex.Keys)
                groupStates.Add((groupId, ComputeGroupState(groupId)));

            foreach (var dk in _deviceIndex.Keys)
                deviceStates.Add((dk.Id, dk.Type, ComputeDeviceState(dk)));

            deviceKeys = _deviceIndex.Keys.Select(k => (k.Id, k.Type)).ToList();

            _entries.Clear();
            _groupIndex.Clear();
            _deviceIndex.Clear();
        }

        var onGroupEmpty = OnGroupEmpty;
        var onGroupStateChanged = OnGroupStateChanged;
        var onDeviceStateChanged = OnDeviceStateChanged;
        var onDeviceEmpty = OnDeviceEmpty;

        foreach (var (groupId, _) in groupStates)
            onGroupEmpty?.Invoke(groupId);

        foreach (var (groupId, prev) in groupStates)
            if (prev != EnumCompositeEventStatus.Normal)
                onGroupStateChanged?.Invoke(groupId, prev, EnumCompositeEventStatus.Normal);

        foreach (var (id, type, prev) in deviceStates)
            if (prev != EnumCompositeEventStatus.Normal)
                onDeviceStateChanged?.Invoke(id, type, prev, EnumCompositeEventStatus.Normal);

        foreach (var (id, type) in deviceKeys)
            onDeviceEmpty?.Invoke(id, type);

        RaiseActiveCountChanged();   // 전체 비움 → (0,0) 통지(GMap_Map_Instruments)
    }

    public EventEntry? GetEntry(string entryId)
    {
        lock (_gate)
            return _entries.TryGetValue(entryId, out var entry) ? entry : null;
    }

    /// <summary>활성 탐지/장애 건수 재집계 (D-01/03). _gate 보호 하 _entries를 EventType별 카운트.</summary>
    public (int Detection, int Fault) GetActiveCounts()
    {
        lock (_gate)
        {
            int det = 0, flt = 0;
            foreach (var e in _entries.Values)
            {
                if (e.EventType == EnumEventType.Intrusion) det++;
                else if (e.EventType == EnumEventType.Fault) flt++;
            }
            return (det, flt);
        }
    }

    /// <summary>활성 카운트 변경 통지 — 각 mutator 끝(lock 밖)에서 호출. 핸들러 없으면 재집계도 생략.</summary>
    private void RaiseActiveCountChanged()
    {
        var handler = OnActiveCountChanged;
        if (handler == null) return;
        var (det, flt) = GetActiveCounts();
        handler.Invoke(det, flt);
    }

    public int GetTotalQueueCount()
    {
        lock (_gate)
            return _entries.Count;
    }

    public bool HasEventsForGroup(int groupId)
    {
        lock (_gate)
            return _groupIndex.TryGetValue(groupId, out var entryIds) && entryIds.Count > 0;
    }

    public bool HasEventsForDevice(int deviceId, EnumDeviceType deviceType)
    {
        lock (_gate)
            return _deviceIndex.TryGetValue((deviceId, deviceType), out var entryIds) && entryIds.Count > 0;
    }

    public EventEntry? FindEntryByDevice(int deviceId, EnumDeviceType deviceType)
    {
        lock (_gate)
        {
            var key = (deviceId, deviceType);
            if (!_deviceIndex.TryGetValue(key, out var entryIds) || entryIds.Count == 0)
                return null;

            EventEntry? oldest = null;
            foreach (var id in entryIds)
                if (_entries.TryGetValue(id, out var e))
                    if (oldest == null || e.EnqueuedAt < oldest.EnqueuedAt)
                        oldest = e;
            return oldest;
        }
    }
    #endregion

    #region - SharedTimer -
    public void StartSharedTimer()
    {
        if (_sharedTimer != null) return;

        _sharedTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _sharedTimer.Tick += (_, _) => OnSharedTimerTick();
        _sharedTimer.Start();
    }

    public void StopSharedTimer()
    {
        if (_sharedTimer == null) return;

        _sharedTimer.Stop();
        _sharedTimer = null;
    }

    public void OnSharedTimerTick()
    {
        // 글로벌 kill-switch: IsAutoEventDiscard=false면 자동 조치보고 전체 비활성
        if (_eventSetupModel != null && !_eventSetupModel.IsAutoEventDiscard)
            return;

        var now = DateTime.Now;
        _expiredScratch.Clear();

        lock (_gate)
        {
            foreach (var e in _entries.Values)
            {
                if (!e.IsAutoReportEnabled) continue;
                if (e.AutoReportInFlight) continue;
                if (now < e.NextRetryAfter) continue;
                if ((now - e.EnqueuedAt).TotalSeconds < e.TimeoutSeconds) continue;
                _expiredScratch.Add(e);
            }

            if (_expiredScratch.Count > 1)
                _expiredScratch.Sort((a, b) => a.EnqueuedAt.CompareTo(b.EnqueuedAt));

            var fireCount = Math.Min(_expiredScratch.Count, MaxDequeuePerTick);
            for (var i = 0; i < fireCount; i++)
                _expiredScratch[i].AutoReportInFlight = true;

            if (_expiredScratch.Count > MaxDequeuePerTick)
                _expiredScratch.RemoveRange(MaxDequeuePerTick, _expiredScratch.Count - MaxDequeuePerTick);
        }

        var onAutoReport = OnAutoReport;
        foreach (var entry in _expiredScratch)
        {
            _log?.Info($"[AutoTimeout] 자동조치보고 발화: Device({entry.DeviceId},{entry.DeviceType}) elapsed={(DateTime.Now - entry.EnqueuedAt).TotalSeconds:F1}s timeout={entry.TimeoutSeconds}s");
            try { onAutoReport?.Invoke(entry); }
            catch (Exception ex)
            {
                _log?.Error($"OnAutoReport 발화 예외: {ex.Message}");
                entry.AutoReportInFlight = false;
            }
        }
    }
    #endregion

    #region - IDisposable -
    public void Dispose()
    {
        StopSharedTimer();
        lock (_gate)
        {
            _entries.Clear();
            _groupIndex.Clear();
            _deviceIndex.Clear();
        }
        OnGroupFirstEvent = null;
        OnGroupEmpty = null;
        OnGroupStateChanged = null;
        OnDeviceFirstEvent = null;
        OnDeviceEmpty = null;
        OnAutoReport = null;
        OnAnyEnqueue = null;
        OnActiveCountChanged = null;
        OnDeviceStateChanged = null;
        OnAutoRecovery = null;
    }
    #endregion

    #region - Private -
    /// <summary>_entries + _groupIndex 기반으로 그룹 복합 상태 계산 (SSOT: _entries)</summary>
    private EnumCompositeEventStatus ComputeGroupState(int groupId)
    {
        Debug.Assert(Monitor.IsEntered(_gate));

        if (!_groupIndex.TryGetValue(groupId, out var entryIds) || entryIds.Count == 0)
            return EnumCompositeEventStatus.Normal;

        bool hasBlackout = false;
        bool hasFault = false;
        bool hasDetection = false;

        foreach (var id in entryIds)
        {
            if (_entries.TryGetValue(id, out var entry))
            {
                if (entry.IsControllerBlackout) hasBlackout = true;
                if (entry.EventType == EnumEventType.Fault)
                    hasFault = true;
                else if (entry.EventType == EnumEventType.Intrusion)
                    hasDetection = true;
            }
        }

        // 단일 우선순위 지점(GMap_Controller_Blackout): Blackout > FaultedDetecting > Faulted > Detecting > Normal.
        return ControllerBlackoutModel.Resolve(hasBlackout, hasFault, hasDetection);
    }

    /// <summary>_entries + _deviceIndex 기반으로 디바이스 복합 상태 계산 (SSOT: _entries)</summary>
    private EnumCompositeEventStatus ComputeDeviceState((int Id, EnumDeviceType Type) deviceKey)
    {
        Debug.Assert(Monitor.IsEntered(_gate));

        if (!_deviceIndex.TryGetValue(deviceKey, out var entryIds) || entryIds.Count == 0)
            return EnumCompositeEventStatus.Normal;

        bool hasBlackout = false;
        bool hasFault = false;
        bool hasDetection = false;

        foreach (var id in entryIds)
        {
            if (_entries.TryGetValue(id, out var entry))
            {
                if (entry.IsControllerBlackout) hasBlackout = true;
                if (entry.EventType == EnumEventType.Fault)
                    hasFault = true;
                else if (entry.EventType == EnumEventType.Intrusion)
                    hasDetection = true;
            }
        }

        // 단일 우선순위 지점(GMap_Controller_Blackout): Blackout > FaultedDetecting > Faulted > Detecting > Normal.
        return ControllerBlackoutModel.Resolve(hasBlackout, hasFault, hasDetection);
    }
    #endregion

    #region - Events -
    /// <summary>그룹에 첫 이벤트 등록 시 (0→1 전이)</summary>
    public event Action<int, EnumEventType>? OnGroupFirstEvent;

    /// <summary>그룹의 이벤트가 모두 제거 시 (N→0 전이)</summary>
    public event Action<int>? OnGroupEmpty;

    /// <summary>그룹 복합 상태 전이 시 (groupId, prev, next)</summary>
    public event Action<int, EnumCompositeEventStatus, EnumCompositeEventStatus>? OnGroupStateChanged;

    /// <summary>개별 심볼에 첫 이벤트 등록 시 (0→1 전이)</summary>
    public event Action<int, EnumDeviceType, EnumEventType>? OnDeviceFirstEvent;

    /// <summary>개별 심볼의 이벤트가 모두 제거 시 (N→0 전이)</summary>
    public event Action<int, EnumDeviceType>? OnDeviceEmpty;

    /// <summary>자동 조치보고 타임아웃 시 발화 (EventEntry 전달 — Dequeue는 핸들러 책임)</summary>
    public event Action<EventEntry>? OnAutoReport;

    /// <summary>Enqueue 호출 시마다 발생 (0→1 전이 여부와 무관). EventType 전달.</summary>
    public event Action<EnumEventType>? OnAnyEnqueue;

    /// <summary>개별 디바이스 복합 상태 전이 시 (deviceId, deviceType, prev, next)</summary>
    public event Action<int, EnumDeviceType, EnumCompositeEventStatus, EnumCompositeEventStatus>? OnDeviceStateChanged;

    /// <summary>Fault 자동복구 완료 시 발화 (faultEntryId). 이미 Dequeue 처리 완료.</summary>
    public event Action<string>? OnAutoRecovery;

    /// <summary>활성 탐지/장애 건수 변경 (detection, fault) — GMap_Map_Instruments 인디케이터 소스.</summary>
    public event Action<int, int>? OnActiveCountChanged;
    #endregion

    #region - Properties -
    public int MaxDequeuePerTick { get; set; } = 5;
    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly IEventSetupModel? _eventSetupModel;
    private readonly Dictionary<string, EventEntry> _entries;
    private readonly Dictionary<int, HashSet<string>> _groupIndex;
    private readonly Dictionary<(int Id, EnumDeviceType Type), HashSet<string>> _deviceIndex;
    private readonly object _gate = new();
    private DispatcherTimer? _sharedTimer;
    // lock 전용 scratch 재사용 필드 — OnSharedTimerTick/Enqueue 내부에서만 사용
    private readonly List<EventEntry> _expiredScratch = new();
    private readonly HashSet<int> _scratchAffectedGroupIds = new();
    private readonly List<string> _scratchFaultIds = new();
    private readonly Dictionary<int, EnumCompositeEventStatus> _scratchPrevGroupStates = new();
    #endregion
}
