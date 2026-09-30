using System.Collections.Concurrent;
using System.Globalization;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.EventWindows;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.ViewModel.Models;

namespace Ironwall.Dotnet.Libraries.Events.Ui.EventWindows;

/****************************************************************************
   Purpose      : 탐지 → 이벤트 창 트리거 · 조치보고 → 창 닫기 (PRD camera-popup-modes FR-09 · 15 · 27 · 28 · NFR-02)
                  - EQM Enqueue(중복 걸러진 뒤) → 자체 모드 · 탐지/장애 켜짐일 때만 → 매핑 카메라(캐시) → 창 관리자
                  - 조치보고(로컬 · 원격, 종류 구분) → 조치보고로 닫기 켜짐이면 닫기(📌 제외)
                  - SYNC_EVENT_MAPPING(EventMappingsChangedMessage) → 매핑 캐시 비움
   Created By   : Claude (T-06)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <see cref="OnEntryEnqueued"/> 는 NATS 처리 줄에서 불린다 — 설정 · 열림 여부만 보고 곧바로 돌아가며,
/// 매핑 조회 · 창 명령은 배경 작업에서 한다(기다리지 않음, FR-28). 모든 입구는 예외를 삼키고 로그만 남긴다(FR-27).
/// 창 관리자가 없으면(호스트가 팝업 모듈을 등록하지 않음) 아무 일도 하지 않는다.
/// </summary>
public sealed class EventWindowTrigger : IHandle<EventMappingsChangedMessage>
{
    private const int ReportedMemory = 256;

    private readonly IEventWindowManager? _manager;
    private readonly IEventMappingCameraSource _cameras;
    private readonly IEventWindowDeviceDirectory _directory;
    private readonly Func<bool> _ptzAllowed;
    private readonly ILogService? _log;
    private readonly ConcurrentDictionary<string, byte> _inFlight = new(StringComparer.Ordinal);
    private readonly object _reportedGate = new();
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);
    private readonly Queue<string> _reportedOrder = new();

    public EventWindowTrigger(IEventWindowManager? manager, IEventMappingCameraSource cameras, IEventWindowDeviceDirectory directory,
                              Func<bool> ptzAllowed, ILogService? log = null)
    {
        _manager = manager;
        _cameras = cameras ?? throw new ArgumentNullException(nameof(cameras));
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _ptzAllowed = ptzAllowed ?? (() => false);
        _log = log;
    }

    /// <summary>창 관리자가 있어 실제로 동작하는가.</summary>
    public bool IsEnabled => _manager is not null;

    // ───────────────────────── 탐지 → 창 ─────────────────────────

    /// <summary>EQM <c>OnEntryEnqueued</c> 구독 — 곧바로 돌아간다.</summary>
    public void OnEntryEnqueued(EventEntry entry) => _ = TryStart(entry);

    /// <summary>창을 만들기 시작했으면 그 작업(시험이 기다린다), 아니면 null.</summary>
    internal Task? TryStart(EventEntry? entry)
    {
        try
        {
            if (_manager is null || entry is null || entry.EventId <= 0) return null;
            var settings = _manager.CurrentSettings;
            if (!EventWindowPlanning.ShouldOpen(settings, entry.EventType)) return null;

            var kind = EventWindowPlanning.KindOf(entry.EventType);
            var eventId = entry.EventId.ToString(CultureInfo.InvariantCulture);
            var key = EventKeys.Build(kind, eventId);
            if (WasReported(key)) return null;
            if (_manager.IsOpen(key))
            {
                _manager.BringToFront(kind, eventId);   // 같은 이벤트 = 새 창 없이 앞으로(FR-09)
                return null;
            }
            if (!_inFlight.TryAdd(key, 0)) return null;   // 같은 이벤트를 이미 만드는 중

            var snapshot = new EntrySnapshot(kind, eventId, entry.EventType, entry.DeviceId, entry.DeviceType,
                                             entry.GroupIds?.Where(g => g > 0).ToList() ?? new List<int>(),
                                             entry.EnqueuedAt == default ? DateTime.Now : entry.EnqueuedAt);
            return Task.Run(() => BuildAndOpenAsync(snapshot, key));
        }
        catch (Exception ex)
        {
            _log?.Error($"[EventWindow] 탐지 트리거 실패: {ex.GetType().Name} {ex.Message}");
            return null;
        }
    }

    private async Task BuildAndOpenAsync(EntrySnapshot s, string key)
    {
        try
        {
            var entries = await _cameras.GetCamerasForGroupsAsync(s.GroupIds).ConfigureAwait(false);

            var manager = _manager!;
            var settings = manager.CurrentSettings;
            if (!EventWindowPlanning.ShouldOpen(settings, s.EventType)) return;   // 조회하는 사이 설정이 바뀜
            if (WasReported(key))
            {
                _log?.Info($"[EventWindow] 매핑을 읽는 사이 조치보고됨 — 창 열지 않음: {key}");
                return;
            }

            var ptz = SafePtzAllowed();
            var (cameras, extra) = EventWindowPlanning.Select(entries, settings.CamerasPerWindow,
                e => _directory.FindCamera(e.CameraId) is { } camera ? EventWindowPlanning.BuildCamera(camera, e, settings, ptz) : null);
            if (cameras.Count == 0)
            {
                _log?.Info($"[EventWindow] 매핑 카메라 없음 — 창 열지 않음: {key}, groups=[{string.Join(",", s.GroupIds)}], 배선 {entries.Count}건");
                return;
            }

            var zone = s.GroupIds.Select(_directory.GroupName).FirstOrDefault(n => n is not null);
            var header = EventWindowPlanning.BuildHeader(s.EventType, zone, _directory.DeviceName(s.DeviceId, s.DeviceType), s.OccurredAt);
            var result = manager.Open(new EventWindowRequest
            {
                Kind = s.Kind,
                EventId = s.EventId,
                Header = header,
                Cameras = cameras,
                ExtraCameraCount = extra,
                Title = EventWindowPlanning.BuildTitle(header, s.EventId),
            });
            _log?.Info($"[EventWindow] {key} → {result} (카메라 {cameras.Count}대, +{extra})");

            // 창 명령을 보내는 사이 조치보고가 닿았으면 곧바로 닫는다(열기 · 닫기 순서가 뒤집히지 않게).
            if (result == EventWindowOpenResult.Opened && WasReported(key)) manager.CloseForActionReport(s.Kind, s.EventId);
        }
        catch (Exception ex)
        {
            _log?.Error($"[EventWindow] 창 만들기 실패 {key}: {ex.GetType().Name} {ex.Message}");
        }
        finally
        {
            _inFlight.TryRemove(key, out _);
        }
    }

    private bool SafePtzAllowed()
    {
        try { return _ptzAllowed(); }
        catch (Exception ex)
        {
            _log?.Warning($"[EventWindow] PTZ 권한 확인 실패 — 끔: {ex.Message}");
            return false;   // 명령류는 모르면 닫는다(fail-closed)
        }
    }

    // ───────────────────────── 조치보고 → 닫기 ─────────────────────────

    /// <summary>
    /// 이벤트 카드 목록의 <c>ActionReported(kind, eventId)</c> 구독 — 로컬(카드 · 창 · 트레이 · 전체 · 자동 · 자동복구) · 원격 ACTION_REPORT 공통.
    /// 종류("detection" · "malfunction")로 창을 가른다 — 탐지 7번의 조치가 장애 7번 창을 닫지 않는다.
    /// </summary>
    public void OnActionReported(string kind, int eventId)
    {
        try
        {
            if (_manager is null || eventId <= 0) return;
            if (EventWindowPlanning.KindOf(kind) is not { } windowKind) return;
            var id = eventId.ToString(CultureInfo.InvariantCulture);
            RememberReported(EventKeys.Build(windowKind, id));
            _manager.CloseForActionReport(windowKind, id);
        }
        catch (Exception ex)
        {
            _log?.Error($"[EventWindow] 조치보고 닫기 실패 {kind} {eventId}: {ex.GetType().Name} {ex.Message}");
        }
    }

    private void RememberReported(string key)
    {
        lock (_reportedGate)
        {
            if (!_reported.Add(key)) return;
            _reportedOrder.Enqueue(key);
            while (_reportedOrder.Count > ReportedMemory) _reported.Remove(_reportedOrder.Dequeue());
        }
    }

    private bool WasReported(string key)
    {
        lock (_reportedGate) return _reported.Contains(key);
    }

    // ───────────────────────── 매핑 변경 ─────────────────────────

    /// <summary>서버 SYNC_EVENT_MAPPING — 호스트가 옮겨 온다. 캐시만 비우고 곧바로 돌아간다.</summary>
    public Task HandleAsync(EventMappingsChangedMessage message, CancellationToken cancellationToken)
    {
        try { _cameras.Invalidate(); }
        catch (Exception ex) { _log?.Error($"[EventWindow] 매핑 캐시 비우기 실패: {ex.Message}"); }
        return Task.CompletedTask;
    }

    private sealed record EntrySnapshot(EventWindowKind Kind, string EventId, EnumEventType EventType, int DeviceId,
                                        EnumDeviceType DeviceType, List<int> GroupIds, DateTime OccurredAt);
}
