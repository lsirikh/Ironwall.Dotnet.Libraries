using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System.Diagnostics;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/31/2025 6:33:23 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 개선된 ImageModel 확장 메서드 - 정확한 좌표 계산
/// </summary>
public static class ImageModelExtensions
{
    #region 기본 속성 접근자 (기존 유지)

    public static PointLatLng TopLeft(this IImageModel model)
        => new PointLatLng(model.Top, model.Left);

    public static PointLatLng TopRight(this IImageModel model)
        => new PointLatLng(model.Top, model.Right);

    public static PointLatLng BottomLeft(this IImageModel model)
        => new PointLatLng(model.Bottom, model.Left);

    public static PointLatLng BottomRight(this IImageModel model)
        => new PointLatLng(model.Bottom, model.Right);

    public static PointLatLng Center(this IImageModel model)
        => new PointLatLng((model.Top + model.Bottom) / 2.0, (model.Left + model.Right) / 2.0);

    public static RectLatLng ToRect(this IImageModel model)
        => new RectLatLng(model.Top, model.Left, model.Right - model.Left, model.Top - model.Bottom);

    public static void Deconstruct(this IImageModel model, RectLatLng rect)
    {
        model.Top = rect.Top;
        model.Bottom = rect.Bottom;
        model.Left = rect.Left;
        model.Right = rect.Right;
    }
    #endregion
    #region GMapControl 통합 메서드

    /// <summary>
    /// GMapControl과 완전히 동기화된 경계 계산 (가장 정확한 방법)
    /// </summary>
    public static void SyncWithGMapControl(
        this IImageModel model,
        GMapControl mapControl,
        PointLatLng centerPosition)
    {
        if (mapControl == null) return;

        try
        {
            // 1. 지리적 좌표를 화면 좌표로 변환
            var screenCenter = mapControl.FromLatLngToLocal(centerPosition);

            // 2. GMapControl의 실제 변환을 사용하여 경계 계산
            model.CalculateBoundsFromScreen(mapControl, screenCenter.X, screenCenter.Y, model.Rotation);

            _log?.Info($"GMapControl 동기화 완료: Center({centerPosition}), Screen({screenCenter})");
        }
        catch (Exception ex)
        {
            _log?.Error($"GMapControl 동기화 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// Adorner 편집 후 GMapControl과 재동기화
    /// </summary>
    public static void ResyncAfterAdornerEdit(
        this IImageModel model,
        GMap.NET.WindowsPresentation.GMapControl mapControl,
        double newScreenLeft, double newScreenTop,
        double newScreenWidth, double newScreenHeight)
    {
        if (mapControl == null) return;

        try
        {
            // 편집된 화면 좌표들을 지리적 좌표로 변환
            var topLeft = mapControl.FromLocalToLatLng((int)newScreenLeft, (int)newScreenTop);
            var bottomRight = mapControl.FromLocalToLatLng(
                (int)(newScreenLeft + newScreenWidth),
                (int)(newScreenTop + newScreenHeight));

            // Model 업데이트
            model.Left = topLeft.Lng;
            model.Top = topLeft.Lat;
            model.Right = bottomRight.Lng;
            model.Bottom = bottomRight.Lat;

            // 중심점 및 크기 재계산
            model.Latitude = (model.Top + model.Bottom) / 2.0;
            model.Longitude = (model.Left + model.Right) / 2.0;
            model.Width = newScreenWidth;
            model.Height = newScreenHeight;

            _log?.Info($"Adorner 편집 후 재동기화: {model.BoundsInfo()}");
        }
        catch (Exception ex)
        {
            _log?.Error($"Adorner 편집 후 재동기화 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 현재 ViewArea와의 교집영역 계산 (가시성 판단용)
    /// </summary>
    public static bool IsVisibleInViewArea(
        this IImageModel model,
        GMap.NET.WindowsPresentation.GMapControl mapControl)
    {
        if (mapControl == null) return false;

        try
        {
            var viewArea = mapControl.ViewArea;
            var imageRect = model.ToRect();

            return imageRect.IntersectsWith(viewArea);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 현재 줌 레벨에서의 화면 크기 계산
    /// </summary>
    public static (double screenWidth, double screenHeight) GetScreenSize(
        this IImageModel model,
        GMap.NET.WindowsPresentation.GMapControl mapControl)
    {
        if (mapControl == null) return (0, 0);

        try
        {
            var topLeft = mapControl.FromLatLngToLocal(model.TopLeft());
            var bottomRight = mapControl.FromLatLngToLocal(model.BottomRight());

            return (
                Math.Abs(bottomRight.X - topLeft.X),
                Math.Abs(bottomRight.Y - topLeft.Y)
            );
        }
        catch
        {
            return (0, 0);
        }
    }

    #endregion

    private static ILogService? _log = null; // 로깅용 (필요시 IoC에서 가져와야 함)

    #region 정확한 좌표 계산 메서드

    /// <summary>
    /// 중심점, 크기, 줌레벨을 기반으로 정확한 경계 좌표 계산 (ImageModel의 Width/Height 활용)
    /// </summary>
    /// <param name="model">이미지 모델</param>
    /// <param name="centerLat">중심점 위도</param>
    /// <param name="centerLng">중심점 경도</param>
    /// <param name="zoomLevel">현재 줌 레벨</param>
    /// <param name="rotationDegrees">회전각 (도)</param>
    public static void CalculateBoundsFromCenter(
        this IImageModel model,
        double centerLat,
        double centerLng,
        int zoomLevel,
        double rotationDegrees = 0.0)
    {
        // 1. Model에서 이미지 크기 가져오기
        int imageWidthPixels = (int)model.Width;
        int imageHeightPixels = (int)model.Height;

        // 2. 줌 레벨에 따른 해상도 계산 (미터/픽셀)
        double metersPerPixel = GetMetersPerPixel(centerLat, zoomLevel);

        // 3. 이미지 크기를 지리적 거리로 변환
        double imageWidthMeters = imageWidthPixels * metersPerPixel;
        double imageHeightMeters = imageHeightPixels * metersPerPixel;

        // 4. 지리적 좌표계에서의 크기 계산
        double latDegreesPerMeter = 1.0 / 111319.9; // 위도 1도 ≈ 111.32km
        double lngDegreesPerMeter = 1.0 / (111319.9 * Math.Cos(DegreesToRadians(centerLat)));

        double halfWidthDegrees = (imageWidthMeters / 2.0) * lngDegreesPerMeter;
        double halfHeightDegrees = (imageHeightMeters / 2.0) * latDegreesPerMeter;

        if (Math.Abs(rotationDegrees) < 0.1) // 회전 없음
        {
            // 5. 단순한 경계 계산
            model.Left = centerLng - halfWidthDegrees;
            model.Right = centerLng + halfWidthDegrees;
            model.Top = centerLat + halfHeightDegrees;
            model.Bottom = centerLat - halfHeightDegrees;
        }
        else // 회전 있음
        {
            // 6. 회전된 이미지의 경계 계산
            CalculateRotatedBounds(model, centerLat, centerLng,
                                 halfWidthDegrees, halfHeightDegrees, rotationDegrees);
        }

        // 7. 중심점 좌표도 업데이트
        model.Latitude = centerLat;
        model.Longitude = centerLng;
        model.Rotation = rotationDegrees;
    }

    /// <summary>
    /// GMapControl의 화면 좌표를 이용한 정확한 경계 계산 (ImageModel의 Width/Height 활용)
    /// </summary>
    /// <param name="model">이미지 모델</param>
    /// <param name="mapControl">GMap 컨트롤</param>
    /// <param name="screenCenterX">화면 중심 X</param>
    /// <param name="screenCenterY">화면 중심 Y</param>
    /// <param name="rotationDegrees">회전각</param>
    public static void CalculateBoundsFromScreen(
        this IImageModel model,
        GMap.NET.WindowsPresentation.GMapControl mapControl,
        double screenCenterX,
        double screenCenterY,
        double rotationDegrees = 0.0)
    {
        // 1. Model에서 이미지 크기 가져오기
        int imageWidthPixels = (int)model.Width;
        int imageHeightPixels = (int)model.Height;

        // 2. 화면 좌표를 지리적 좌표로 변환
        var centerPoint = mapControl.FromLocalToLatLng((int)screenCenterX, (int)screenCenterY);

        // 3. 이미지 모서리의 화면 좌표 계산
        double halfWidth = imageWidthPixels / 2.0;
        double halfHeight = imageHeightPixels / 2.0;

        if (Math.Abs(rotationDegrees) < 0.1) // 회전 없음
        {
            // 단순한 모서리 계산
            var topLeft = mapControl.FromLocalToLatLng(
                (int)(screenCenterX - halfWidth),
                (int)(screenCenterY - halfHeight));
            var bottomRight = mapControl.FromLocalToLatLng(
                (int)(screenCenterX + halfWidth),
                (int)(screenCenterY + halfHeight));

            model.Left = topLeft.Lng;
            model.Top = topLeft.Lat;
            model.Right = bottomRight.Lng;
            model.Bottom = bottomRight.Lat;
        }
        else // 회전 있음
        {
            // 회전된 모서리들의 지리적 좌표 계산
            var corners = CalculateRotatedScreenCorners(
                screenCenterX, screenCenterY,
                halfWidth, halfHeight, rotationDegrees);

            // 각 모서리를 지리적 좌표로 변환
            var geoCorners = new PointLatLng[4];
            for (int i = 0; i < 4; i++)
            {
                geoCorners[i] = mapControl.FromLocalToLatLng(
                    (int)corners[i].X, (int)corners[i].Y);
            }

            // 경계 상자 계산
            model.Left = Math.Min(Math.Min(geoCorners[0].Lng, geoCorners[1].Lng),
                                Math.Min(geoCorners[2].Lng, geoCorners[3].Lng));
            model.Right = Math.Max(Math.Max(geoCorners[0].Lng, geoCorners[1].Lng),
                                 Math.Max(geoCorners[2].Lng, geoCorners[3].Lng));
            model.Top = Math.Max(Math.Max(geoCorners[0].Lat, geoCorners[1].Lat),
                               Math.Max(geoCorners[2].Lat, geoCorners[3].Lat));
            model.Bottom = Math.Min(Math.Min(geoCorners[0].Lat, geoCorners[1].Lat),
                                  Math.Min(geoCorners[2].Lat, geoCorners[3].Lat));
        }

        model.Latitude = centerPoint.Lat;
        model.Longitude = centerPoint.Lng;
        model.Rotation = rotationDegrees;
    }

    /// <summary>
    /// 실제 지리적 좌표를 이용한 정확한 경계 업데이트
    /// (GeoTIFF 등 실제 지리참조 데이터용)
    /// </summary>
    public static void UpdateFromGeoTransform(
        this IImageModel model,
        double[] geoTransform, // GDAL 스타일 GeoTransform
        int imageWidth,
        int imageHeight)
    {
        if (geoTransform?.Length != 6)
            throw new ArgumentException("GeoTransform must have 6 elements");

        // GDAL GeoTransform: [originX, pixelWidth, 0, originY, 0, -pixelHeight]
        double originX = geoTransform[0];  // 좌상단 X 좌표
        double pixelWidth = geoTransform[1];  // 픽셀 너비 (경도 방향)
        double originY = geoTransform[3];  // 좌상단 Y 좌표  
        double pixelHeight = -geoTransform[5]; // 픽셀 높이 (위도 방향, 보통 음수)

        // 모서리 좌표 계산
        model.Left = originX;  // 좌상단 X
        model.Top = originY;   // 좌상단 Y
        model.Right = originX + (imageWidth * pixelWidth);   // 우하단 X
        model.Bottom = originY - (imageHeight * pixelHeight); // 우하단 Y

        // 중심점 계산
        model.Latitude = (model.Top + model.Bottom) / 2.0;
        model.Longitude = (model.Left + model.Right) / 2.0;

        model.HasGeoReference = true;
        model.Width = imageWidth;
        model.Height = imageHeight;
    }

    #endregion

    #region 헬퍼 메서드

    /// <summary>
    /// 줌 레벨과 위도에 따른 미터/픽셀 계산
    /// </summary>
    private static double GetMetersPerPixel(double latitude, int zoomLevel)
    {
        // 웹 메르카토르 투영에서 적도에서의 미터/픽셀
        double equatorMetersPerPixel = 40075016.686 / Math.Pow(2, zoomLevel + 8);

        // 위도에 따른 보정 (메르카토르 투영에서 위도가 높아질수록 축척 변화)
        return equatorMetersPerPixel * Math.Cos(DegreesToRadians(latitude));
    }

    /// <summary>
    /// 회전된 이미지의 경계 계산
    /// </summary>
    private static void CalculateRotatedBounds(
        IImageModel model,
        double centerLat, double centerLng,
        double halfWidthDegrees, double halfHeightDegrees,
        double rotationDegrees)
    {
        double rotationRad = DegreesToRadians(rotationDegrees);

        // 회전 전 4개 모서리 (중심 기준 상대 좌표)
        var corners = new[]
        {
            new { X = -halfWidthDegrees, Y = halfHeightDegrees },   // 좌상단
            new { X = halfWidthDegrees, Y = halfHeightDegrees },    // 우상단  
            new { X = halfWidthDegrees, Y = -halfHeightDegrees },   // 우하단
            new { X = -halfWidthDegrees, Y = -halfHeightDegrees }   // 좌하단
        };

        // 회전 변환 적용
        double minLng = double.MaxValue, maxLng = double.MinValue;
        double minLat = double.MaxValue, maxLat = double.MinValue;

        foreach (var corner in corners)
        {
            // 2D 회전 변환
            double rotatedX = corner.X * Math.Cos(rotationRad) - corner.Y * Math.Sin(rotationRad);
            double rotatedY = corner.X * Math.Sin(rotationRad) + corner.Y * Math.Cos(rotationRad);

            // 절대 좌표로 변환
            double lng = centerLng + rotatedX;
            double lat = centerLat + rotatedY;

            // 경계 상자 업데이트
            minLng = Math.Min(minLng, lng);
            maxLng = Math.Max(maxLng, lng);
            minLat = Math.Min(minLat, lat);
            maxLat = Math.Max(maxLat, lat);
        }

        model.Left = minLng;
        model.Right = maxLng;
        model.Bottom = minLat;
        model.Top = maxLat;
    }

    /// <summary>
    /// 화면 좌표에서 회전된 모서리 계산
    /// </summary>
    private static System.Windows.Point[] CalculateRotatedScreenCorners(
        double centerX, double centerY,
        double halfWidth, double halfHeight,
        double rotationDegrees)
    {
        double rotationRad = DegreesToRadians(rotationDegrees);

        var corners = new[]
        {
            new { X = -halfWidth, Y = -halfHeight },  // 좌상단
            new { X = halfWidth, Y = -halfHeight },   // 우상단
            new { X = halfWidth, Y = halfHeight },    // 우하단  
            new { X = -halfWidth, Y = halfHeight }    // 좌하단
        };

        var result = new System.Windows.Point[4];
        for (int i = 0; i < 4; i++)
        {
            double rotatedX = corners[i].X * Math.Cos(rotationRad) - corners[i].Y * Math.Sin(rotationRad);
            double rotatedY = corners[i].X * Math.Sin(rotationRad) + corners[i].Y * Math.Cos(rotationRad);

            result[i] = new System.Windows.Point(
                centerX + rotatedX,
                centerY + rotatedY);
        }

        return result;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    #endregion

    #region 기존 메서드들 (호환성 유지)

    public static string BoundsInfo(this IImageModel model)
    {
        var tl = model.TopLeft();
        var br = model.BottomRight();
        double width = model.Right - model.Left;
        double height = model.Top - model.Bottom;

        return $"좌상단: ({tl.Lat:F6}, {tl.Lng:F6})\n" +
               $"우하단: ({br.Lat:F6}, {br.Lng:F6})\n" +
               $"크기: {width:F6}° × {height:F6}°";
    }

    public static (double Top, double Bottom, double Left, double Right) AsEdges(this RectLatLng rect)
        => (rect.Top, rect.Bottom, rect.Left, rect.Right);

    #endregion
}