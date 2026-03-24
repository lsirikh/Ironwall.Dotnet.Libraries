using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
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
    public EventQueueManager()
    {
        _entries = new Dictionary<string, EventEntry>();
        _groupIndex = new Dictionary<int, HashSet<string>>();
        _deviceIndex = new Dictionary<(int, EnumDeviceType), HashSet<string>>();
    }
    #endregion

    #region - IEventQueueManager -
    public string Enqueue(EventEntry entry, string? externalEntryId = null)
    {
        entry.EntryId = !string.IsNullOrWhiteSpace(externalEntryId)
            ? externalEntryId
            : Guid.NewGuid().ToString();
        entry.EnqueuedAt = DateTime.Now;

        // 1. 글로벌 저장소 등록
        _entries[entry.EntryId] = entry;

        // 2. 그룹 역인덱스 등록
        if (entry.GroupIds != null)
        {
            foreach (var groupId in entry.GroupIds)
            {
                if (!_groupIndex.ContainsKey(groupId))
                    _groupIndex[groupId] = new HashSet<string>();

                var wasEmpty = _groupIndex[groupId].Count == 0;
                _groupIndex[groupId].Add(entry.EntryId);

                // 3. 그룹 0→1 전이: 심볼 Detecting 설정
                if (wasEmpty)
                    OnGroupFirstEvent?.Invoke(groupId, entry.EventType);
            }
        }

        // 3. 개별 심볼 역인덱스 등록
        var deviceKey = (entry.DeviceId, entry.DeviceType);
        if (!_deviceIndex.ContainsKey(deviceKey))
            _deviceIndex[deviceKey] = new HashSet<string>();

        var wasDeviceEmpty = _deviceIndex[deviceKey].Count == 0;
        _deviceIndex[deviceKey].Add(entry.EntryId);

        // 4. 개별 심볼 0→1 전이: 심볼 Detecting 설정
        if (wasDeviceEmpty)
            OnDeviceFirstEvent?.Invoke(entry.DeviceId, entry.DeviceType, entry.EventType);

        return entry.EntryId;
    }

    public void Dequeue(string entryId)
    {
        if (!_entries.TryGetValue(entryId, out var entry)) return;

        // 1. 글로벌 저장소에서 제거
        _entries.Remove(entryId);

        // 2. 그룹 역인덱스에서 제거 + 복원 판단
        if (entry.GroupIds != null)
        {
            foreach (var groupId in entry.GroupIds)
            {
                if (_groupIndex.TryGetValue(groupId, out var entryIds))
                {
                    entryIds.Remove(entryId);

                    // 그룹의 이벤트가 0건이 되면 → 심볼 Normal 복원
                    if (entryIds.Count == 0)
                    {
                        OnGroupEmpty?.Invoke(groupId);
                        _groupIndex.Remove(groupId);
                    }
                }
            }
        }

        // 3. 개별 심볼 역인덱스에서 제거 + 복원 판단
        var deviceKey = (entry.DeviceId, entry.DeviceType);
        if (_deviceIndex.TryGetValue(deviceKey, out var deviceEntryIds))
        {
            deviceEntryIds.Remove(entryId);

            // 개별 심볼의 이벤트가 0건이 되면 → 심볼 Normal 복원
            if (deviceEntryIds.Count == 0)
            {
                OnDeviceEmpty?.Invoke(entry.DeviceId, entry.DeviceType);
                _deviceIndex.Remove(deviceKey);
            }
        }
    }

    public void DequeueAll()
    {
        // 모든 그룹의 복원 이벤트를 발행하기 위해 현재 그룹 목록 스냅샷
        var groupIds = _groupIndex.Keys.ToList();

        // 모든 개별 심볼의 복원 이벤트를 발행하기 위해 현재 디바이스 목록 스냅샷
        var deviceKeys = _deviceIndex.Keys.ToList();

        _entries.Clear();
        _groupIndex.Clear();
        _deviceIndex.Clear();

        foreach (var groupId in groupIds)
        {
            OnGroupEmpty?.Invoke(groupId);
        }

        foreach (var deviceKey in deviceKeys)
        {
            OnDeviceEmpty?.Invoke(deviceKey.Id, deviceKey.Type);
        }
    }

    public EventEntry? GetEntry(string entryId)
    {
        return _entries.TryGetValue(entryId, out var entry) ? entry : null;
    }

    public int GetTotalQueueCount()
    {
        return _entries.Count;
    }

    public bool HasEventsForGroup(int groupId)
    {
        return _groupIndex.TryGetValue(groupId, out var entryIds) && entryIds.Count > 0;
    }

    public bool HasEventsForDevice(int deviceId, EnumDeviceType deviceType)
    {
        return _deviceIndex.TryGetValue((deviceId, deviceType), out var entryIds) && entryIds.Count > 0;
    }

    public EventEntry? FindEntryByDevice(int deviceId, EnumDeviceType deviceType)
    {
        var key = (deviceId, deviceType);
        if (!_deviceIndex.TryGetValue(key, out var entryIds) || entryIds.Count == 0)
            return null;

        // 가장 오래된 entry 반환 (매칭만, 제거하지 않음)
        return entryIds
            .Select(id => _entries[id])
            .OrderBy(e => e.EnqueuedAt)
            .FirstOrDefault();
    }
    #endregion

    #region - SharedTimer -
    /// <summary>
    /// DispatcherTimer를 생성하고 1초 간격으로 OnSharedTimerTick을 호출하도록 시작
    /// </summary>
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

    /// <summary>
    /// 공유 타이머를 정지하고 해제
    /// </summary>
    public void StopSharedTimer()
    {
        if (_sharedTimer == null) return;

        _sharedTimer.Stop();
        _sharedTimer = null;
    }

    /// <summary>
    /// 공유 타이머 Tick 핸들러 — 1초마다 호출되어 타임아웃 초과 이벤트를 자동 Dequeue
    /// </summary>
    public void OnSharedTimerTick()
    {
        var now = DateTime.Now;
        var expiredIds = _entries.Values
            .Where(e => e.IsAutoReportEnabled
                        && (now - e.EnqueuedAt).TotalSeconds >= e.TimeoutSeconds)
            .OrderBy(e => e.EnqueuedAt)
            .Take(MaxDequeuePerTick)
            .Select(e => e.EntryId!)
            .ToList();

        foreach (var entryId in expiredIds)
        {
            Dequeue(entryId);
            OnAutoReport?.Invoke(entryId);
        }
    }
    #endregion

    #region - IDisposable -
    public void Dispose()
    {
        StopSharedTimer();
        _entries.Clear();
        _groupIndex.Clear();
        _deviceIndex.Clear();
    }
    #endregion

    #region - Events -
    /// <summary>그룹에 첫 이벤트 등록 시 (0→1 전이)</summary>
    public event Action<int, EnumEventType>? OnGroupFirstEvent;

    /// <summary>그룹의 이벤트가 모두 제거 시 (N→0 전이)</summary>
    public event Action<int>? OnGroupEmpty;

    /// <summary>개별 심볼에 첫 이벤트 등록 시 (0→1 전이)</summary>
    public event Action<int, EnumDeviceType, EnumEventType>? OnDeviceFirstEvent;

    /// <summary>개별 심볼의 이벤트가 모두 제거 시 (N→0 전이)</summary>
    public event Action<int, EnumDeviceType>? OnDeviceEmpty;

    /// <summary>자동 조치보고 타임아웃 시</summary>
    public event Action<string>? OnAutoReport;
    #endregion

    #region - Properties -
    public int MaxDequeuePerTick { get; set; } = 5;
    #endregion

    #region - Attributes -
    private readonly Dictionary<string, EventEntry> _entries;
    private readonly Dictionary<int, HashSet<string>> _groupIndex;
    private readonly Dictionary<(int Id, EnumDeviceType Type), HashSet<string>> _deviceIndex;
    private DispatcherTimer? _sharedTimer;
    #endregion
}
