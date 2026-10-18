using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapImages;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using MaterialDesignThemes.Wpf;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;

/****************************************************************************
   Purpose      : 지도 마커 → 3D(map-3d) 오버레이 다리 — 3D 가 보이는 동안만 구독해 전체 스냅샷 1회 + 변경분(patch · add · remove)을 보낸다.
   Note         : ★ 대상 = 점 심볼(GMapPidsMarker "pids" · GMapCustomMarker "symbol" · GMapGeometricMarker "geometric" ·
                    GMapMilitarySymbolMarker "military" · GMapInfraMarker "infra") · 선(GMapPidsGroupMarker "pidsGroup" ·
                    GMapLineMarker "line" | 닫힌 선 "polygon") · 오버레이 이미지(GMapImageMarker · 지도 CustomImages(TIF 등) "image") ·
                    추적 타겟(GMapTrackingMarker + 같은 트랙의 GMapTrailMarker → track.upsert · track.remove).
                  ★ 상태 변경은 마커 PropertyChanged 하나로 받는다 — 심볼 모델 Update 를 마커가 받아 EventStatus · CompositeStatus ·
                    OperationState · DoorState(구역은 ActiveSensorDeviceIds 포함)로 다시 통지하므로 모델에 따로 붙지 않는다.
                    통지는 50 ms 모아(코얼레싱) 직전에 보낸 JSON 과 비교해 바뀐 필드만 patch 로 보낸다(선택 · 크기 등 무관한 통지는 버려진다).
                  ★ 점 아이콘(규약 v2): 2D 마커 Shape 를 VisualBrush → RenderTargetBitmap(2배) 으로 그린 PNG(overlay.icon, 내용 SHA-1 키).
                    시각 서명(Map3DOverlay.IconSignature — 상태 · 스타일 · 크기) → PNG 를 세션 동안 메모리에 두어 같은 서명은 다시 그리지 않는다.
                    그리기 실패면 아이콘 없이 보낸다.
                    VisualBrush 는 루트의 RenderTransform(2D 표시 회전 · 클릭 펄스)을 그리지 않는다 — 회전은 bearing 으로 따로 간다.
                  ★ 이미지 픽셀은 긴 변 2048 px 이하 JPEG(투명이면 PNG)로 한 번만 만들어(ImageSource 별 · 원본 파일 기준 디스크 캐시 Map3DImageCache)
                    overlay.imageData 로 보낸다. 가림(mask)은 기본 true.
                  ★ 3D 를 접으면 Pause(구독만 풂 · 보낸 기록 유지), 다시 펼치면 Resume(그 사이 바뀐 것만 rev 이어서). 새 페이지(ready)는 Start(전체 스냅샷).
                    지도 CustomImages 는 DB 키가 없어(대개 Id 0) 다리가 음수 id(-1, -2, …)를 붙인다 — 3D pick 은 FindCustomImage 로 되찾는다.
                  ★ overlay.icon · overlay.imageData 는 같은 key 를 한 번만 보내고, 스냅샷 때 기록을 비워 그 스냅샷에 쓰이는 것을 먼저 다시 보낸다.
                  ★ 어느 스레드에서 통지가 와도 UI 스레드에서 처리한다.
   Created On   : 2026-10-17 · Sensorway Co., Ltd.
****************************************************************************/
internal sealed class Map3DOverlayBridge
{
    /// <summary>아이콘 PNG 배율(2D DIP 대비 — 3D 화면에서 선명하게).</summary>
    private const double IconScale = 2.0;
    /// <summary>라이브 추적 마커 ZIndex(TrackingOverlayManager) — 이보다 크면 재생(Playback) 마커.</summary>
    private const int LiveTrackingZ = 2000;

    private static readonly FieldInfo? TrailPointsField = typeof(GMapTrailMarker).GetField("_points", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? TrackBearingField = typeof(GMapTrackingMarker).GetField("_worldBearing", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly Action<string> _post;
    private readonly ILogService? _log;
    /// <summary>구역 선 위 탐지 센서 구간(선 길이 비율) — 없으면 보내지 않는다(MapViewModel.EventFlyTo).</summary>
    private readonly Func<GMapPidsGroupMarker, (double From, double To)?>? _zoneSection;
    private readonly Dictionary<string, JObject> _sent = new();
    private readonly HashSet<INotifyPropertyChanged> _subscribed = new();
    private readonly HashSet<INotifyPropertyChanged> _dirty = new();
    /// <summary>3D 에 보낸 overlay.icon · overlay.imageData 키.</summary>
    private readonly HashSet<string> _sentBlobs = new();
    /// <summary>3D 에 보낸 추적 id.</summary>
    private readonly HashSet<string> _sentTracks = new();
    private readonly Dictionary<GMapCustomImage, int> _customImageIds = new();
    /// <summary>시각 서명 → 아이콘 PNG(세션 동안 — 다시 들어갈 때 다시 그리지 않는다).</summary>
    private readonly Dictionary<string, IconEntry> _icons = new();
    private readonly ConditionalWeakTable<ImageSource, Blob> _imageBlobs = new();
    private GMapCustomControl? _map;
    /// <summary>접어 둔 지도(Pause) — 보낸 기록은 이 지도 기준으로 남아 있다.</summary>
    private GMapCustomControl? _paused;
    private DispatcherTimer? _timer;
    private long _rev;
    private int _nextCustomImageId = -1;

    public Map3DOverlayBridge(Action<string> post, ILogService? log, Func<GMapPidsGroupMarker, (double From, double To)?>? zoneSection = null)
    {
        _post = post;
        _log = log;
        _zoneSection = zoneSection;
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
            map.CustomImages.CollectionChanged += OnCustomImagesChanged;
        }
        SendSnapshot();
    }

    /// <summary>구독을 모두 풀고 보낸 기록을 버린다(3D 페이지가 바뀜 · 상태를 잃음 — 다음엔 전체 스냅샷).</summary>
    public void Stop()
    {
        Detach();
        _paused = null;
        Unsubscribe();
    }

    /// <summary>
    /// 3D 를 접었다(페이지는 1×1 로 살아 있다) — 구독만 풀고 보낸 기록 · rev 는 남긴다. 2D 동안 아무것도 보내지 않는다.
    /// 다시 펼칠 때 <see cref="Resume"/> 가 그 사이 바뀐 것만 보낸다.
    /// </summary>
    public void Pause()
    {
        if (_map == null) return;
        var map = _map;
        Detach();
        _paused = map;
        foreach (var s in _subscribed) s.PropertyChanged -= OnSourcePropertyChanged;
        _subscribed.Clear();
        _dirty.Clear();
    }

    /// <summary>
    /// 3D 를 펼친다 — 이미 붙어 있으면(ready 직후 스냅샷을 보낸 그대로) 아무것도 하지 않고, 같은 지도에서 접혀 있었으면
    /// 그 사이 바뀐 것만(add · patch · remove, rev 이어서) 보낸다. 기록이 없으면 전체 스냅샷(<see cref="Start"/>).
    /// </summary>
    public void Resume(GMapCustomControl map)
    {
        if (ReferenceEquals(_map, map)) return;
        if (!ReferenceEquals(_paused, map)) { Start(map); return; }
        _paused = null;
        _map = map;
        map.Markers.CollectionChanged += OnMarkersChanged;
        map.CustomImages.CollectionChanged += OnCustomImagesChanged;

        var seen = new HashSet<string>();
        var tracks = new List<GMapTrackingMarker>();
        foreach (var marker in map.Markers.ToList())
        {
            if (marker is GMapTrackingMarker t) { Subscribe(t); tracks.Add(t); continue; }
            if (Resend(marker) is { } key) seen.Add(key);
        }
        foreach (var image in map.CustomImages.ToList())
            if (Resend(image) is { } key) seen.Add(key);
        foreach (var gone in _customImageIds.Keys.Where(i => !map.CustomImages.Contains(i)).ToList()) _customImageIds.Remove(gone);
        int removed = 0;
        foreach (var (key, item) in _sent.Where(kv => !seen.Contains(kv.Key)).ToList())
        {
            _sent.Remove(key);
            _post(Map3DOverlay.RemoveJson(++_rev, item.Value<string>("kind")!, item.Value<int>("id")));
            removed++;
        }
        var liveTracks = tracks.Select(TrackIdOf).ToHashSet();
        foreach (var id in _sentTracks.Where(id => !liveTracks.Contains(id)).ToList())
        {
            _sentTracks.Remove(id);
            _post(Map3DOverlay.TrackRemoveJson(id));
        }
        foreach (var t in tracks) PostTrack(t);
        _log?.Info($"[Map3D] 오버레이 이어 보내기 rev={_rev} 항목 {seen.Count} · 지움 {removed} · 추적 {_sentTracks.Count}");
    }

    private void Detach()
    {
        if (_map != null)
        {
            _map.Markers.CollectionChanged -= OnMarkersChanged;
            _map.CustomImages.CollectionChanged -= OnCustomImagesChanged;
        }
        _map = null;
        _timer?.Stop();
    }

    /// <summary>접혀 있던 항목 하나를 다시 구독하고 직전 기록과 비교해 보낸다 — 키(대상이 아니거나 실패면 null).</summary>
    private string? Resend(INotifyPropertyChanged source)
    {
        JObject? item;
        try { item = Build(source); }
        catch (Exception ex) { _log?.Warning($"[Map3D] 오버레이 항목 만들기 실패(건너뜀): {ex.Message}"); return null; }
        if (item == null) return null;
        Subscribe(source);
        return PostChange(item);
    }

    /// <summary>전체 스냅샷(3D ready 직후 · 재사용 펼침 · 3D 의 overlay.resync 요청).</summary>
    public void SendSnapshot()
    {
        if (_map == null) return;
        Unsubscribe();

        var points = new List<JObject>();
        var lines = new List<JObject>();
        var images = new List<JObject>();
        var tracks = new List<GMapTrackingMarker>();
        foreach (var marker in _map.Markers.ToList())
        {
            if (marker is GMapTrackingMarker t) { Subscribe(t); tracks.Add(t); continue; }
            var item = Track(marker);
            if (item == null) continue;
            string? kind = item.Value<string>("kind");
            (kind == Map3DOverlay.KindImage ? images : Map3DOverlay.IsLineKind(kind) ? lines : points).Add(item);
        }
        foreach (var image in _map.CustomImages.ToList())
            if (Track(image) is { } item) images.Add(item);
        _post(Map3DOverlay.SnapshotJson(++_rev, points, lines, images));
        foreach (var t in tracks) PostTrack(t);
        _log?.Info($"[Map3D] 오버레이 스냅샷 rev={_rev} 점 {points.Count} · 선 {lines.Count} · 이미지 {images.Count} · 추적 {_sentTracks.Count}");
    }

    /// <summary>마커의 3D 종류. 대상이 아니면 null(추적 마커는 track 메시지로 따로 간다).</summary>
    public static string? KindOf(GMapMarker marker) => marker switch
    {
        GMapPidsMarker => Map3DOverlay.KindPids,
        GMapPidsGroupMarker => Map3DOverlay.KindPidsGroup,
        GMapLineMarker l => l.Model?.IsClosedPath == true ? Map3DOverlay.KindPolygon : Map3DOverlay.KindLine,
        GMapGeometricMarker => Map3DOverlay.KindGeometric,
        GMapMilitarySymbolMarker => Map3DOverlay.KindMilitary,
        GMapInfraMarker => Map3DOverlay.KindInfra,
        GMapCustomMarker => Map3DOverlay.KindSymbol,
        GMapImageMarker => Map3DOverlay.KindImage,
        _ => null,
    };

    /// <summary>3D pick 의 음수 이미지 id → 지도 CustomImages 의 이미지(없으면 null).</summary>
    public GMapCustomImage? FindCustomImage(int id)
        => _customImageIds.FirstOrDefault(kv => kv.Value == id).Key;

    /// <summary>지도 CustomImages 이미지의 3D id(음수 — 스냅샷과 같은 값). 굽기 요청 overlays 의 id 로 쓴다.</summary>
    public int IdOf(GMapCustomImage image) => CustomImageId(image);

    /// <summary>
    /// 모델에서 값을 복사해 3D 항목 JSON 을 만든다(아이콘 · 이미지 픽셀 제외 — 그 둘은 다리 인스턴스가 붙인다).
    /// 대상이 아니거나 해제된 마커 · 이미지 마커면 null.
    /// </summary>
    public static JObject? BuildItem(GMapMarker marker) => BuildDto(marker) switch
    {
        Map3DOverlayPoint p => Map3DOverlay.ToJson(p),
        Map3DOverlayLine l => Map3DOverlay.ToJson(l),
        _ => null,
    };

    private static object? BuildDto(GMapMarker marker)
    {
        if (marker is IEditableMarker { IsDisposed: true }) return null;
        switch (marker)
        {
            case GMapPidsMarker p:
            {
                var m = p.Model;
                return Map3DOverlay.Point(Map3DOverlay.KindPids, p.Id, m.Title, m.Latitude, m.Longitude, m.Altitude, m.Bearing,
                    m.DeviceType, HousingModels.DeviceKey(m.DeviceType, m.ModelVariant),
                    m.CompositeStatus, m.OperationState, m.DoorState,
                    m.ShowTitle, m.Visible, p.IsLayerEnabled, m.Zoom) with
                {
                    Label = LabelOf(m),
                    Fov = m.DeviceType == EnumDeviceType.IpCamera   // 2D 도 카메라만 부채꼴을 그린다
                        ? Map3DOverlay.Fov(m.ShowFOV, m.DetectionRange, m.DetectionAngle, m.DetectionBearing, m.FOVColor.ToHexString(), m.FOVOpacity)
                        : null,
                };
            }
            case GMapGeometricMarker g: return PlainPoint(Map3DOverlay.KindGeometric, g.Id, g.Model, g.IsLayerEnabled);
            case GMapMilitarySymbolMarker ms: return PlainPoint(Map3DOverlay.KindMilitary, ms.Id, ms.Model, ms.IsLayerEnabled);
            case GMapInfraMarker i: return PlainPoint(Map3DOverlay.KindInfra, i.Id, i.Model, i.IsLayerEnabled);
            case GMapCustomMarker c: return PlainPoint(Map3DOverlay.KindSymbol, c.Id, c.Model, c.IsLayerEnabled);
            case GMapPidsGroupMarker g:
            {
                var m = g.Model;
                return Map3DOverlay.Line(Map3DOverlay.KindPidsGroup, g.Id, m.Title,
                    m.LinePoints?.Select(pt => (pt.Latitude, pt.Longitude)), m.IsClosedPath,
                    m.StrokeColor.ToHexString(), m.StrokeThickness,
                    hasFence: true, m.FenceHeightM, m.PostSpacingM, m.Render3D,
                    m.CompositeStatus, m.Visible, g.IsLayerEnabled, m.Zoom);
            }
            case GMapLineMarker l:
            {
                var m = l.Model;
                return Map3DOverlay.Line(KindOf(l)!, l.Id, m.Title,
                    m.LinePoints?.Select(pt => (pt.Latitude, pt.Longitude)), m.IsClosedPath,
                    m.StrokeColor.ToHexString(), m.StrokeThickness,
                    hasFence: false, null, null, false,
                    null, m.Visible, l.IsLayerEnabled, m.Zoom) with
                {
                    Style = Map3DOverlay.LineStyle(m.FillColor.ToHexString(), m.LineOpacity, m.LinePattern, m.ShowArrowHead),
                };
            }
            default:
                return null;
        }
    }

    /// <summary>장비가 아닌 점 심볼(도형 · 군사부호 · 시설물 · 기본) — 장비 종류 · 모델 · composite · door 는 비운다.</summary>
    private static Map3DOverlayPoint PlainPoint(string kind, int id, ISymbolModel m, bool layerEnabled)
        => Map3DOverlay.Point(kind, id, m.Title, m.Latitude, m.Longitude, m.Altitude, m.Bearing,
               null, null, null, m.OperationState, null,
               m.ShowTitle, m.Visible, layerEnabled, m.Zoom) with { Label = LabelOf(m) };

    private static Map3DOverlayLabelStyle LabelOf(ISymbolModel m)
        => Map3DOverlay.Label(m.Title, m.TitleSize, m.TitleColor.ToHexString(), m.TitleBackground.ToHexString(), m.TitleBold);

    /// <summary>아이콘 서명에 넣을 마커 스타일(항목 JSON 에 없는 시각 값).</summary>
    private static string StyleOf(GMapMarker marker)
    {
        string Base(ISymbolModel m) => $"{m.Width:0.##}|{m.Height:0.##}|{m.FillColor}|{m.StrokeColor}|{m.StrokeThickness:0.##}|{m.ShowShape}|{m.Category}";
        return marker switch
        {
            GMapPidsMarker p => $"{Base(p.Model)}|{p.Model.ModelVariant}|{p.IsSelected}",
            GMapGeometricMarker g => $"{Base(g.Model)}|{g.Model.ShapeType}|{g.Model.Opacity:0.###}|{g.IsSelected}",
            GMapMilitarySymbolMarker ms => $"{Base(ms.Model)}|{ms.Model.Affiliation}|{ms.Model.BattleDimension}|{ms.Model.StandardIdentity}|{ms.Model.UnitType}|{ms.Model.UnitSize}|{ms.Model.UnitDesignator}|{ms.Model.HigherFormation}|{ms.Model.CallSign}|{ms.Model.CountryCode}|{ms.IsSelected}",
            GMapInfraMarker i => $"{Base(i.Model)}|{i.Model.BuildingType}|{i.Model.BuildingUsage}|{i.Model.FloorCount}|{i.IsSelected}",
            GMapCustomMarker c => $"{Base(c.Model)}|{c.IsSelected}",
            _ => string.Empty,
        };
    }

    // ── 픽셀(아이콘 · 이미지) ──

    /// <summary>
    /// 2D 마커 시각(UIElement)을 PNG 로 — 자기 배치 크기(RenderSize) 영역만, scale 배 픽셀. 크기가 없거나 숨김이면 null.
    /// VisualBrush 는 부모 오프셋은 빼지만 루트 변환(표시 회전 · 클릭 펄스)은 그대로 그린다 — 그 역변환을 걸어 회전 없는 모양으로 그린다
    /// (회전은 bearing 으로 따로 간다).
    /// </summary>
    internal static byte[]? RenderIconPng(UIElement element, double scale = IconScale)
    {
        var size = element.RenderSize;
        if (element.Visibility != Visibility.Visible || size.Width < 1 || size.Height < 1) return null;
        var box = new Rect(size);
        var rootTransform = VisualTreeHelper.GetTransform(element);
        var matrix = rootTransform?.Value ?? Matrix.Identity;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            if (!matrix.IsIdentity && matrix.HasInverse)
            {
                box = rootTransform!.TransformBounds(box);   // 변환된 마커 칸(브러시 내용 좌표)
                matrix.Invert();
                dc.PushTransform(new MatrixTransform(matrix));
            }
            var brush = new VisualBrush(element)
            {
                Stretch = Stretch.Fill,
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = box,   // 부채꼴 등 밖으로 넘친 그림은 빼고 마커 칸만
            };
            dc.DrawRectangle(brush, null, box);
        }
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(size.Width * scale), (int)Math.Ceiling(size.Height * scale),
                                            96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return EncodePng(bitmap);
    }

    /// <summary>오버레이 이미지 픽셀 → PNG. 긴 변이 max 를 넘으면 비율을 지켜 줄인다.</summary>
    internal static byte[] EncodeImagePng(BitmapSource source, int max = Map3DOverlay.MaxImageSide)
        => EncodePng(Fit(source, max));

    /// <summary>긴 변이 max 를 넘으면 비율을 지켜 줄인 그림(이미 작으면 그대로).</summary>
    internal static BitmapSource Fit(BitmapSource source, int max)
    {
        var (w, h) = Map3DOverlay.FitLongSide(source.PixelWidth, source.PixelHeight, max);
        return w == source.PixelWidth && h == source.PixelHeight
            ? source
            : new TransformedBitmap(source, new ScaleTransform((double)w / source.PixelWidth, (double)h / source.PixelHeight));
    }

    internal static byte[] EncodePng(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    // ── 내부 ──

    private void Subscribe(INotifyPropertyChanged source)
    {
        if (_subscribed.Add(source)) source.PropertyChanged += OnSourcePropertyChanged;
    }

    private void Unsubscribe()
    {
        foreach (var s in _subscribed) s.PropertyChanged -= OnSourcePropertyChanged;
        _subscribed.Clear();
        _dirty.Clear();
        _sent.Clear();
        _sentBlobs.Clear();
        _sentTracks.Clear();
    }

    /// <summary>대상이면 구독 · 기록하고 항목을 돌려준다(필요한 아이콘 · 이미지 픽셀을 먼저 보낸다).</summary>
    private JObject? Track(INotifyPropertyChanged source)
    {
        JObject? item;
        try { item = Build(source); }
        catch (Exception ex) { _log?.Warning($"[Map3D] 오버레이 항목 만들기 실패(건너뜀): {ex.Message}"); return null; }
        if (item == null) return null;
        Subscribe(source);
        _sent[Map3DOverlay.Key(item.Value<string>("kind")!, item.Value<int>("id"))] = item;
        return item;
    }

    /// <summary>항목 JSON(아이콘 · 이미지 키 포함). 대상이 아니거나 픽셀이 없는 이미지면 null.</summary>
    private JObject? Build(object source)
    {
        switch (source)
        {
            case GMapImageMarker im:
            {
                if (im.IsDisposed || BlobOf(im.ImageSource) is not { } blob) return null;
                return Map3DOverlay.ToJson(Map3DOverlay.Image(im.Id, im.Title, im.Left, im.Top, im.Right, im.Bottom, im.Bearing, im.Opacity,
                    ((IEditableMarker)im).ZOrder, im.Visible, im.IsLayerEnabled, im.Zoom, mask: true, blob.Key));
            }
            case GMapCustomImage ci:
            {
                if (BlobOf(ci.Img) is not { } blob) return null;
                var b = ci.ImageBounds;
                return Map3DOverlay.ToJson(Map3DOverlay.Image(CustomImageId(ci), ci.Title, b.Left, b.Top, b.Right, b.Bottom, ci.UserRotation, ci.Opacity,
                    ci.Model.ZOrder, ci.Visibility, true, ci.Zoom, mask: true, blob.Key));
            }
            case GMapMarker marker:
            {
                var dto = BuildDto(marker);
                if (dto is Map3DOverlayLine line)
                    return Map3DOverlay.ToJson(marker is GMapPidsGroupMarker g && _zoneSection != null ? line with { ActiveSection = _zoneSection(g) } : line);
                if (dto is not Map3DOverlayPoint point) return null;
                var item = Map3DOverlay.ToJson(point);
                return IconOf(marker, item) is { } icon ? Map3DOverlay.ToJson(point with { Icon = icon }) : item;
            }
            default:
                return null;
        }
    }

    /// <summary>점 아이콘 — 같은 시각 서명은 세션 동안 메모리 캐시, 처음 보는 서명만 그린다. 처음 쓰는 키는 overlay.icon 으로 먼저 보낸다.</summary>
    private Map3DOverlayIcon? IconOf(GMapMarker marker, JObject item)
    {
        if (marker.Shape is not FrameworkElement element) return null;
        var size = element.RenderSize;
        string signature = Map3DOverlay.IconSignature(item, $"{StyleOf(marker)}|{element.IsVisible}", size.Width, size.Height);
        if (!_icons.TryGetValue(signature, out var entry))
        {
            byte[]? png = null;
            try { png = RenderIconPng(element); }
            catch (Exception ex) { _log?.Warning($"[Map3D] 아이콘 그리기 실패(원 표식): {ex.Message}"); }
            entry = png == null
                ? new IconEntry(null, null)
                : new IconEntry(new Map3DOverlayIcon(Map3DOverlay.HashKey(png), size.Width, size.Height), Map3DOverlay.PngDataUrl(png));
            _icons[signature] = entry;
        }
        if (entry.Icon is { } icon && _sentBlobs.Add(icon.Key)) _post(Map3DOverlay.IconJson(icon.Key, entry.DataUrl!));
        return entry.Icon;
    }

    /// <summary>
    /// 이미지 픽셀(ImageSource 별 1회 — 원본 파일이 있으면 디스크 캐시, Map3DImageCache). 처음 쓰는 키는 overlay.imageData 로 먼저 보낸다.
    /// 픽셀이 없으면 null.
    /// </summary>
    private Blob? BlobOf(ImageSource? source)
    {
        if (source is not BitmapSource bitmap) return null;
        if (!_imageBlobs.TryGetValue(source, out var blob))
        {
            var (bytes, mime) = Map3DImageCache.GetOrEncode(bitmap, Map3DImageCache.DefaultDirectory, _log);
            blob = new Blob(Map3DOverlay.HashKey(bytes), Map3DOverlay.DataUrl(bytes, mime));
            _imageBlobs.AddOrUpdate(source, blob);
        }
        if (_sentBlobs.Add(blob.Key)) _post(Map3DOverlay.ImageDataJson(blob.Key, blob.DataUrl));
        return blob;
    }

    private int CustomImageId(GMapCustomImage image)
    {
        if (!_customImageIds.TryGetValue(image, out int id)) _customImageIds[image] = id = _nextCustomImageId--;
        return id;
    }

    // ── 추적 ──

    /// <summary>추적 id — 카메라:트랙(재생 마커는 ":play" — 라이브와 같은 트랙이 겹치지 않게).</summary>
    private static string TrackIdOf(GMapTrackingMarker t) => $"{t.CameraId}:{t.TrackId}{(t.ZIndex > LiveTrackingZ ? ":play" : "")}";

    /// <summary>추적 타겟 upsert — 2D 에서 숨겨졌으면(최소 줌) remove. 색 = 2D 핀 색, 꼬리 = 같은 트랙 GMapTrailMarker 의 점.</summary>
    private void PostTrack(GMapTrackingMarker t)
    {
        string id = TrackIdOf(t);
        if (t.Shape is not Canvas canvas || canvas.Visibility != Visibility.Visible)
        {
            if (_sentTracks.Remove(id)) _post(Map3DOverlay.TrackRemoveJson(id));
            return;
        }
        string color = canvas.Children.OfType<PackIcon>().FirstOrDefault()?.Foreground is SolidColorBrush b
            ? $"#{b.Color.R:X2}{b.Color.G:X2}{b.Color.B:X2}" : "#95A5A6";
        var trail = _map?.Markers.OfType<GMapTrailMarker>()
            .Where(tr => tr.CameraId == t.CameraId && tr.TrackId == t.TrackId && tr.ZIndex < t.ZIndex)
            .OrderByDescending(tr => tr.ZIndex).FirstOrDefault();
        var points = trail != null && TrailPointsField?.GetValue(trail) is List<PointLatLng> list ? list.ToList() : new List<PointLatLng>();
        double bearing = TrackBearingField?.GetValue(t) is double d ? d : 0d;
        _post(Map3DOverlay.TrackUpsertJson(id, t.Position.Lat, t.Position.Lng, bearing, color, t.TrackId, points.Select(p => (p.Lat, p.Lng))));
        _sentTracks.Add(id);
    }

    // ── 통지 ──

    private void OnMarkersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_map == null) return;
        if (e.Action == NotifyCollectionChangedAction.Reset) { SendSnapshot(); return; }
        if (e.OldItems != null)
            foreach (var marker in e.OldItems.OfType<GMapMarker>())
            {
                if (!_subscribed.Remove(marker)) continue;
                ((INotifyPropertyChanged)marker).PropertyChanged -= OnSourcePropertyChanged;
                _dirty.Remove(marker);
                if (marker is GMapTrackingMarker t)
                {
                    if (_sentTracks.Remove(TrackIdOf(t))) _post(Map3DOverlay.TrackRemoveJson(TrackIdOf(t)));
                    continue;
                }
                if (KindOf(marker) is not { } kind) continue;
                var id = ((IEditableMarker)marker).Id;
                if (_sent.Remove(Map3DOverlay.Key(kind, id))) _post(Map3DOverlay.RemoveJson(++_rev, kind, id));
            }
        if (e.NewItems != null)
            foreach (var marker in e.NewItems.OfType<GMapMarker>())
            {
                if (_subscribed.Contains(marker)) continue;
                // 추적 마커는 꼬리(바로 뒤에 추가된다)가 붙은 뒤 모아서 보낸다
                if (marker is GMapTrackingMarker t) { Subscribe(t); MarkDirty(t); continue; }
                if (Track(marker) is { } item) _post(Map3DOverlay.AddJson(++_rev, item));
            }
    }

    private void OnCustomImagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_map == null) return;
        if (e.Action == NotifyCollectionChangedAction.Reset) { SendSnapshot(); return; }
        if (e.OldItems != null)
            foreach (var image in e.OldItems.OfType<GMapCustomImage>())
            {
                if (!_customImageIds.Remove(image, out int id)) continue;
                if (_subscribed.Remove(image)) image.PropertyChanged -= OnSourcePropertyChanged;
                _dirty.Remove(image);
                if (_sent.Remove(Map3DOverlay.Key(Map3DOverlay.KindImage, id))) _post(Map3DOverlay.RemoveJson(++_rev, Map3DOverlay.KindImage, id));
            }
        if (e.NewItems != null)
            foreach (var image in e.NewItems.OfType<GMapCustomImage>())
                if (!_subscribed.Contains(image) && Track(image) is { } item) _post(Map3DOverlay.AddJson(++_rev, item));
    }

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not INotifyPropertyChanged source) return;
        var dispatcher = _map?.Dispatcher;
        if (dispatcher == null) return;
        if (!dispatcher.CheckAccess())
        {
            if (!dispatcher.HasShutdownStarted) dispatcher.BeginInvoke(new Action(() => MarkDirty(source)));
            return;
        }
        MarkDirty(source);
    }

    private void MarkDirty(INotifyPropertyChanged source)
    {
        if (_map == null || !_subscribed.Contains(source)) return;
        _dirty.Add(source);
        if (_timer == null)
        {
            _timer = new DispatcherTimer(DispatcherPriority.Background, _map.Dispatcher) { Interval = TimeSpan.FromMilliseconds(Map3DOverlay.CoalesceMs) };
            _timer.Tick += (_, __) => { _timer!.Stop(); Flush(); };
        }
        if (!_timer.IsEnabled) _timer.Start();
    }

    /// <summary>모아 둔 항목마다 직전에 보낸 것과 비교해 바뀐 필드만 patch(추적은 upsert).</summary>
    private void Flush()
    {
        if (_map == null) return;
        var dirty = _dirty.ToList();
        _dirty.Clear();
        foreach (var source in dirty)
        {
            try
            {
                if (source is GMapTrackingMarker t) { PostTrack(t); continue; }
                if (Build(source) is { } item) PostChange(item);
            }
            catch (Exception ex) { _log?.Warning($"[Map3D] 오버레이 patch 실패(건너뜀): {ex.Message}"); }
        }
    }

    /// <summary>처음이면 add, 직전에 보낸 것과 다르면 바뀐 필드만 patch. 항목 키를 돌려준다.</summary>
    private string PostChange(JObject item)
    {
        string kind = item.Value<string>("kind")!;
        int id = item.Value<int>("id");
        string key = Map3DOverlay.Key(kind, id);
        if (!_sent.TryGetValue(key, out var previous))
        {
            _sent[key] = item;
            _post(Map3DOverlay.AddJson(++_rev, item));
        }
        else if (Map3DOverlay.Diff(previous, item) is { } set)
        {
            _sent[key] = item;
            _post(Map3DOverlay.PatchJson(++_rev, kind, id, set));
        }
        return key;
    }

    private sealed record IconEntry(Map3DOverlayIcon? Icon, string? DataUrl);
    private sealed record Blob(string Key, string DataUrl);
}
