using System.Collections.Concurrent;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;

namespace Ironwall.Dotnet.Libraries.Events.Ui.EventWindows;

/****************************************************************************
   Purpose      : 이벤트 매핑 카메라 조회 + 캐시 (PRD camera-popup-modes FR-09 · §3 트리거)
                  - 서버 7.0+: 워크벤치 게이트웨이(매핑 목록 1회 + 매핑별 카메라, 프리셋 참조 포함)
                  - 6.3: 옛 IEventApiService(그룹별 매핑, 프리셋은 DB id 뿐이라 자동 이동 없음)
                  - SYNC_EVENT_MAPPING(EventMappingsChangedMessage) → Invalidate
   Created By   : Claude (T-06)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>그룹(구역)에 걸린 활성 매핑의 카메라 배선을 돌려준다.</summary>
public interface IEventMappingCameraSource
{
    /// <summary>
    /// <paramref name="groupIds"/> 중 하나에 걸린 <b>켜진</b> 매핑의 카메라 배선(정렬 · 중복 제거 전). 실패하면 빈 목록 — 예외를 내지 않는다.
    /// </summary>
    Task<IReadOnlyList<MappingCameraEntry>> GetCamerasForGroupsAsync(IReadOnlyCollection<int> groupIds, CancellationToken token = default);

    /// <summary>캐시를 버린다(서버 SYNC_EVENT_MAPPING). 다음 조회가 서버를 다시 읽는다.</summary>
    void Invalidate();
}

/// <summary>
/// 캐시 단위: 매핑 목록 1벌 + 매핑별 카메라(7.0+), 또는 그룹별 결과(6.3). 같은 열쇠의 동시 조회는 한 번의 요청을 함께 기다린다.
/// 실패한 조회는 캐시에 남기지 않는다(다음 탐지가 다시 시도). <see cref="Invalidate"/> 전에 시작된 조회는 옛 세대로 표시돼 새 캐시에 들어가지 않는다.
/// </summary>
public sealed class EventMappingCameraCache : IEventMappingCameraSource
{
    /// <summary>운영 이벤트 전용 매핑 — 탐지 · 장애 창과 무관하다.</summary>
    internal const string OperationOnlyCategory = "OPERATION_ONLY";
    private const int LegacyPageLimit = 100;

    private readonly IMappingWorkbenchGateway? _gateway;
    private readonly IEventApiService? _legacyApi;
    private readonly ILogService? _log;
    private readonly object _gate = new();
    private Task<IReadOnlyList<EventMappingReadDto>?>? _mappings;
    private readonly ConcurrentDictionary<int, Task<IReadOnlyList<MappingCameraEntry>?>> _camerasByMapping = new();
    private readonly ConcurrentDictionary<int, Task<IReadOnlyList<MappingCameraEntry>?>> _legacyByGroup = new();
    private int _generation;
    private int _legacyNoticeLogged;

    public EventMappingCameraCache(IMappingWorkbenchGateway? gateway, IEventApiService? legacyApi = null, ILogService? log = null)
    {
        _gateway = gateway;
        _legacyApi = legacyApi;
        _log = log;
    }

    /// <summary>시험 · 진단 — 캐시가 버려진 횟수.</summary>
    public int Generation => Volatile.Read(ref _generation);

    public void Invalidate()
    {
        lock (_gate)
        {
            _generation++;
            _mappings = null;
            _camerasByMapping.Clear();
            _legacyByGroup.Clear();
        }
        _log?.Info("[EventWindow] 이벤트 매핑이 바뀌어 카메라 캐시를 비움");
    }

    public async Task<IReadOnlyList<MappingCameraEntry>> GetCamerasForGroupsAsync(IReadOnlyCollection<int> groupIds, CancellationToken token = default)
    {
        try
        {
            var groups = groupIds?.Where(g => g > 0).Distinct().ToList() ?? new List<int>();
            if (groups.Count == 0) return Array.Empty<MappingCameraEntry>();
            return UseGateway
                ? await FromGatewayAsync(groups, token).ConfigureAwait(false)
                : await FromLegacyAsync(groups, token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log?.Warning($"[EventWindow] 매핑 카메라 조회 실패: {ex.GetType().Name} {ex.Message}");
            return Array.Empty<MappingCameraEntry>();
        }
    }

    private bool UseGateway => _gateway is not null && (_gateway is not MappingWorkbenchGateway real || real.IsSupported);

    // ───────────────────────── 7.0+ ─────────────────────────

    private async Task<IReadOnlyList<MappingCameraEntry>> FromGatewayAsync(List<int> groups, CancellationToken token)
    {
        var mappings = await CachedAsync(() => _mappings, t => _mappings = t, () => LoadMappingsAsync(token)).ConfigureAwait(false);
        if (mappings is null) return Array.Empty<MappingCameraEntry>();

        var relevant = mappings
            .Where(m => m.Status && m.DeviceGroupId is int g && groups.Contains(g) && !IsOperationOnly(m.CategoryEventMapping))
            .OrderBy(m => groups.IndexOf(m.DeviceGroupId!.Value))
            .ThenBy(m => m.Id)
            .ToList();

        var result = new List<MappingCameraEntry>();
        foreach (var mapping in relevant)
        {
            var id = mapping.Id;
            var cameras = await CachedAsync(() => _camerasByMapping.TryGetValue(id, out var t) ? t : null,
                                            t => _camerasByMapping[id] = t,
                                            () => LoadMappingCamerasAsync(id, token)).ConfigureAwait(false);
            if (cameras is not null) result.AddRange(cameras);
        }
        return result;
    }

    private async Task<IReadOnlyList<EventMappingReadDto>?> LoadMappingsAsync(CancellationToken token)
    {
        var r = await _gateway!.ListMappingsAsync(token).ConfigureAwait(false);
        if (r.IsSuccess && r.Value is not null) return r.Value;
        _log?.Warning($"[EventWindow] 매핑 목록 조회 실패: {r.Message} ({r.RawError})");
        return null;
    }

    private async Task<IReadOnlyList<MappingCameraEntry>?> LoadMappingCamerasAsync(int mappingId, CancellationToken token)
    {
        var r = await _gateway!.ListCamerasAsync(mappingId, token).ConfigureAwait(false);
        if (!r.IsSuccess || r.Value is null)
        {
            _log?.Warning($"[EventWindow] 매핑 {mappingId} 카메라 조회 실패: {r.Message} ({r.RawError})");
            return null;
        }
        return r.Value
            .Where(c => c.Camera is { Id: > 0 })
            .Select(c => new MappingCameraEntry(
                c.Camera!.Id, c.Priority, c.ConfigId, Math.Max(0, c.DelayTime), c.IsEnable,
                c.TargetPreset?.PresetIndex, c.TargetPreset?.PresetName, c.HomePreset?.PresetIndex))
            .ToList();
    }

    // ───────────────────────── 6.3 ─────────────────────────

    private async Task<IReadOnlyList<MappingCameraEntry>> FromLegacyAsync(List<int> groups, CancellationToken token)
    {
        if (_legacyApi is null) return Array.Empty<MappingCameraEntry>();
        if (Interlocked.Exchange(ref _legacyNoticeLogged, 1) == 0)
            _log?.Info("[EventWindow] 서버 계약 7.0 미만 — 옛 매핑 API 로 카메라만 읽는다(프리셋 자동 이동 없음: 옛 응답엔 프리셋 번호가 없다)");

        var result = new List<MappingCameraEntry>();
        foreach (var group in groups)
        {
            var g = group;
            var cameras = await CachedAsync(() => _legacyByGroup.TryGetValue(g, out var t) ? t : null,
                                            t => _legacyByGroup[g] = t,
                                            () => LoadLegacyGroupAsync(g, token)).ConfigureAwait(false);
            if (cameras is not null) result.AddRange(cameras);
        }
        return result;
    }

    private async Task<IReadOnlyList<MappingCameraEntry>?> LoadLegacyGroupAsync(int groupId, CancellationToken token)
    {
        var list = await _legacyApi!.GetEventMappingsAsync(groupId, true, 1, LegacyPageLimit, token).ConfigureAwait(false);
        if (list is null || !list.Success)
        {
            _log?.Warning($"[EventWindow] 그룹 {groupId} 매핑 조회 실패(옛 API): {list?.Message}");
            return null;
        }
        var result = new List<MappingCameraEntry>();
        foreach (var mapping in (list.Data ?? new List<EventMappingDto>())
                     .Where(m => m.Status != false && !IsOperationOnly(m.CategoryEventMapping))
                     .OrderBy(m => m.Id))
        {
            var cameras = mapping.Cameras;
            if (cameras is null)
            {
                var r = await _legacyApi.GetMappingCamerasAsync(mapping.Id, token).ConfigureAwait(false);
                if (r is null || !r.Success) return null;   // 한 매핑이라도 못 읽으면 캐시하지 않는다
                cameras = r.Data ?? new List<EventMappingCameraDto>();
            }
            result.AddRange(cameras.Where(c => c.CameraId > 0)
                .Select((c, i) => new MappingCameraEntry(c.CameraId, c.Priority, mapping.Id * 1000 + i, Math.Max(0, c.DelayTime), c.IsEnable)));
        }
        return result;
    }

    // ───────────────────────── 공통 ─────────────────────────

    private static bool IsOperationOnly(string? category)
        => string.Equals(category, OperationOnlyCategory, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 캐시된 조회를 함께 기다린다. 없으면 새로 시작해 넣는다. 결과가 null(실패)이면 — 아직 같은 조회가 들어 있을 때만 — 뺀다.
    /// 무효화 이후 끝난 조회는 새 세대 캐시에 영향이 없다(무효화가 이미 비웠다).
    /// </summary>
    private async Task<T?> CachedAsync<T>(Func<Task<T?>?> read, Action<Task<T?>> write, Func<Task<T?>> load) where T : class
    {
        Task<T?> task;
        int generation;
        lock (_gate)
        {
            generation = _generation;
            var existing = read();
            if (existing is null)
            {
                existing = Task.Run(async () =>
                {
                    try { return await load().ConfigureAwait(false); }
                    catch (Exception ex)
                    {
                        _log?.Warning($"[EventWindow] 매핑 조회 예외: {ex.GetType().Name} {ex.Message}");
                        return null;   // 실패는 캐시에 남기지 않는다(아래에서 뺀다)
                    }
                });
                write(existing);
            }
            task = existing;
        }
        var value = await task.ConfigureAwait(false);
        if (value is null)
        {
            lock (_gate)
            {
                if (generation == _generation && ReferenceEquals(read(), task)) Forget(task);
            }
        }
        return value;
    }

    private void Forget<T>(Task<T?> task) where T : class
    {
        if (ReferenceEquals(_mappings, task)) _mappings = null;
        foreach (var kv in _camerasByMapping)
            if (ReferenceEquals(kv.Value, task)) _camerasByMapping.TryRemove(kv.Key, out _);
        foreach (var kv in _legacyByGroup)
            if (ReferenceEquals(kv.Value, task)) _legacyByGroup.TryRemove(kv.Key, out _);
    }
}
