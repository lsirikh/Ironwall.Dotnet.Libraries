using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Threading;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;

/****************************************************************************
   Purpose      : 지도 마커 → 3D(map-3d) 오버레이 다리 — 3D 가 보이는 동안만 구독해 전체 스냅샷 1회 + 변경분(patch · add · remove)을 보낸다.
   Note         : ★ 대상 = 점 심볼(GMapPidsMarker "pids" · GMapCustomMarker "symbol") · 선(GMapPidsGroupMarker "pidsGroup" · GMapLineMarker "line").
                    이미지 · TIF · 추적 · 도형 · 군사부호 · 시설물은 보내지 않는다.
                  ★ 상태 변경은 마커 PropertyChanged 하나로 받는다 — 심볼 모델 Update 를 마커가 받아 EventStatus · CompositeStatus ·
                    OperationState · DoorState(구역은 ActiveSensorDeviceIds 포함)로 다시 통지하므로 모델에 따로 붙지 않는다.
                    통지는 50 ms 모아(코얼레싱) 직전에 보낸 JSON 과 비교해 바뀐 필드만 patch 로 보낸다(선택 · 크기 등 무관한 통지는 버려진다).
                  ★ 어느 스레드에서 통지가 와도 UI 스레드에서 처리한다.
   Created On   : 2026-10-17 · Sensorway Co., Ltd.
****************************************************************************/
internal sealed class Map3DOverlayBridge
{
    private readonly Action<string> _post;
    private readonly ILogService? _log;
    private readonly Dictionary<string, JObject> _sent = new();
    private readonly HashSet<GMapMarker> _subscribed = new();
    private readonly HashSet<GMapMarker> _dirty = new();
    private GMapCustomControl? _map;
    private DispatcherTimer? _timer;
    private long _rev;

    public Map3DOverlayBridge(Action<string> post, ILogService? log)
    {
        _post = post;
        _log = log;
    }

    /// <summary>구독 중인가(3D 표시 중).</summary>
    public bool IsActive => _map != null;

    /// <summary>구독을 시작하고 전체 스냅샷을 보낸다. 이미 같은 지도에 붙어 있으면 스냅샷만 다시 보낸다.</summary>
    public void Start(GMapCustomControl map)
    {
        if (!ReferenceEquals(_map, map))
        {
            Stop();
            _map = map;
            map.Markers.CollectionChanged += OnMarkersChanged;
        }
        SendSnapshot();
    }

    /// <summary>구독을 모두 풀고 보낸 기록을 버린다(3D 가 꺼짐).</summary>
    public void Stop()
    {
        if (_map != null) _map.Markers.CollectionChanged -= OnMarkersChanged;
        _map = null;
        foreach (var m in _subscribed) ((INotifyPropertyChanged)m).PropertyChanged -= OnMarkerPropertyChanged;
        _subscribed.Clear();
        _dirty.Clear();
        _sent.Clear();
        _timer?.Stop();
    }

    /// <summary>전체 스냅샷(3D ready 직후 · 재사용 펼침 · 3D 의 overlay.resync 요청).</summary>
    public void SendSnapshot()
    {
        if (_map == null) return;
        foreach (var m in _subscribed) ((INotifyPropertyChanged)m).PropertyChanged -= OnMarkerPropertyChanged;
        _subscribed.Clear();
        _dirty.Clear();
        _sent.Clear();

        var points = new List<JObject>();
        var lines = new List<JObject>();
        foreach (var marker in _map.Markers.ToList())
        {
            var item = Track(marker);
            if (item == null) continue;
            (Map3DOverlay.IsLineKind(item.Value<string>("kind")) ? lines : points).Add(item);
        }
        _post(Map3DOverlay.SnapshotJson(++_rev, points, lines));
        _log?.Info($"[Map3D] 오버레이 스냅샷 rev={_rev} 점 {points.Count} · 선 {lines.Count}");
    }

    /// <summary>마커의 3D 종류("pids" · "symbol" · "pidsGroup" · "line"). 대상이 아니면 null.</summary>
    public static string? KindOf(GMapMarker marker) => marker switch
    {
        GMapPidsMarker => Map3DOverlay.KindPids,
        GMapPidsGroupMarker => Map3DOverlay.KindPidsGroup,
        GMapLineMarker => Map3DOverlay.KindLine,
        GMapCustomMarker => Map3DOverlay.KindSymbol,
        _ => null,
    };

    /// <summary>모델에서 값을 복사해 3D 항목 JSON 을 만든다. 대상이 아니거나 해제된 마커면 null.</summary>
    public static JObject? BuildItem(GMapMarker marker)
    {
        if (marker is IEditableMarker { IsDisposed: true }) return null;
        switch (marker)
        {
            case GMapPidsMarker p:
            {
                var m = p.Model;
                return Map3DOverlay.ToJson(Map3DOverlay.Point(Map3DOverlay.KindPids, p.Id, m.Title, m.Latitude, m.Longitude, m.Altitude, m.Bearing,
                    m.DeviceType, HousingModels.DeviceKey(m.DeviceType, m.ModelVariant),
                    m.CompositeStatus, m.OperationState, m.DoorState,
                    m.ShowTitle, m.Visible, p.IsLayerEnabled, m.Zoom));
            }
            case GMapCustomMarker c:
            {
                var m = c.Model;
                return Map3DOverlay.ToJson(Map3DOverlay.Point(Map3DOverlay.KindSymbol, c.Id, m.Title, m.Latitude, m.Longitude, m.Altitude, m.Bearing,
                    null, null, null, m.OperationState, null,
                    m.ShowTitle, m.Visible, c.IsLayerEnabled, m.Zoom));
            }
            case GMapPidsGroupMarker g:
            {
                var m = g.Model;
                return Map3DOverlay.ToJson(Map3DOverlay.Line(Map3DOverlay.KindPidsGroup, g.Id, m.Title,
                    m.LinePoints?.Select(pt => (pt.Latitude, pt.Longitude)), m.IsClosedPath,
                    m.StrokeColor.ToHexString(), m.StrokeThickness,
                    hasFence: true, m.FenceHeightM, m.PostSpacingM, m.Render3D,
                    m.CompositeStatus, m.Visible, g.IsLayerEnabled, m.Zoom));
            }
            case GMapLineMarker l:
            {
                var m = l.Model;
                return Map3DOverlay.ToJson(Map3DOverlay.Line(Map3DOverlay.KindLine, l.Id, m.Title,
                    m.LinePoints?.Select(pt => (pt.Latitude, pt.Longitude)), m.IsClosedPath,
                    m.StrokeColor.ToHexString(), m.StrokeThickness,
                    hasFence: false, null, null, false,
                    null, m.Visible, l.IsLayerEnabled, m.Zoom));
            }
            default:
                return null;
        }
    }

    // ── 내부 ──

    /// <summary>대상이면 구독 · 기록하고 항목을 돌려준다.</summary>
    private JObject? Track(GMapMarker marker)
    {
        JObject? item;
        try { item = BuildItem(marker); }
        catch (Exception ex) { _log?.Warning($"[Map3D] 오버레이 항목 만들기 실패(건너뜀): {ex.Message}"); return null; }
        if (item == null) return null;
        if (_subscribed.Add(marker)) ((INotifyPropertyChanged)marker).PropertyChanged += OnMarkerPropertyChanged;
        _sent[Map3DOverlay.Key(item.Value<string>("kind")!, item.Value<int>("id"))] = item;
        return item;
    }

    private void OnMarkersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_map == null) return;
        if (e.Action == NotifyCollectionChangedAction.Reset) { SendSnapshot(); return; }
        if (e.OldItems != null)
            foreach (var marker in e.OldItems.OfType<GMapMarker>())
            {
                if (!_subscribed.Remove(marker)) continue;
                ((INotifyPropertyChanged)marker).PropertyChanged -= OnMarkerPropertyChanged;
                _dirty.Remove(marker);
                if (KindOf(marker) is not { } kind) continue;
                var id = ((IEditableMarker)marker).Id;
                if (_sent.Remove(Map3DOverlay.Key(kind, id))) _post(Map3DOverlay.RemoveJson(++_rev, kind, id));
            }
        if (e.NewItems != null)
            foreach (var marker in e.NewItems.OfType<GMapMarker>())
            {
                if (_subscribed.Contains(marker)) continue;
                if (Track(marker) is { } item) _post(Map3DOverlay.AddJson(++_rev, item));
            }
    }

    private void OnMarkerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not GMapMarker marker) return;
        var dispatcher = _map?.Dispatcher;
        if (dispatcher == null) return;
        if (!dispatcher.CheckAccess())
        {
            if (!dispatcher.HasShutdownStarted) dispatcher.BeginInvoke(new Action(() => MarkDirty(marker)));
            return;
        }
        MarkDirty(marker);
    }

    private void MarkDirty(GMapMarker marker)
    {
        if (_map == null || !_subscribed.Contains(marker)) return;
        _dirty.Add(marker);
        if (_timer == null)
        {
            _timer = new DispatcherTimer(DispatcherPriority.Background, _map.Dispatcher) { Interval = TimeSpan.FromMilliseconds(Map3DOverlay.CoalesceMs) };
            _timer.Tick += (_, __) => { _timer!.Stop(); Flush(); };
        }
        if (!_timer.IsEnabled) _timer.Start();
    }

    /// <summary>모아 둔 마커마다 직전에 보낸 것과 비교해 바뀐 필드만 patch.</summary>
    private void Flush()
    {
        if (_map == null) return;
        var dirty = _dirty.ToList();
        _dirty.Clear();
        foreach (var marker in dirty)
        {
            try
            {
                var item = BuildItem(marker);
                if (item == null) continue;
                string kind = item.Value<string>("kind")!;
                int id = item.Value<int>("id");
                string key = Map3DOverlay.Key(kind, id);
                if (!_sent.TryGetValue(key, out var previous))
                {
                    _sent[key] = item;
                    _post(Map3DOverlay.AddJson(++_rev, item));
                    continue;
                }
                if (Map3DOverlay.Diff(previous, item) is not { } set) continue;
                _sent[key] = item;
                _post(Map3DOverlay.PatchJson(++_rev, kind, id, set));
            }
            catch (Exception ex) { _log?.Warning($"[Map3D] 오버레이 patch 실패(건너뜀): {ex.Message}"); }
        }
    }
}
