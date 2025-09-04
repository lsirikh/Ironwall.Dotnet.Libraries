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

    #region 벡터 기반 회전 계산 메서드들

    /// <summary>
    /// 벡터 기반 회전 각도 계산 (Vector.AngleBetween 사용)
    /// </summary>
    /// <param name="centerPoint">회전 중심점</param>
    /// <param name="startPoint">시작점 (드래그 시작)</param>
    /// <param name="currentPoint">현재점 (드래그 중)</param>
    /// <returns>회전 각도 (-180 ~ +180도)</returns>
    public static double CalculateVectorRotationAngle(Point centerPoint, Point startPoint, Point currentPoint)
    {
        try
        {
            // 중심점에서 각 점까지의 벡터 계산
            var startVector = Point.Subtract(startPoint, centerPoint);
            var currentVector = Point.Subtract(currentPoint, centerPoint);

            // 벡터 길이가 0에 가까우면 각도 계산 불가
            if (startVector.Length < 0.001 || currentVector.Length < 0.001)
                return 0.0;

            // 두 벡터 간의 각도 계산 (WPF Vector.AngleBetween 사용)
            return Vector.AngleBetween(startVector, currentVector);
        }
        catch
        {
            return 0.0; // 예외 발생 시 회전 없음
        }
    }

    /// <summary>
    /// 각도를 0-360도 범위로 정규화
    /// </summary>
    /// <param name="angle">정규화할 각도</param>
    /// <returns>0-360도 범위의 각도</returns>
    public static double NormalizeAngle(double angle)
    {
        while (angle < 0) angle += 360;
        while (angle >= 360) angle -= 360;
        return angle;
    }

    /// <summary>
    /// 회전 스냅 적용
    /// </summary>
    /// <param name="angle">원본 각도</param>
    /// <param name="snapDegree">스냅 단위 (기본 5도)</param>
    /// <param name="enableSnap">스냅 활성화 여부</param>
    /// <returns>스냅된 각도</returns>
    public static double ApplyRotationSnap(double angle, double snapDegree = 5.0, bool enableSnap = true)
    {
        if (!enableSnap)
            return Math.Round(angle); // 스냅 비활성화 시 1도 단위

        return Math.Round(angle / snapDegree) * snapDegree;
    }

    /// <summary>
    /// 회전 변화량이 의미있는 크기인지 확인
    /// </summary>
    /// <param name="currentAngle">현재 각도</param>
    /// <param name="newAngle">새로운 각도</param>
    /// <param name="threshold">최소 변화량 임계값 (기본 0.5도)</param>
    /// <returns>의미있는 변화인지 여부</returns>
    public static bool IsSignificantRotationChange(double currentAngle, double newAngle, double threshold = 0.5)
    {
        var delta = Math.Abs(AngleDifference(currentAngle, newAngle));
        return delta >= threshold;
    }

    /// <summary>
    /// 두 각도 간의 차이 계산 (최단 경로)
    /// </summary>
    /// <param name="angle1">첫 번째 각도</param>
    /// <param name="angle2">두 번째 각도</param>
    /// <returns>각도 차이 (-180 ~ +180도)</returns>
    public static double AngleDifference(double angle1, double angle2)
    {
        var diff = angle2 - angle1;
        while (diff > 180) diff -= 360;
        while (diff < -180) diff += 360;
        return diff;
    }

    /// <summary>
    /// 벡터 기반 완전한 회전 처리 (RotateThumb 방식)
    /// </summary>
    /// <param name="centerPoint">회전 중심점</param>
    /// <param name="startPoint">시작점</param>
    /// <param name="currentPoint">현재점</param>
    /// <param name="initialBearing">초기 마커 회전값</param>
    /// <param name="enableSnap">스냅 활성화</param>
    /// <param name="snapDegree">스냅 단위</param>
    /// <param name="threshold">변화량 임계값</param>
    /// <returns>벡터 기반 회전 결과</returns>
    public static VectorRotationResult ProcessVectorRotation(
        Point centerPoint,
        Point startPoint,
        Point currentPoint,
        double initialBearing,
        bool enableSnap = true,
        double snapDegree = 5.0,
        double threshold = 3.0)
    {
        // 1. 벡터 기반 회전 각도 계산
        var rotationAngle = CalculateVectorRotationAngle(centerPoint, startPoint, currentPoint);

        // 2. 새로운 마커 회전값 계산 (RotateThumb 방식)
        var newBearing = initialBearing + rotationAngle;

        // 3. 0-360도 범위로 정규화
        newBearing = NormalizeAngle(newBearing);

        // 4. 스냅 적용
        var snappedBearing = ApplyRotationSnap(newBearing, snapDegree, enableSnap);

        // 5. 변화량 확인
        var isSignificant = IsSignificantRotationChange(initialBearing, snappedBearing, threshold);

        return new VectorRotationResult
        {
            RotationAngle = rotationAngle,
            NewBearing = newBearing,
            SnappedBearing = snappedBearing,
            InitialBearing = initialBearing,
            IsSignificantChange = isSignificant,
            EnableSnap = enableSnap,
            SnapDegree = snapDegree
        };
    }

    /// <summary>
    /// 회전 핸들 위치 계산 (회전 핸들만)
    /// </summary>
    /// <param name="bounds">마커 경계</param>
    /// <returns>회전 핸들 위치</returns>
    public static Point GetRotateHandlePosition(Rect bounds)
    {
        var centerX = bounds.Left + bounds.Width / 2;
        var top = bounds.Top;
        return new Point(centerX, top - MarkerEditSettings.RotateHandleDistance);
    }

    /// <summary>
    /// 마커 중심점 계산
    /// </summary>
    /// <param name="bounds">마커 경계</param>
    /// <returns>마커 중심점</returns>
    public static Point GetMarkerCenter(Rect bounds)
    {
        return new Point(
            bounds.Left + bounds.Width / 2,
            bounds.Top + bounds.Height / 2
        );
    }

    /// <summary>
    /// 마커 화면 중심점 계산
    /// </summary>
    public static Point CalculateMarkerScreenCenter(Size size)
    {
        return new Point(size.Width / 2.0, size.Height / 2.0);
    }

    /// <summary>
    /// 회전 방향 계산 (+1: 시계방향, -1: 반시계방향, 0: 움직임 없음)
    /// </summary>
    public static int CalculateRotationDirection(Point startPoint, Point currentPoint, Point markerCenterPoint)
    {
        // 중심점에서 각 점까지의 벡터
        var startVector = Point.Subtract(startPoint, markerCenterPoint);
        var currentVector = Point.Subtract(currentPoint, markerCenterPoint);

        // 외적으로 회전 방향 계산
        var crossProduct = startVector.X * currentVector.Y - startVector.Y * currentVector.X;

        // 최소 움직임 임계값
        if (Math.Abs(crossProduct) < 10.0) return 0; // 너무 작은 움직임 무시

        return crossProduct > 0 ? 1 : -1; // 1: 시계방향, -1: 반시계방향
    }

    /// <summary>
    /// 경계를 고려한 각도 차이 계산
    /// </summary>
    public static double CalculateAngleDifferenceWithBoundary(double currentBearing, double targetBearing)
    {
        // 기본 차이 계산
        var directDiff = Math.Abs(targetBearing - currentBearing);

        // 경계를 넘나드는 경우의 차이도 계산
        var crossBoundaryDiff1 = Math.Abs((targetBearing + 360) - currentBearing);
        var crossBoundaryDiff2 = Math.Abs(targetBearing - (currentBearing + 360));

        // 가장 작은 차이 반환 (최단 경로)
        return Math.Min(directDiff, Math.Min(crossBoundaryDiff1, crossBoundaryDiff2));
    }

    /// <summary>
    /// 방향성을 고려한 감소 여부 확인
    /// </summary>
    public static bool IsDecreasingInDirection(double current, double target, int direction)
    {
        if (direction > 0) // 시계방향
        {
            // 경계를 고려한 감소 확인
            if (current > 350 && target < 10) // 359° → 1° (경계 횡단)
            {
                return false; // 실제로는 증가
            }

            return target < current;
        }

        return false;
    }

    /// <summary>
    /// 방향성을 고려한 증가 여부 확인
    /// </summary>
    public static bool IsIncreasingInDirection(double current, double target, int direction)
    {
        if (direction < 0) // 반시계방향
        {
            // 경계를 고려한 증가 확인
            if (current < 10 && target > 350) // 1° → 359° (경계 횡단)
            {
                return false; // 실제로는 감소
            }

            return target > current;
        }

        return false;
    }

    #endregion

    #region 회전 결과 구조체

    /// <summary>
    /// 벡터 기반 회전 결과
    /// </summary>
    public struct VectorRotationResult
    {
        /// <summary>
        /// 회전 각도 (-180 ~ +180도)
        /// </summary>
        public double RotationAngle { get; set; }

        /// <summary>
        /// 새로운 마커 방향각 (0-360도)
        /// </summary>
        public double NewBearing { get; set; }

        /// <summary>
        /// 스냅이 적용된 방향각 (0-360도)
        /// </summary>
        public double SnappedBearing { get; set; }

        /// <summary>
        /// 초기 마커 방향각 (0-360도)
        /// </summary>
        public double InitialBearing { get; set; }

        /// <summary>
        /// 의미있는 변화인지 여부
        /// </summary>
        public bool IsSignificantChange { get; set; }

        /// <summary>
        /// 스냅 활성화 여부
        /// </summary>
        public bool EnableSnap { get; set; }

        /// <summary>
        /// 스냅 단위 (도)
        /// </summary>
        public double SnapDegree { get; set; }

        /// <summary>
        /// 로깅용 정보 문자열
        /// </summary>
        /// <returns>회전 정보 문자열</returns>
        public readonly string GetLogInfo()
        {
            return $"벡터회전: {SnappedBearing:F1}° (회전량: {RotationAngle:F1}°, " +
                   $"초기: {InitialBearing:F1}°, 스냅: {(EnableSnap ? $"{SnapDegree}도" : "1도")})";
        }
    }

    #endregion
}
