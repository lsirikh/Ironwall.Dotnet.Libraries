using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GMap.NET;
using GMap.NET.MapProviders.Custom;
using GMap.NET.Projections;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Monitoring.Models.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/// <summary>
/// CustomMap 오버레이 렌더링 서비스
/// MBTiles 베이스맵 위에 CustomMap 타일을 WPF Canvas로 오버레이한다.
/// </summary>
public class CustomMapOverlayService : IDisposable
{
    private readonly ILogService? _log;
    private readonly LruTileCache _tileCache;
    private readonly CustomMapService _customMapService;
    private readonly Dictionary<int, OverlayMapState> _activeOverlays = new();

    private Canvas? _overlayCanvas;
    private GMapControl? _mainMap;

    public CustomMapOverlayService(
        ILogService? log,
        LruTileCache tileCache,
        CustomMapService customMapService)
    {
        _log = log;
        _tileCache = tileCache;
        _customMapService = customMapService;
    }

    /// <summary>MapView에서 Canvas 참조 전달 (OnViewAttached 시점)</summary>
    public void Initialize(Canvas overlayMapCanvas)
    {
        _overlayCanvas = overlayMapCanvas;
    }

    /// <summary>활성 오버레이 목록</summary>
    public IReadOnlyDictionary<int, OverlayMapState> ActiveOverlays => _activeOverlays;

    #region Activate / Deactivate

    /// <summary>CustomMap을 오버레이로 활성화</summary>
    public void ActivateOverlay(CustomMapModel customMap, GMapControl mainMap)
    {
        if (_overlayCanvas == null)
            throw new InvalidOperationException("Initialize()를 먼저 호출하세요.");

        _mainMap = mainMap;

        if (_activeOverlays.ContainsKey(customMap.Id))
        {
            _log?.Info($"[Overlay] 이미 활성화됨: {customMap.Name}");
            return;
        }

        // FileBasedCustomMapProvider 생성 (기존 CustomMapService 재활용)
        var provider = _customMapService.ActivateCustomMap(customMap);

        // 전용 Canvas 생성 (ClipToBounds=false — 부모 Canvas에서 크기 제한 없이 렌더링)
        var canvas = new Canvas
        {
            IsHitTestVisible = false,
        };

        // GeoBounds 계산
        var geoBounds = RectLatLng.Empty;
        if (customMap.MinLatitude.HasValue && customMap.MaxLatitude.HasValue &&
            customMap.MinLongitude.HasValue && customMap.MaxLongitude.HasValue)
        {
            geoBounds = new RectLatLng(
                customMap.MaxLatitude.Value,
                customMap.MinLongitude.Value,
                customMap.MaxLongitude.Value - customMap.MinLongitude.Value,
                customMap.MaxLatitude.Value - customMap.MinLatitude.Value);
        }

        var state = new OverlayMapState
        {
            CustomMap = customMap,
            Provider = provider,
            Canvas = canvas,
            IsVisible = true,
            Opacity = 1.0,
            MinZoom = customMap.MinZoomLevel,
            MaxZoom = customMap.MaxZoomLevel,
            ZOrder = _activeOverlays.Count,
            GeoBounds = geoBounds,
        };

        _activeOverlays[customMap.Id] = state;
        Panel.SetZIndex(canvas, state.ZOrder);
        _overlayCanvas.Children.Add(canvas);

        _log?.Info($"[Overlay] 활성화: {customMap.Name}, GeoBounds={geoBounds}, Zoom={state.MinZoom}~{state.MaxZoom}");

        // 즉시 렌더링 시도 (ViewArea가 유효한 경우에만)
        var viewArea = mainMap.ViewArea;
        if (viewArea.WidthLng > 0 && viewArea.HeightLat > 0)
        {
            RefreshVisibleTilesForState(state, mainMap);
            _log?.Info($"[Overlay] 즉시 렌더링: VisibleTiles={state.VisibleTiles.Count}");
        }
        else
        {
            _log?.Info($"[Overlay] 맵 미로드 상태 — 줌/패닝 이벤트에서 렌더링 대기");
        }
    }

    /// <summary>CustomMap 오버레이 비활성화</summary>
    public void DeactivateOverlay(int customMapId)
    {
        if (!_activeOverlays.TryGetValue(customMapId, out var state))
            return;

        // Canvas 조작은 UI 스레드에서 수행 (Dispose가 비-UI 스레드에서 호출될 수 있음)
        if (_overlayCanvas?.Dispatcher.CheckAccess() == true)
        {
            _overlayCanvas.Children.Remove(state.Canvas);
            state.Canvas.Children.Clear();
        }
        else
        {
            _overlayCanvas?.Dispatcher.Invoke(() =>
            {
                _overlayCanvas.Children.Remove(state.Canvas);
                state.Canvas.Children.Clear();
            });
        }

        state.VisibleTiles.Clear();

        // 캐시 정리
        _tileCache.Invalidate(customMapId);

        _activeOverlays.Remove(customMapId);
        _customMapService.DeactivateCustomMap(customMapId);

        _log?.Info($"[Overlay] 비활성화: {state.CustomMap.Name}");
    }

    #endregion

    #region Visibility / Opacity / Zoom

    public bool IsActive(int customMapId) => _activeOverlays.ContainsKey(customMapId);

    public void SetVisibility(int customMapId, bool isVisible)
    {
        if (!_activeOverlays.TryGetValue(customMapId, out var state)) return;
        state.IsVisible = isVisible;
        state.Canvas.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

        // Visible 전환 시 타일이 비어있을 수 있으므로 즉시 로드
        if (isVisible && _mainMap != null)
        {
            RefreshVisibleTilesForState(state, _mainMap);
        }

        _mainMap?.InvalidateVisual();
    }

    public void SetOpacity(int customMapId, double opacity)
    {
        if (!_activeOverlays.TryGetValue(customMapId, out var state)) return;
        state.Opacity = Math.Clamp(opacity, 0.0, 1.0);
        state.Canvas.Opacity = state.Opacity;
        _mainMap?.InvalidateVisual(); // OnRender 재호출 → Opacity 반영
    }

    public void SetZOrder(int customMapId, int zOrder)
    {
        if (!_activeOverlays.TryGetValue(customMapId, out var state)) return;
        state.ZOrder = zOrder;
        Panel.SetZIndex(state.Canvas, zOrder);
        _mainMap?.InvalidateVisual(); // OnRender 재호출 → OrderBy(ZIndex) 반영
    }

    public void SetZoomRange(int customMapId, int minZoom, int maxZoom)
    {
        if (!_activeOverlays.TryGetValue(customMapId, out var state)) return;
        state.MinZoom = minZoom;
        state.MaxZoom = maxZoom;
    }

    #endregion

    #region Tile Rendering

    /// <summary>
    /// 뷰포트 변경 시 호출 — 모든 활성 오버레이의 가시 타일 갱신
    /// </summary>
    public void RefreshVisibleTiles(GMapControl mainMap)
    {
        foreach (var state in _activeOverlays.Values)
        {
            RefreshVisibleTilesForState(state, mainMap);
        }
        // OnRender에서 DrawingContext로 렌더링하므로 다시 그리기 요청
        mainMap.InvalidateVisual();
    }

    private void RefreshVisibleTilesForState(OverlayMapState state, GMapControl mainMap)
    {
        var zoom = (int)mainMap.Zoom;

        // 1. Zoom 범위 + Visibility 체크
        if (!state.IsVisible || !state.IsZoomInRange(zoom))
        {
            // 줌 범위 밖 또는 숨김 상태 — 렌더링 생략
            state.Canvas.Visibility = Visibility.Collapsed;
            return;
        }

        state.Canvas.Visibility = Visibility.Visible;
        state.Canvas.Opacity = state.Opacity;

        // 2. 뷰포트와 CustomMap 범위의 교차 영역 계산
        var viewArea = mainMap.ViewArea;
        // 디버그 로그 제거 — 패닝마다 수십 회 호출됨

        if (state.GeoBounds == RectLatLng.Empty)
        {
            _log?.Warning($"[Overlay] GeoBounds is Empty — 타일 렌더링 불가");
            ClearTiles(state);
            return;
        }

        // 3. 교차 영역 수동 계산 (RectLatLng.Intersect가 잘못된 Empty 반환하는 GMap.NET 버그 회피)
        var projection = MercatorProjection.Instance;
        var effectiveArea = ManualIntersect(viewArea, state.GeoBounds);
        if (effectiveArea == RectLatLng.Empty)
        {
            // 교차 영역 없음 — 렌더링 생략
            ClearTiles(state);
            return;
        }

        var neededTiles = projection.GetAreaTileList(effectiveArea, zoom, 0);
        // 필요 타일 계산 완료

        // 4. 필요한 타일 키 Set
        var neededSet = new HashSet<TileKey>();
        foreach (var p in neededTiles)
            neededSet.Add(new TileKey(zoom, p.X, p.Y));

        // 5. 불필요한 타일 제거 (줌 변경 또는 뷰포트 밖)
        var toRemove = state.VisibleTiles.Keys
            .Where(k => !neededSet.Contains(k)).ToList();
        foreach (var key in toRemove)
        {
            state.Canvas.Children.Remove(state.VisibleTiles[key]);
            state.VisibleTiles.Remove(key);
        }

        // 6. 신규 타일 추가 + 기존 타일 좌표 갱신
        foreach (var tilePoint in neededTiles)
        {
            var key = new TileKey(zoom, tilePoint.X, tilePoint.Y);

            // 타일 좌상단 LatLng → 현재 뷰포트 기준 픽셀 좌표
            var tileLatLng = GetTileTopLeftLatLng(projection, zoom, tilePoint);
            var pixelPoint = mainMap.FromLatLngToLocal(tileLatLng);

            if (state.VisibleTiles.TryGetValue(key, out var existingImg))
            {
                // 기존 타일: 좌표만 갱신 (패닝 시 위치 동기화)
                Canvas.SetLeft(existingImg, pixelPoint.X);
                Canvas.SetTop(existingImg, pixelPoint.Y);
                continue;
            }

            // 신규 타일: 이미지 로드 + Canvas 배치
            var tileImage = LoadTileImageSource(state, key);
            if (tileImage == null) continue;

            var img = new System.Windows.Controls.Image
            {
                Source = tileImage,
                Width = 256,
                Height = 256,
                IsHitTestVisible = false,
            };

            Canvas.SetLeft(img, pixelPoint.X);
            Canvas.SetTop(img, pixelPoint.Y);
            state.Canvas.Children.Add(img);
            state.VisibleTiles[key] = img;
        }

        // RefreshTiles 완료
    }

    private void ClearTiles(OverlayMapState state)
    {
        state.Canvas.Children.Clear();
        state.VisibleTiles.Clear();
    }

    #endregion

    #region Tile Loading

    /// <summary>
    /// 타일 이미지 로드 — GMapImage.Img(WPF ImageSource)를 직접 사용
    /// PureImage.Data → 새 BitmapImage 변환 방식은 데이터 손실 발생하여 폐기
    /// </summary>
    private ImageSource? LoadTileImageSource(OverlayMapState state, TileKey key)
    {
        // 1. FileBasedCustomMapProvider에서 타일 로드
        var pureImage = state.Provider.GetTileImage(
            new GPoint(key.X, key.Y), key.Zoom);
        if (pureImage == null) return null;

        // 2. GMapImage.Img (WPF ImageSource) 직접 사용
        //    GMap.NET WPF: GMapImageProxy.FromStream → BitmapImage 생성 → GMapImage.Img에 저장
        //    이미 WPF용 ImageSource가 만들어져 있으므로 재변환 불필요
        if (pureImage is GMap.NET.WindowsPresentation.GMapImage gMapImage && gMapImage.Img != null)
        {
            return gMapImage.Img;
        }

        // 3. Fallback: Data에서 변환 (일반적으로 여기 오지 않음)
        return ConvertToBitmapImage(pureImage);
    }

    private static BitmapImage? ConvertToBitmapImage(PureImage pureImage)
    {
        try
        {
            if (pureImage.Data == null || pureImage.Data.Length == 0)
                return null;

            pureImage.Data.Position = 0;
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = pureImage.Data;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Coordinate Helpers

    /// <summary>
    /// RectLatLng 교차 영역 수동 계산
    /// GMap.NET RectLatLng: Lat=상단(북), Lng=좌측(서), HeightLat=아래로, WidthLng=오른쪽으로
    /// Bottom = Lat - HeightLat, Right = Lng + WidthLng
    /// </summary>
    private static RectLatLng ManualIntersect(RectLatLng a, RectLatLng b)
    {
        var aTop = a.Lat;
        var aBottom = a.Lat - a.HeightLat;
        var aLeft = a.Lng;
        var aRight = a.Lng + a.WidthLng;

        var bTop = b.Lat;
        var bBottom = b.Lat - b.HeightLat;
        var bLeft = b.Lng;
        var bRight = b.Lng + b.WidthLng;

        var top = Math.Min(aTop, bTop);
        var bottom = Math.Max(aBottom, bBottom);
        var left = Math.Max(aLeft, bLeft);
        var right = Math.Min(aRight, bRight);

        if (top <= bottom || right <= left)
            return RectLatLng.Empty;

        return new RectLatLng(top, left, right - left, top - bottom);
    }

    /// <summary>타일 좌표 (zoom, x, y) → 타일 좌상단 LatLng</summary>
    private static PointLatLng GetTileTopLeftLatLng(PureProjection projection, int zoom, GPoint tileXY)
    {
        var pixelX = tileXY.X * 256;
        var pixelY = tileXY.Y * 256;
        return projection.FromPixelToLatLng(pixelX, pixelY, zoom);
    }

    #endregion

    public void Dispose()
    {
        try
        {
            foreach (var id in _activeOverlays.Keys.ToList())
                DeactivateOverlay(id);
        }
        catch (Exception ex)
        {
            _log?.Error($"[Overlay] Dispose 중 오류: {ex.Message}");
        }

        _tileCache.Clear();
    }
}
