using System;
using System.Windows;
using GMap.NET;
namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/12/2025 12:27:00 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 마커 편집 핸들 유형 (Image 스타일)
/// </summary>
public enum MarkerHandle
{
    None = 0,

    // 이동
    Move = 1,           // 중심 이동

    // 회전  
    Rotate = 2,         // 회전 핸들

    // 자유 크기 조정 (각 모서리)
    ResizeTopLeft = 3,      // 좌상단
    ResizeTopRight = 4,     // 우상단  
    ResizeBottomLeft = 5,   // 좌하단
    ResizeBottomRight = 6,  // 우하단

    // 비율 유지 크기 조정 (각 변의 중점)
    ResizeTop = 7,          // 상단 중점 (높이만)
    ResizeBottom = 8,       // 하단 중점 (높이만)
    ResizeLeft = 9,         // 좌측 중점 (너비만)
    ResizeRight = 10,       // 우측 중점 (너비만)

    // 비율 유지 전체 크기 조정
    ResizeProportional = 11 // Ctrl+드래그 시 모든 핸들이 비율 유지
}

/// <summary>
/// 크기 조정 모드
/// </summary>
public enum ResizeMode
{
    /// <summary>
    /// 자유 크기 조정 (가로세로 독립적)
    /// </summary>
    Free = 0,

    /// <summary>
    /// 비율 유지 크기 조정
    /// </summary>
    Proportional = 1,

    /// <summary>
    /// 너비만 조정
    /// </summary>
    WidthOnly = 2,

    /// <summary>
    /// 높이만 조정  
    /// </summary>
    HeightOnly = 3
}

/// <summary>
/// 마커 편집 설정 (Image 스타일)
/// </summary>
public static class MarkerEditSettings
{
    /// <summary>
    /// 편집 핸들 크기
    /// </summary>
    public static double HandleSize { get; set; } = 8.0;

    /// <summary>
    /// 편집 핸들 허용 오차
    /// </summary>
    public static double HandleTolerance { get; set; } = 12.0;

    /// <summary>
    /// 편집 영역 오프셋
    /// </summary>
    public static double EditAreaOffset { get; set; } = 15.0;

    /// <summary>
    /// 회전 핸들 거리
    /// </summary>
    public static double RotateHandleDistance { get; set; } = 25.0;

    /// <summary>
    /// 최소 마커 크기
    /// </summary>
    public static double MinMarkerSize { get; set; } = 10.0;

    /// <summary>
    /// 최대 마커 크기
    /// </summary>
    public static double MaxMarkerSize { get; set; } = 500.0;

    /// <summary>
    /// 비율 유지 임계값 (픽셀)
    /// </summary>
    public static double ProportionalThreshold { get; set; } = 5.0;
}


/// <summary>
/// 마커 편집 유틸리티 (Image 스타일)
/// </summary>
public static class MarkerEditUtils
{
    /// <summary>
    /// 두 점 사이의 거리 계산
    /// </summary>
    public static double Distance(Point p1, Point p2)
    {
        return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
    }

    /// <summary>
    /// 점이 허용 범위 내에 있는지 확인
    /// </summary>
    public static bool IsPointNear(Point p1, Point p2, double tolerance)
    {
        return Distance(p1, p2) <= tolerance;
    }

    /// <summary>
    /// 중심점을 기준으로 한 각도 계산
    /// </summary>
    public static double CalculateAngle(Point center, Point point)
    {
        double deltaX = point.X - center.X;
        double deltaY = point.Y - center.Y;
        double angle = Math.Atan2(deltaX, -deltaY) * 180 / Math.PI;
        return angle < 0 ? angle + 360 : angle;
    }

    /// <summary>
    /// 값을 범위 내로 제한
    /// </summary>
    public static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }

    /// <summary>
    /// 비율을 유지하면서 크기 조정
    /// </summary>
    public static Size CalculateProportionalSize(Size originalSize, double newWidth, double newHeight)
    {
        var aspectRatio = originalSize.Width / originalSize.Height;

        // 더 큰 변화를 기준으로 크기 결정
        var widthChange = Math.Abs(newWidth - originalSize.Width);
        var heightChange = Math.Abs(newHeight - originalSize.Height);

        if (widthChange > heightChange)
        {
            // 너비 기준으로 높이 계산
            return new Size(newWidth, newWidth / aspectRatio);
        }
        else
        {
            // 높이 기준으로 너비 계산  
            return new Size(newHeight * aspectRatio, newHeight);
        }
    }

    /// <summary>
    /// 핸들 유형에 따른 크기 조정 모드 결정
    /// </summary>
    public static ResizeMode GetResizeMode(MarkerHandle handle, bool isCtrlPressed)
    {
        // Ctrl 키가 눌렸으면 항상 비율 유지
        if (isCtrlPressed)
            return ResizeMode.Proportional;

        return handle switch
        {
            // 모서리 핸들 - 자유 크기 조정
            MarkerHandle.ResizeTopLeft or
            MarkerHandle.ResizeTopRight or
            MarkerHandle.ResizeBottomLeft or
            MarkerHandle.ResizeBottomRight => ResizeMode.Free,

            // 상하단 중점 - 높이만
            MarkerHandle.ResizeTop or
            MarkerHandle.ResizeBottom => ResizeMode.HeightOnly,

            // 좌우측 중점 - 너비만
            MarkerHandle.ResizeLeft or
            MarkerHandle.ResizeRight => ResizeMode.WidthOnly,

            // 비율 유지 핸들
            MarkerHandle.ResizeProportional => ResizeMode.Proportional,

            _ => ResizeMode.Free
        };
    }

    /// <summary>
    /// 사각형 영역 내의 핸들 위치들 계산
    /// </summary>
    public static Point[] CalculateHandlePositions(Rect bounds)
    {
        var positions = new Point[11]; // None 제외하고 11개 핸들

        var left = bounds.Left;
        var right = bounds.Right;
        var top = bounds.Top;
        var bottom = bounds.Bottom;
        var centerX = bounds.Left + bounds.Width / 2;
        var centerY = bounds.Top + bounds.Height / 2;

        positions[(int)MarkerHandle.Move - 1] = new Point(centerX, centerY);
        positions[(int)MarkerHandle.Rotate - 1] = new Point(centerX, top - MarkerEditSettings.RotateHandleDistance);

        // 모서리 핸들들
        positions[(int)MarkerHandle.ResizeTopLeft - 1] = new Point(left, top);
        positions[(int)MarkerHandle.ResizeTopRight - 1] = new Point(right, top);
        positions[(int)MarkerHandle.ResizeBottomLeft - 1] = new Point(left, bottom);
        positions[(int)MarkerHandle.ResizeBottomRight - 1] = new Point(right, bottom);

        // 중점 핸들들
        positions[(int)MarkerHandle.ResizeTop - 1] = new Point(centerX, top);
        positions[(int)MarkerHandle.ResizeBottom - 1] = new Point(centerX, bottom);
        positions[(int)MarkerHandle.ResizeLeft - 1] = new Point(left, centerY);
        positions[(int)MarkerHandle.ResizeRight - 1] = new Point(right, centerY);

        return positions;
    }
}