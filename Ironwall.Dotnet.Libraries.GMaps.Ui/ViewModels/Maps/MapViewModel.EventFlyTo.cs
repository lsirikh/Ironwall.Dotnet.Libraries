using Caliburn.Micro;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 이벤트 발생 시 지도 이동(줌 + 부드러운 팬) — event-map-flyto PRD FR-01~07.
                  판정 · 보간은 순수(MapFlyToMath), 여기는 큐 구독 · 프레임 적용 · 사용자 조작 감지만.
   Note         : 프레임마다 경과 ms(Stopwatch)로 자세를 정한다 — 프레임이 빠지면(RDP · 저사양) 건너뛸 뿐 끝나는 시간은 같다.
                  줌은 벤더 정수 + 디지털 반 단계라 0.5 로 스냅한 값이 바뀔 때만 SetEffectiveZoom 을 부른다.
   Created On   : 2026-10-13 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel
{
    private MapEventFlyToModel? _flyToSettings;
    private MapFlyToAnimation? _flyAnimation;
    private readonly Stopwatch _flyWatch = new();
    private double _flyAppliedZoom;
    private bool _flyRenderingHooked;
    private GMapCustomControl? _flyInputHookedMap;
    private long _flyLastUserInputTick = long.MinValue / 2;

    /// <summary>초기 배선 — 설정 해석 · 큐 구독 · 사용자 조작 훅. 부팅(MainMap 준비 뒤) 1회.</summary>
    private void InitializeEventFlyTo()
    {
        try
        {
            // DI 싱글톤(설정 블록과 같은 인스턴스) — 없으면(시험 · 미등록) 파일에서 직접.
            _flyToSettings = IoC.GetAll<MapEventFlyToModel>().FirstOrDefault() ?? MapSettingsHelper.LoadMapEventFlyTo(_log);
            if (MainMap != null)
            {
                var setup = IoC.GetAll<MapEventFlyToSetupViewModel>().FirstOrDefault();
                if (setup != null) setup.SetZoomRange(MainMap.MinZoom, MainMap.MaxZoom);
                else _flyToSettings.Normalize(MainMap.MinZoom, MainMap.MaxZoom);
            }
        }
        catch (Exception ex)
        {
            _flyToSettings ??= new MapEventFlyToModel();
            _log?.Warning($"[EventFlyTo] 설정 해석 실패, 기본값: {ex.Message}");
        }

        // 큐 진입 훅은 구체 형식에만 있다(인터페이스를 넓히지 않는다 — 목 · 가짜 구현 보호).
        if (_eventQueueManager is EventQueueManager eqm)
        {
            eqm.OnEntryEnqueued -= OnEntryEnqueuedForFlyTo;
            eqm.OnEntryEnqueued += OnEntryEnqueuedForFlyTo;
        }
        EnsureFlyInputHooked();
        _log?.Info($"[EventFlyTo] 배선: {_flyToSettings}");
    }

    /// <summary>큐 진입(NATS 처리 줄에서 옴) — 종류가 꺼져 있으면 바로 돌아가고, 아니면 UI 스레드로 넘긴다.</summary>
    private void OnEntryEnqueuedForFlyTo(EventEntry entry)
    {
        if (entry == null || !MapFlyToMath.IsEnabledFor(_flyToSettings, entry.EventType)) return;
        OnUIThread(() => FlyToEventEntry(entry));
    }

    /// <summary>
    /// 그 장비의 심볼로 비행 — 심볼이 없으면 소속 구역 선(탐지 센서 구간 가운데, 없으면 구역 전체)으로. 사용자가 지도(2D · 3D)를
    /// 만지는 중이면 양보(FR-05). 3D 표시 중이고 목표에서 3D 를 유지하면 2D 비행 없이 바로 3D 카메라(뷰어가 부드럽게/바로를 판단).
    /// </summary>
    private void FlyToEventEntry(EventEntry entry)
    {
        try
        {
            if (MainMap == null || _flyToSettings == null) return;
            EnsureFlyInputHooked();

            var user = new FlyToUserState(
                MainMap.IsDragging, MainMap.IsMouseCaptured, IsEditModeEnabled,
                ExclusiveInputModeGate.IsActive(MainMap),
                Environment.TickCount64 - _flyLastUserInputTick);
            if (MapFlyToMath.ShouldYield(user))
            {
                _log?.Info($"[EventFlyTo] 사용자 조작 중 — 이동 양보({entry.EventType} 장비 {entry.DeviceId})");
                return;
            }

            Sync2DFrom3DCamera();   // 3D 표시 중이면 지금 보이는 3D 자리에서 출발(MapViewModel.Map3D)
            var currentZoom = MainMap.EffectiveZoom;
            PointLatLng target;
            double targetZoom;
            string what;
            var marker = FindDeviceMarker(entry.DeviceId, entry.DeviceType);
            if (marker != null)
            {
                target = marker.Position;
                targetZoom = MapFlyToMath.ResolveTargetZoom(_flyToSettings.MaxZoom, MainMap.MinZoom, MainMap.MaxZoom, currentZoom);
                what = "심볼";
            }
            else
            {
                var device = DeviceProvider.FirstOrDefault(d => d.Id == entry.DeviceId && d.DeviceType == entry.DeviceType);
                var groupIds = MapFlyToMath.ZoneGroupCandidates(entry.GroupIds, device?.DeviceGroups, entry.DeviceType == EnumDeviceType.Fence_Group, entry.DeviceId);
                var zone = groupIds.Select(id => MainMap.Markers.OfType<GMapPidsGroupMarker>().FirstOrDefault(z => z.LinkedDeviceGroup == id)).FirstOrDefault(z => z != null);
                var line = zone?.RuntimePoints.Select(p => (p.Lat, p.Lng)).ToList();
                if (zone == null || MapFlyToMath.ResolveZoneTarget(line!, zone.IsClosedPath, ActiveZoneSensorPoints(zone.LinkedDeviceGroup)) is not { } zt)
                {
                    _log?.Info($"[EventFlyTo] 이동할 곳 없음 — 장비 심볼 · 구역 선이 지도에 없다({entry.EventType} 장비 {entry.DeviceId} {entry.DeviceType}, 그룹 [{string.Join(",", groupIds)}])");
                    return;
                }
                target = new PointLatLng(zt.Lat, zt.Lng);
                targetZoom = MapFlyToMath.ResolveZoneZoom(zt.IsSection, _flyToSettings.MaxZoom, MainMap.MinZoom, MainMap.MaxZoom, currentZoom,
                                                          MapFlyToMath.FitZoom(line!, MainMap.ActualWidth, MainMap.ActualHeight));
                what = $"구역 '{zone.Title}' {(zt.IsSection ? "탐지 구간" : "전체")}";
            }
            var center = MainMap.Position;
            var duration = MapFlyToMath.PlanDurationMs(
                _flyToSettings.DurationMs,
                isTargetInView: MainMap.ViewArea.Contains(target),
                isAtCenter: Math.Abs(center.Lat - target.Lat) < MapFlyToMath.CenterEpsilonDeg && Math.Abs(center.Lng - target.Lng) < MapFlyToMath.CenterEpsilonDeg,
                zoomChanges: MapFlyToMath.SnapHalf(targetZoom) != MapFlyToMath.SnapHalf(currentZoom));
            if (duration is not int ms) return;

            var path = MapFlyToMath.ChoosePath(_map3DState == Map3DState.Shown, Map3DStaysAt(target, targetZoom));
            if (path == FlyToPath.ThreeD) ms = 0;   // 2D 는 목표로 바로 옮기고 3D 카메라만 animate(FlyTo 0 ms → Sync3DAfter2DMove)
            else if (path == FlyToPath.Leave3DThenTwoD) Leave3D(null, "event-flyto");   // 2D 가 이미 3D 자리(Sync2DFrom3DCamera)
            FlyTo(target, targetZoom, ms);
            _log?.Info($"[EventFlyTo] {entry.EventType} 장비 {entry.DeviceId} → {what} · 줌 {targetZoom:0.#} · {path} · {ms} ms");
        }
        catch (Exception ex)
        {
            _log?.Error($"[EventFlyTo] 이동 실패: {ex.Message}");
        }
    }

    /// <summary>그 장비의 지도 심볼(없으면 null).</summary>
    private IPidsEditableMarker? FindDeviceMarker(int deviceId, EnumDeviceType deviceType)
        => MainMap?.Markers.OfType<IPidsEditableMarker>()
            .FirstOrDefault(m => (m.LinkedDevice?.Id ?? m.LinkedDeviceId) == deviceId && m.DeviceType == deviceType);

    /// <summary>구역 그룹에서 이벤트가 살아 있는 장비 자리 — 지도 심볼이 있으면 심볼, 없으면 장비 좌표(0,0 · 수 아님은 뺀다).</summary>
    private IEnumerable<(double Lat, double Lng)> ActiveZoneSensorPoints(int groupId)
    {
        foreach (var d in DeviceProvider.Where(d => d.DeviceGroups?.Contains(groupId) == true).ToList())
        {
            if (!_eventQueueManager.HasEventsForDevice(d.Id, d.DeviceType)) continue;
            if (FindDeviceMarker(d.Id, d.DeviceType) is { } m) yield return (m.Position.Lat, m.Position.Lng);
            else if (double.IsFinite(d.Latitude) && double.IsFinite(d.Longitude) && (d.Latitude != 0 || d.Longitude != 0)) yield return (d.Latitude, d.Longitude);
        }
    }

    /// <summary>3D 에 보낼 구역 선 위 탐지 센서 구간(선 길이 비율) — 모르면 null(Map3DOverlayBridge).</summary>
    private (double From, double To)? ZoneDetectionSection(GMapPidsGroupMarker zone)
        => zone.LinkedDeviceGroup > 0
            ? MapFlyToMath.ZoneSection(zone.RuntimePoints.Select(p => (p.Lat, p.Lng)).ToList(), zone.IsClosedPath, ActiveZoneSensorPoints(zone.LinkedDeviceGroup))
            : null;

    /// <summary>
    /// 공용 이동 — 목표 위치(와 줌)로 <paramref name="durationMs"/> 동안 부드럽게. 0 ms = 순간 이동.
    /// 진행 중 비행이 있으면 지금 보이는 자세에서 이어서 출발한다(FR-06). 호출 스레드: UI.
    /// </summary>
    private void FlyTo(PointLatLng target, double? targetZoom, int durationMs)
    {
        if (MainMap == null) return;
        durationMs = MapEventFlyToModel.NormalizeDurationMs(durationMs);

        if (durationMs <= 0)
        {
            StopFly();
            if (targetZoom is double z && MapFlyToMath.SnapHalf(z) != MapFlyToMath.SnapHalf(MainMap.EffectiveZoom))
                MainMap.SetEffectiveZoom(z);
            MainMap.Position = target;
            Sync3DAfter2DMove("event-flyto");
            return;
        }

        var current = new FlyToPose(MainMap.Position.Lat, MainMap.Position.Lng, MainMap.EffectiveZoom);
        var end = new FlyToPose(target.Lat, target.Lng, targetZoom ?? current.Zoom);
        _flyAnimation = MapFlyToMath.Retarget(_flyAnimation, _flyWatch.Elapsed.TotalMilliseconds, current, end, durationMs);
        _flyAppliedZoom = MapFlyToMath.SnapHalf(MainMap.EffectiveZoom);
        _flyWatch.Restart();
        if (!_flyRenderingHooked)
        {
            CompositionTarget.Rendering += OnFlyRendering;
            _flyRenderingHooked = true;
        }
    }

    /// <summary>프레임마다 경과 ms 로 자세를 정해 지도에 옮긴다 — 줌을 먼저(중심 유지), 위치를 다음.</summary>
    private void OnFlyRendering(object? sender, EventArgs e)
    {
        var animation = _flyAnimation;
        if (animation == null || MainMap == null) { StopFly(); return; }
        try
        {
            var elapsed = _flyWatch.Elapsed.TotalMilliseconds;
            var pose = animation.Sample(elapsed);
            var zoom = MapFlyToMath.SnapHalf(pose.Zoom);
            if (zoom != _flyAppliedZoom)
            {
                MainMap.SetEffectiveZoom(zoom);
                _flyAppliedZoom = zoom;
            }
            MainMap.Position = new PointLatLng(pose.Lat, pose.Lng);
            if (animation.IsDone(elapsed))
            {
                StopFly();
                Sync3DAfter2DMove("event-flyto");   // 3D 표시 중이면 끝난 자리 · 줌을 3D 에도
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"[EventFlyTo] 프레임 적용 실패: {ex.Message}");
            StopFly();
        }
    }

    /// <summary>비행을 그 자리에서 멈춘다.</summary>
    private void StopFly()
    {
        _flyAnimation = null;
        _flyWatch.Reset();
        if (_flyRenderingHooked)
        {
            CompositionTarget.Rendering -= OnFlyRendering;
            _flyRenderingHooked = false;
        }
    }

    /// <summary>지도를 누르거나 휠을 돌리면 시각을 적고 진행 중 비행을 멈춘다(빼앗지 않는다). 소비하지 않는다.</summary>
    private void EnsureFlyInputHooked()
    {
        if (MainMap == null || ReferenceEquals(_flyInputHookedMap, MainMap)) return;
        if (_flyInputHookedMap != null)
        {
            _flyInputHookedMap.PreviewMouseDown -= OnFlyUserInput;
            _flyInputHookedMap.PreviewMouseWheel -= OnFlyUserInput;
        }
        MainMap.PreviewMouseDown += OnFlyUserInput;
        MainMap.PreviewMouseWheel += OnFlyUserInput;
        _flyInputHookedMap = MainMap;
    }

    /// <summary>3D 의 사용자 조작(camera reason "user" · "live") — 2D 조작과 같은 2 s 양보 규칙에 넣는다.</summary>
    private void NoteMap3DCameraForFlyTo(string? reason)
    {
        if (MapFlyToMath.IsUserCameraReason(reason)) _flyLastUserInputTick = Environment.TickCount64;
    }

    private void OnFlyUserInput(object sender, MouseEventArgs e)
    {
        _flyLastUserInputTick = Environment.TickCount64;
        if (_flyAnimation != null) StopFly();
    }
}
