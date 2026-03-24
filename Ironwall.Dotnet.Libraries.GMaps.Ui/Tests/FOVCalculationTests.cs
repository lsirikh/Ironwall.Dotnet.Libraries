using System;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/****************************************************************************
   Purpose      : Camera FOV 계산 로직 단위 테스트 (Phase 15)
   Created By   : Claude Code
   Created On   : 2025-11-30
   PRD Reference: Docs/Camera_FOV_Fix_PRD.md v1.1
****************************************************************************/

/// <summary>
/// FOV 부채꼴 계산 관련 단위 테스트
/// - 각도 변환 (지도 방위각 → WPF 좌표계)
/// - 좌표 계산
/// </summary>
public class FOVCalculationTests
{
    #region Phase 15.2: 각도 좌표계 변환 테스트

    /// <summary>
    /// TEST-15.2.1: 지도 방위각 0° (북) → WPF 각도 -90° (위) 변환
    ///
    /// 지도 좌표계: 0° = 북, 90° = 동, 시계방향
    /// WPF 좌표계: 0° = 오른쪽(동), 90° = 아래(남), 시계방향
    /// 변환 공식: wpfAngle = bearing - 90
    /// </summary>
    [Fact]
    public void TEST_15_2_1_BearingConversion_North0_ShouldReturn_Minus90()
    {
        // Arrange
        double mapBearing = 0; // 북쪽

        // Act
        double wpfAngle = FOVCalculationHelper.ConvertBearingToWpfAngle(mapBearing);

        // Assert
        Assert.Equal(-90, wpfAngle);
    }

    /// <summary>
    /// TEST-15.2.2: 지도 방위각 90° (동) → WPF 각도 0° (오른쪽) 변환
    /// </summary>
    [Fact]
    public void TEST_15_2_2_BearingConversion_East90_ShouldReturn_0()
    {
        // Arrange
        double mapBearing = 90; // 동쪽

        // Act
        double wpfAngle = FOVCalculationHelper.ConvertBearingToWpfAngle(mapBearing);

        // Assert
        Assert.Equal(0, wpfAngle);
    }

    /// <summary>
    /// TEST-15.2.3: 지도 방위각 180° (남) → WPF 각도 90° (아래) 변환
    /// </summary>
    [Fact]
    public void TEST_15_2_3_BearingConversion_South180_ShouldReturn_90()
    {
        // Arrange
        double mapBearing = 180; // 남쪽

        // Act
        double wpfAngle = FOVCalculationHelper.ConvertBearingToWpfAngle(mapBearing);

        // Assert
        Assert.Equal(90, wpfAngle);
    }

    /// <summary>
    /// TEST-15.2.4: 지도 방위각 270° (서) → WPF 각도 180° (왼쪽) 변환
    /// </summary>
    [Fact]
    public void TEST_15_2_4_BearingConversion_West270_ShouldReturn_180()
    {
        // Arrange
        double mapBearing = 270; // 서쪽

        // Act
        double wpfAngle = FOVCalculationHelper.ConvertBearingToWpfAngle(mapBearing);

        // Assert
        Assert.Equal(180, wpfAngle);
    }

    #endregion

    #region Phase 15.1: 부채꼴 좌표 계산 테스트

    /// <summary>
    /// TEST-15.1.1: 부채꼴 시작점 계산 (북향, 60° 각도, 100px 반지름)
    /// </summary>
    [Fact]
    public void TEST_15_1_1_SectorStartPoint_North60Degree_ShouldBeCorrect()
    {
        // Arrange
        double bearing = 0;       // 북쪽
        double angle = 60;        // 60° 시야각
        double radius = 100;      // 100px 반지름
        double centerX = 0;
        double centerY = 0;

        // Act
        var (startX, startY, endX, endY) = FOVCalculationHelper.CalculateSectorPoints(
            centerX, centerY, radius, bearing, angle);

        // Assert
        // 북향(0°) + 60° 시야각의 시작점은 -30° (= -90° - 30° = -120°)에서 시작
        // WPF에서 -120°: cos(-120°) ≈ -0.5, sin(-120°) ≈ -0.866
        // 허용 오차 0.1 (소수점 반올림)
        Assert.InRange(startX, -51, -49);  // 100 * cos(-120°) ≈ -50
        Assert.InRange(startY, -87, -85);  // 100 * sin(-120°) ≈ -86.6
    }

    /// <summary>
    /// TEST-15.1.2: 180° 이상 각도에서 IsLargeArc 플래그 확인
    /// </summary>
    [Fact]
    public void TEST_15_1_2_IsLargeArc_Over180Degree_ShouldBeTrue()
    {
        // Arrange
        double angle = 200; // 180° 초과

        // Act
        bool isLargeArc = FOVCalculationHelper.IsLargeArc(angle);

        // Assert
        Assert.True(isLargeArc);
    }

    /// <summary>
    /// TEST-15.1.3: 180° 미만 각도에서 IsLargeArc 플래그 확인
    /// </summary>
    [Fact]
    public void TEST_15_1_3_IsLargeArc_Under180Degree_ShouldBeFalse()
    {
        // Arrange
        double angle = 90; // 180° 미만

        // Act
        bool isLargeArc = FOVCalculationHelper.IsLargeArc(angle);

        // Assert
        Assert.False(isLargeArc);
    }

    #endregion
}

/// <summary>
/// FOV 계산 헬퍼 - 순수 계산 로직 (테스트 가능)
/// </summary>
public static class FOVCalculationHelper
{
    /// <summary>
    /// 지도 방위각을 WPF 좌표계 각도로 변환
    /// - 지도: 0° = 북, 90° = 동, 시계방향
    /// - WPF: 0° = 동(오른쪽), 90° = 남(아래), 시계방향
    /// </summary>
    /// <param name="mapBearing">지도 방위각 (0-360°)</param>
    /// <returns>WPF 좌표계 각도</returns>
    public static double ConvertBearingToWpfAngle(double mapBearing)
    {
        // 지도 방위각에서 90도를 빼면 WPF 각도가 됨
        // 북(0°) → -90° (위)
        // 동(90°) → 0° (오른쪽)
        // 남(180°) → 90° (아래)
        // 서(270°) → 180° (왼쪽)
        return mapBearing - 90;
    }

    /// <summary>
    /// 부채꼴의 시작점과 끝점 좌표 계산
    /// </summary>
    /// <param name="centerX">중심 X 좌표</param>
    /// <param name="centerY">중심 Y 좌표</param>
    /// <param name="radius">반지름 (픽셀)</param>
    /// <param name="bearing">지도 방위각 (도)</param>
    /// <param name="angle">시야각 (도)</param>
    /// <returns>(startX, startY, endX, endY)</returns>
    public static (double startX, double startY, double endX, double endY) CalculateSectorPoints(
        double centerX, double centerY, double radius, double bearing, double angle)
    {
        // 1. 지도 방위각을 WPF 각도로 변환
        var wpfAngle = ConvertBearingToWpfAngle(bearing);

        // 2. 라디안으로 변환
        var wpfAngleRad = wpfAngle * Math.PI / 180.0;
        var halfAngleRad = (angle / 2.0) * Math.PI / 180.0;

        // 3. 시작각과 끝각 계산
        var startAngleRad = wpfAngleRad - halfAngleRad;
        var endAngleRad = wpfAngleRad + halfAngleRad;

        // 4. 좌표 계산
        var startX = centerX + radius * Math.Cos(startAngleRad);
        var startY = centerY + radius * Math.Sin(startAngleRad);
        var endX = centerX + radius * Math.Cos(endAngleRad);
        var endY = centerY + radius * Math.Sin(endAngleRad);

        return (startX, startY, endX, endY);
    }

    /// <summary>
    /// 180° 이상 각도 여부 확인 (WPF ArcSegment.IsLargeArc용)
    /// </summary>
    public static bool IsLargeArc(double angle)
    {
        return angle > 180;
    }
}
