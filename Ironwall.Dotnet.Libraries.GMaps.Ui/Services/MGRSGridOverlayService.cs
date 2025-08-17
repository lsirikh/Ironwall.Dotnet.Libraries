using CoordinateSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/// <summary>
/// MGRS 그리드 오버레이 서비스 - 고성능 캐싱 버전
/// 목적: 지도 위에 MGRS(Military Grid Reference System) 그리드를 효율적으로 표시
/// 최적화 기법: 캐싱, 화면 클리핑, 동적 LOD(Level of Detail)
/// </summary>
public class MGRSGridOverlayService
{
    #region - 성능 최적화를 위한 캐시 시스템 -

    /// <summary>
    /// 캐시 키 구조체 - 뷰 상태를 나타냄
    /// 동일한 뷰 상태에서는 캐시된 그리드 데이터를 재사용
    /// </summary>
    private struct GridCacheKey : IEquatable<GridCacheKey>
    {
        public double Top, Left, Width, Height; // 뷰 영역
        public int ZoomLevel;                   // 줌 레벨

        public bool Equals(GridCacheKey other)
        {
            // 미세한 차이는 무시하여 캐시 효율성 증대
            const double tolerance = 0.0001;
            return Math.Abs(Top - other.Top) < tolerance &&
                   Math.Abs(Left - other.Left) < tolerance &&
                   Math.Abs(Width - other.Width) < tolerance &&
                   Math.Abs(Height - other.Height) < tolerance &&
                   ZoomLevel == other.ZoomLevel;
        }

        public override bool Equals(object obj) => obj is GridCacheKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(
            Math.Round(Top, 4), Math.Round(Left, 4),
            Math.Round(Width, 4), Math.Round(Height, 4), ZoomLevel);
    }

    /// <summary>
    /// 그리드 선 데이터 구조체 - 지리 좌표로 저장하여 패닝 시에도 정확한 위치 유지
    /// </summary>
    private struct CachedGridLine
    {
        public PointLatLng StartGeo; // 지리 시작 좌표 (위경도)
        public PointLatLng EndGeo;   // 지리 끝 좌표 (위경도)
        public Pen LinePen;          // 그리기용 펜
        public bool IsMajorGrid;     // 주요 그리드(10km) 여부
    }

    /// <summary>
    /// MGRS 라벨 데이터 구조체 - 지리 좌표로 저장하여 패닝 시에도 정확한 위치 유지
    /// </summary>
    private struct CachedGridLabel
    {
        public PointLatLng GeoPosition;  // 지리 위치 (위경도)
        public FormattedText TextObject; // 미리 포맷된 텍스트 객체
        public string MGRSText;          // MGRS 텍스트
    }

    // 캐시 데이터 저장소
    private GridCacheKey _currentCacheKey;
    private List<CachedGridLine> _cachedGridLines = new();
    private List<CachedGridLabel> _cachedLabels = new();
    private DateTime _lastCacheTime = DateTime.MinValue;

    // 성능 설정값 - 패닝 최적화를 위한 조정
    private const double CACHE_VALID_SECONDS = 1.0;    // 캐시 유효시간 (1초로 증가)
    private const int MAX_GRID_LINES = 100;           // 최대 그리드 선 개수 증가 (패닝 대비)
    private const double SCREEN_MARGIN = 200;         // 화면 여유 공간 증가 (패닝 대비)

    #endregion

    #region - 생성자 및 초기화 -

    /// <summary>
    /// MGRSGridOverlayService 생성자
    /// 그리드 렌더링에 필요한 펜과 브러시 초기화
    /// </summary>
    /// <param name="log">로깅 서비스</param>
    public MGRSGridOverlayService(ILogService? log)
    {
        _log = log;

        // 일반 그리드 펜 (1km 간격) - 회색 점선, 50% 투명도
        var grayBrush = new SolidColorBrush(Colors.Gray) { Opacity = 0.5 };
        grayBrush.Freeze(); // 성능 최적화: 브러시 동결
        _gridPen = new Pen(grayBrush, 1) { DashStyle = DashStyles.Dot };
        _gridPen.Freeze();

        // 주요 그리드 펜 (10km 간격) - 진한 회색 실선, 70% 투명도
        var darkGrayBrush = new SolidColorBrush(Colors.DarkGray) { Opacity = 0.7 };
        darkGrayBrush.Freeze();
        _majorGridPen = new Pen(darkGrayBrush, 2);
        _majorGridPen.Freeze();

        // 라벨용 브러시 - 검은색
        _labelBrush = Brushes.Black;

        _log?.Info("MGRS 그리드 오버레이 서비스 초기화 완료");
    }

    #endregion

    #region - 메인 렌더링 메서드 -

    /// <summary>
    /// MGRS 그리드를 그리는 메인 메서드
    /// 지리 좌표 기반 캐시를 사용하여 패닝 시에도 정확한 그리드 위치 유지
    /// </summary>
    /// <param name="drawingContext">WPF 드로잉 컨텍스트</param>
    /// <param name="viewBounds">현재 뷰 영역 (위경도)</param>
    /// <param name="zoomLevel">현재 줌 레벨</param>
    /// <param name="mapControl">지도 컨트롤 (좌표 변환용)</param>
    public void DrawMGRSGrid(DrawingContext drawingContext, RectLatLng viewBounds, int zoomLevel, GMapControl mapControl)
    {
        try
        {
            // 성능 측정 시작
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _mapControl = mapControl;

            // 1단계: 줌 레벨에 따른 그리드 간격 결정
            var gridSpacing = GetOptimalGridSpacing(zoomLevel, viewBounds);
            if (gridSpacing <= 0)
            {
                // 그리드를 표시하지 않는 줌 레벨
                return;
            }

            // 2단계: 캐시 키 생성 및 유효성 검사
            var newCacheKey = CreateCacheKey(viewBounds, zoomLevel);
            var shouldRegenerateCache = ShouldRegenerateCache(newCacheKey);

            // 3단계: 필요한 경우 그리드 데이터 재생성 (지리 좌표로 저장)
            if (shouldRegenerateCache)
            {
                RegenerateGridCache(viewBounds, zoomLevel, gridSpacing);
                _currentCacheKey = newCacheKey;
                _lastCacheTime = DateTime.Now;

                _log?.Info($"MGRS 캐시 재생성: {_cachedGridLines.Count}개 선, {_cachedLabels.Count}개 라벨");
            }

            // 4단계: 캐시된 지리 좌표를 실시간 화면 좌표로 변환하여 렌더링
            RenderGridWithRealTimeTransform(drawingContext);

            // 성능 모니터링
            stopwatch.Stop();
            if (stopwatch.ElapsedMilliseconds > 16) // 60FPS 기준 (16ms)
            {
                _log?.Warning($"MGRS 렌더링 느림: {stopwatch.ElapsedMilliseconds}ms, 선:{_cachedGridLines.Count}개");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"MGRS 그리드 그리기 실패: {ex.Message}");
        }
    }

    #endregion

    #region - 캐시 관리 메서드 -

    /// <summary>
    /// 뷰 상태를 기반으로 캐시 키 생성
    /// </summary>
    private GridCacheKey CreateCacheKey(RectLatLng viewBounds, int zoomLevel)
    {
        return new GridCacheKey
        {
            Top = viewBounds.Top,
            Left = viewBounds.Left,
            Width = viewBounds.WidthLng,
            Height = viewBounds.HeightLat,
            ZoomLevel = zoomLevel
        };
    }

    /// <summary>
    /// 캐시 재생성 필요 여부 판단
    /// 패닝은 허용하되 줌이나 큰 뷰 변화만 캐시 재생성 트리거
    /// </summary>
    private bool ShouldRegenerateCache(GridCacheKey newKey)
    {
        // 첫 번째 실행이거나 캐시가 비어있음
        if (_cachedGridLines.Count == 0)
            return true;

        // 캐시 만료 시간 초과 (패닝용으로 더 길게 설정)
        if ((DateTime.Now - _lastCacheTime).TotalSeconds > CACHE_VALID_SECONDS * 2) // 1초로 연장
            return true;

        // 줌 레벨이 변경된 경우 (그리드 밀도가 달라짐)
        if (newKey.ZoomLevel != _currentCacheKey.ZoomLevel)
            return true;

        // 뷰 영역이 크게 변경된 경우만 재생성 (패닝은 무시)
        var deltaLat = Math.Abs(newKey.Top - _currentCacheKey.Top);
        var deltaLng = Math.Abs(newKey.Left - _currentCacheKey.Left);

        // 뷰 영역의 30% 이상 변경된 경우만 재생성 (패닝 관대하게)
        var thresholdLat = newKey.Height * 0.3;
        var thresholdLng = newKey.Width * 0.3;

        return deltaLat > thresholdLat || deltaLng > thresholdLng;
    }

    /// <summary>
    /// 그리드 캐시 데이터 재생성
    /// 화면 좌표로 미리 변환하여 렌더링 성능 극대화
    /// </summary>
    private void RegenerateGridCache(RectLatLng viewBounds, int zoomLevel, int gridSpacing)
    {
        // 기존 캐시 초기화
        _cachedGridLines.Clear();
        _cachedLabels.Clear();

        // 여유 공간을 포함한 확장된 뷰 영역 계산 (팬닝 대비)
        var expandedBounds = ExpandViewBounds(viewBounds, 0.2); // 20% 확장

        // 위경도 → UTM 좌표 변환
        var topLeft = ConvertToUTM(expandedBounds.LocationTopLeft);
        var bottomRight = ConvertToUTM(expandedBounds.LocationRightBottom);

        if (topLeft == null || bottomRight == null)
        {
            _log?.Warning("UTM 좌표 변환 실패");
            return;
        }

        // 세로선과 가로선 생성
        GenerateVerticalGridLines(topLeft, bottomRight, gridSpacing);
        GenerateHorizontalGridLines(topLeft, bottomRight, gridSpacing);

        // 줌 레벨이 충분히 높으면 MGRS 라벨 생성
        if (zoomLevel >= 12 && gridSpacing >= 10000)
        {
            GenerateGridLabels(topLeft, bottomRight, gridSpacing);
        }
    }

    /// <summary>
    /// 뷰 영역을 지정된 비율만큼 확장
    /// 팬닝 시 캐시 재사용률을 높이기 위함
    /// </summary>
    private RectLatLng ExpandViewBounds(RectLatLng bounds, double expansionRatio)
    {
        var expandLat = bounds.HeightLat * expansionRatio;
        var expandLng = bounds.WidthLng * expansionRatio;

        return new RectLatLng(
            bounds.Top + expandLat,
            bounds.Left - expandLng,
            bounds.WidthLng + (expandLng * 2),
            bounds.HeightLat + (expandLat * 2)
        );
    }

    #endregion

    #region - 그리드 생성 메서드 -

    /// <summary>
    /// 줌 레벨과 뷰 크기에 따른 최적의 그리드 간격 결정
    /// 성능과 가독성을 고려한 동적 LOD(Level of Detail) 적용
    /// </summary>
    private int GetOptimalGridSpacing(int zoomLevel, RectLatLng viewBounds)
    {
        // 화면에 표시될 대략적인 격자 수 계산
        var viewArea = viewBounds.WidthLng * viewBounds.HeightLat;

        return zoomLevel switch
        {
            // 매우 높은 줌: 100m 간격 (도시 블록 수준)
            >= 17 when viewArea < 0.0001 => 100,

            // 높은 줌: 1km 간격 (근린 구역 수준)  
            >= 15 when viewArea < 0.001 => 1000,
            >= 14 => 1000,

            // 중간 줌: 10km 간격 (도시 수준)
            >= 12 => 10000,

            // 낮은 줌: 100km 간격 (지역 수준)
            >= 10 => 100000,

            // 매우 낮은 줌: 그리드 숨김 (국가/대륙 수준)
            _ => 0
        };
    }

    /// <summary>
    /// 세로 그리드 선 생성 (동서 방향, Easting 기준)
    /// 지리 좌표로 저장하여 패닝 시에도 정확한 위치 유지
    /// </summary>
    private void GenerateVerticalGridLines(UTMCoordinate topLeft, UTMCoordinate bottomRight, int gridSpacing)
    {
        // 시작/끝 Easting 좌표를 격자 간격으로 정렬
        var startEasting = ((int)(topLeft.Easting / gridSpacing)) * gridSpacing;
        var endEasting = ((int)(bottomRight.Easting / gridSpacing) + 1) * gridSpacing;

        // 성능 제한: 너무 많은 선 방지
        var totalLines = (endEasting - startEasting) / gridSpacing;
        var lineStep = Math.Max(gridSpacing, (totalLines > MAX_GRID_LINES) ?
            ((totalLines / MAX_GRID_LINES) * gridSpacing) : gridSpacing);

        var lineCount = 0;
        for (int easting = startEasting; easting <= endEasting && lineCount < MAX_GRID_LINES; easting += lineStep)
        {
            // 10km 간격인지 확인하여 펜 선택
            var pen = (easting % 10000 == 0) ? _majorGridPen : _gridPen;
            var isMajor = (easting % 10000 == 0);

            // UTM → 위경도 변환
            var startGeo = ConvertToLatLng(new UTMCoordinate
            {
                Zone = topLeft.Zone,
                Hemisphere = topLeft.Hemisphere,
                Easting = easting,
                Northing = topLeft.Northing
            });

            var endGeo = ConvertToLatLng(new UTMCoordinate
            {
                Zone = topLeft.Zone,
                Hemisphere = topLeft.Hemisphere,
                Easting = easting,
                Northing = bottomRight.Northing
            });

            // 지리 좌표로 캐시 저장 (화면 좌표 변환은 렌더링 시에 실시간 처리)
            if (startGeo.HasValue && endGeo.HasValue)
            {
                _cachedGridLines.Add(new CachedGridLine
                {
                    StartGeo = startGeo.Value,
                    EndGeo = endGeo.Value,
                    LinePen = pen,
                    IsMajorGrid = isMajor
                });

                lineCount++;
            }
        }
    }

    /// <summary>
    /// 가로 그리드 선 생성 (남북 방향, Northing 기준)  
    /// 지리 좌표로 저장하여 패닝 시에도 정확한 위치 유지
    /// </summary>
    private void GenerateHorizontalGridLines(UTMCoordinate topLeft, UTMCoordinate bottomRight, int gridSpacing)
    {
        // 시작/끝 Northing 좌표를 격자 간격으로 정렬
        var startNorthing = ((int)(bottomRight.Northing / gridSpacing)) * gridSpacing;
        var endNorthing = ((int)(topLeft.Northing / gridSpacing) + 1) * gridSpacing;

        // 성능 제한 적용
        var totalLines = (endNorthing - startNorthing) / gridSpacing;
        var lineStep = Math.Max(gridSpacing, (totalLines > MAX_GRID_LINES) ?
            ((totalLines / MAX_GRID_LINES) * gridSpacing) : gridSpacing);

        var lineCount = 0;
        for (int northing = startNorthing; northing <= endNorthing && lineCount < MAX_GRID_LINES; northing += lineStep)
        {
            var pen = (northing % 10000 == 0) ? _majorGridPen : _gridPen;
            var isMajor = (northing % 10000 == 0);

            var startGeo = ConvertToLatLng(new UTMCoordinate
            {
                Zone = topLeft.Zone,
                Hemisphere = topLeft.Hemisphere,
                Easting = topLeft.Easting,
                Northing = northing
            });

            var endGeo = ConvertToLatLng(new UTMCoordinate
            {
                Zone = topLeft.Zone,
                Hemisphere = topLeft.Hemisphere,
                Easting = bottomRight.Easting,
                Northing = northing
            });

            // 지리 좌표로 캐시 저장
            if (startGeo.HasValue && endGeo.HasValue)
            {
                _cachedGridLines.Add(new CachedGridLine
                {
                    StartGeo = startGeo.Value,
                    EndGeo = endGeo.Value,
                    LinePen = pen,
                    IsMajorGrid = isMajor
                });

                lineCount++;
            }
        }
    }

    /// <summary>
    /// MGRS 격자 라벨 생성 (100km 격자 중심에 표시)
    /// 지리 좌표로 저장하여 패닝 시에도 정확한 위치 유지
    /// </summary>
    private void GenerateGridLabels(UTMCoordinate topLeft, UTMCoordinate bottomRight, int gridSpacing)
    {
        // 100km 격자에만 라벨 표시
        if (gridSpacing < 100000) return;

        var startEasting = ((int)(topLeft.Easting / 100000)) * 100000;
        var endEasting = ((int)(bottomRight.Easting / 100000) + 1) * 100000;
        var startNorthing = ((int)(bottomRight.Northing / 100000)) * 100000;
        var endNorthing = ((int)(topLeft.Northing / 100000) + 1) * 100000;

        // 라벨 개수 제한 (최대 9개: 3x3 격자)
        const int maxLabels = 9;
        var labelCount = 0;

        for (int easting = startEasting; easting < endEasting && labelCount < maxLabels; easting += 100000)
        {
            for (int northing = startNorthing; northing < endNorthing && labelCount < maxLabels; northing += 100000)
            {
                // 100km 격자의 중심점 계산
                var gridCenter = ConvertToLatLng(new UTMCoordinate
                {
                    Zone = topLeft.Zone,
                    Hemisphere = topLeft.Hemisphere,
                    Easting = easting + 50000,  // 중심점: +50km
                    Northing = northing + 50000 // 중심점: +50km
                });

                if (gridCenter.HasValue)
                {
                    // MGRS 좌표 문자열 생성
                    var mgrsText = ConvertToMGRS(gridCenter.Value);
                    if (!string.IsNullOrEmpty(mgrsText))
                    {
                        // FormattedText 미리 생성 (렌더링 시 성능 향상)
                        var formattedText = new FormattedText(
                            mgrsText,
                            System.Globalization.CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            new Typeface("Arial"),
                            11, // 약간 작은 폰트로 가독성 향상
                            _labelBrush,
                            96);

                        // 지리 좌표로 저장 (화면 좌표 변환은 렌더링 시에 실시간 처리)
                        _cachedLabels.Add(new CachedGridLabel
                        {
                            GeoPosition = gridCenter.Value,
                            TextObject = formattedText,
                            MGRSText = mgrsText
                        });

                        labelCount++;
                    }
                }
            }
        }
    }

    #endregion

    #region - 렌더링 메서드 -

    /// <summary>
    /// 캐시된 지리 좌표를 실시간으로 화면 좌표로 변환하여 렌더링
    /// 패닝 시에도 정확한 그리드 위치 유지
    /// </summary>
    private void RenderGridWithRealTimeTransform(DrawingContext drawingContext)
    {
        if (_mapControl == null) return;

        try
        {
            // 캐시된 그리드 선을 실시간 화면 좌표로 변환하여 렌더링
            foreach (var gridLine in _cachedGridLines)
            {
                // 실시간 좌표 변환 (패닝/줌 변화 반영)
                var startScreen = _mapControl.FromLatLngToLocal(gridLine.StartGeo);
                var endScreen = _mapControl.FromLatLngToLocal(gridLine.EndGeo);

                // 화면 영역과 교차하는 선만 그리기 (성능 최적화)
                if (IsLineIntersectingScreen(startScreen, endScreen))
                {
                    drawingContext.DrawLine(
                        gridLine.LinePen,
                        new Point(startScreen.X, startScreen.Y),
                        new Point(endScreen.X, endScreen.Y)
                    );
                }
            }

            // 캐시된 라벨을 실시간 화면 좌표로 변환하여 렌더링
            foreach (var label in _cachedLabels)
            {
                // 실시간 좌표 변환
                var screenPos = _mapControl.FromLatLngToLocal(label.GeoPosition);

                // 화면 영역 내에 있는 라벨만 그리기
                if (IsPointInScreen(screenPos, _mapControl.ActualWidth, _mapControl.ActualHeight))
                {
                    // 라벨을 중앙 정렬하여 표시
                    var centeredPosition = new Point(
                        screenPos.X - label.TextObject.Width / 2,
                        screenPos.Y - label.TextObject.Height / 2
                    );

                    drawingContext.DrawText(label.TextObject, centeredPosition);
                }
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"실시간 그리드 렌더링 실패: {ex.Message}");
        }
    }

    #endregion

    #region - 좌표 변환 메서드 -

    /// <summary>
    /// WGS-84 위경도 좌표를 UTM 좌표로 변환
    /// CoordinateSharp 라이브러리 사용
    /// </summary>
    private UTMCoordinate? ConvertToUTM(PointLatLng position)
{
    try
    {
        var coordinate = new Coordinate(position.Lat, position.Lng);
        return new UTMCoordinate
        {
            Zone = coordinate.UTM.LongZone,
            Hemisphere = coordinate.UTM.LatZone,
            Easting = coordinate.UTM.Easting,
            Northing = coordinate.UTM.Northing
        };
    }
    catch (Exception ex)
    {
        _log?.Error($"UTM 변환 실패 ({position.Lat:F6}, {position.Lng:F6}): {ex.Message}");
        return null;
    }
}

/// <summary>
/// UTM 좌표를 WGS-84 위경도 좌표로 변환
/// </summary>
private PointLatLng? ConvertToLatLng(UTMCoordinate utm)
{
    try
    {
        var utmCoord = new UniversalTransverseMercator($"{utm.Zone}{utm.Hemisphere}", utm.Easting, utm.Northing);
        var coord = UniversalTransverseMercator.ConvertUTMtoLatLong(utmCoord);
        return new PointLatLng(coord.Latitude.ToDouble(), coord.Longitude.ToDouble());
    }
    catch (Exception ex)
    {
        _log?.Error($"위경도 변환 실패 ({utm.Zone}{utm.Hemisphere} {utm.Easting:F0} {utm.Northing:F0}): {ex.Message}");
        return null;
    }
}

/// <summary>
/// 위경도 좌표를 MGRS 문자열로 변환
/// </summary>
private string ConvertToMGRS(PointLatLng position)
{
    try
    {
        var coordinate = new Coordinate(position.Lat, position.Lng);
        return coordinate.MGRS.ToString();
    }
    catch (Exception ex)
    {
        _log?.Error($"MGRS 변환 실패 ({position.Lat:F6}, {position.Lng:F6}): {ex.Message}");
        return string.Empty;
    }
}

#endregion

#region - 유틸리티 메서드 -

/// <summary>
/// 선이 화면 영역과 교차하는지 확인
/// 화면 밖의 불필요한 선을 제거하여 렌더링 성능 향상
/// </summary>
private bool IsLineIntersectingScreen(GPoint start, GPoint end)
{
    if (_mapControl == null) return false;

    var width = _mapControl.ActualWidth;
    var height = _mapControl.ActualHeight;

    // 여유 공간을 포함한 확장된 화면 영역
    var minX = -SCREEN_MARGIN;
    var maxX = width + SCREEN_MARGIN;
    var minY = -SCREEN_MARGIN;
    var maxY = height + SCREEN_MARGIN;

    // 선의 시작점이나 끝점이 확장된 화면 영역 내에 있으면 교차로 판단
    return (start.X >= minX && start.X <= maxX && start.Y >= minY && start.Y <= maxY) ||
           (end.X >= minX && end.X <= maxX && end.Y >= minY && end.Y <= maxY) ||
           // 또는 선이 화면을 관통하는 경우 (간단한 AABB 테스트)
           (Math.Min(start.X, end.X) <= maxX && Math.Max(start.X, end.X) >= minX &&
            Math.Min(start.Y, end.Y) <= maxY && Math.Max(start.Y, end.Y) >= minY);
}

/// <summary>
/// 점이 화면 영역 내에 있는지 확인 (라벨 표시용)
/// </summary>
private bool IsPointInScreen(GPoint point, double width, double height)
{
    return point.X >= -SCREEN_MARGIN && point.X <= width + SCREEN_MARGIN &&
           point.Y >= -SCREEN_MARGIN && point.Y <= height + SCREEN_MARGIN;
}

#endregion

#region - 속성 및 필드 -

/// <summary>
/// 일반 그리드용 펜 (1km 간격, 회색 점선)
/// </summary>
private readonly Pen _gridPen;

/// <summary>
/// 주요 그리드용 펜 (10km 간격, 진한 회색 실선)
/// </summary>
private readonly Pen _majorGridPen;

/// <summary>
/// 라벨용 브러시 (검은색)
/// </summary>
private readonly Brush _labelBrush;

/// <summary>
/// 로깅 서비스
/// </summary>
private readonly ILogService? _log;

/// <summary>
/// 지도 컨트롤 참조 (좌표 변환용)
/// </summary>
private GMapControl? _mapControl;

#endregion

#region - 공개 유틸리티 메서드 -

/// <summary>
/// 현재 캐시 상태 정보를 반환 (디버깅 및 모니터링용)
/// </summary>
/// <returns>캐시 통계 정보</returns>
public (int GridLineCount, int LabelCount, DateTime LastCacheTime, bool IsCacheValid) GetCacheStatistics()
{
    var isValid = (DateTime.Now - _lastCacheTime).TotalSeconds <= CACHE_VALID_SECONDS;
    return (_cachedGridLines.Count, _cachedLabels.Count, _lastCacheTime, isValid);
}

/// <summary>
/// 캐시를 강제로 무효화 (지도 설정 변경 시 사용)
/// </summary>
public void InvalidateCache()
{
    _cachedGridLines.Clear();
    _cachedLabels.Clear();
    _lastCacheTime = DateTime.MinValue;
    _log?.Info("MGRS 그리드 캐시 무효화됨");
}

/// <summary>
/// 메모리 사용량 최적화를 위한 캐시 정리
/// 장시간 사용하지 않은 경우 메모리 해제
/// </summary>
public void CleanupCache()
{
    const double maxIdleMinutes = 5.0; // 5분 이상 미사용 시 정리

    if ((DateTime.Now - _lastCacheTime).TotalMinutes > maxIdleMinutes)
    {
        var beforeCount = _cachedGridLines.Count + _cachedLabels.Count;

        _cachedGridLines.Clear();
        _cachedLabels.Clear();
        _cachedGridLines.TrimExcess();
        _cachedLabels.TrimExcess();

        _log?.Info($"MGRS 캐시 정리 완료: {beforeCount}개 항목 제거");

        // 가비지 컬렉션 힌트 (필요한 경우에만)
        if (beforeCount > 100)
        {
            GC.Collect(0, GCCollectionMode.Optimized);
        }
    }
}

/// <summary>
/// 특정 위치의 MGRS 좌표 문자열 반환 (외부 호출용)
/// </summary>
/// <param name="position">위경도 좌표</param>
/// <returns>MGRS 좌표 문자열 (예: "52S CG 12345 67890")</returns>
public string GetMGRSStringAt(PointLatLng position)
{
    return ConvertToMGRS(position);
}

/// <summary>
/// 현재 뷰에서 권장되는 그리드 간격 반환 (정보 표시용)
/// </summary>
/// <param name="zoomLevel">줌 레벨</param>
/// <param name="viewBounds">뷰 영역</param>
/// <returns>그리드 간격 (미터 단위)</returns>
public int GetRecommendedGridSpacing(int zoomLevel, RectLatLng viewBounds)
{
    return GetOptimalGridSpacing(zoomLevel, viewBounds);
}

    #endregion
}


/// <summary>
/// UTM 좌표 데이터 클래스
/// Universal Transverse Mercator 좌표계 정보를 저장
/// </summary>
public class UTMCoordinate
{
    /// <summary>
    /// UTM 존 번호 (1-60)
    /// </summary>
    public int Zone { get; set; }

    /// <summary>
    /// 반구 정보 ('N' 또는 'S')
    /// 한국은 항상 'N' (북반구)
    /// </summary>
    public string? Hemisphere { get; set; }

    /// <summary>
    /// 동서방향 좌표 (Easting, 미터 단위)
    /// UTM 존의 중앙 경선으로부터 동쪽 거리
    /// </summary>
    public double Easting { get; set; }

    /// <summary>
    /// 남북방향 좌표 (Northing, 미터 단위)
    /// 적도로부터 북쪽 거리 (북반구 기준)
    /// </summary>
    public double Northing { get; set; }

    /// <summary>
    /// UTM 좌표의 문자열 표현
    /// </summary>
    /// <returns>형식화된 UTM 좌표 문자열</returns>
    public override string ToString()
    {
        return $"UTM {Zone}{Hemisphere} {Easting:F0}mE {Northing:F0}mN";
    }
}