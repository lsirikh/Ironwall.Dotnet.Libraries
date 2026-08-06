using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;
/****************************************************************************
   Purpose      : 도넛 호 컨버터 기하 검증 (map-topbar-trafficlight FR-B1 보완).
                  트랙 Ellipse(D=26,T=3)와의 동심 조건 = 반경 11.5 · 중심 (13,13) · 12시 시작.
   Created On   : 2026-08-06 · Sensorway Co., Ltd.
 ****************************************************************************/
public class PercentToDonutArcConverterTests
{
    private static readonly PercentToDonutArcConverter _sut = new() { Diameter = 26d, Thickness = 3d };

    private static object Convert(object? value)
        => _sut.Convert(value!, typeof(Geometry), null!, CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(0d)]
    [InlineData(-5d)]
    [InlineData(double.NaN)]
    public void should_return_empty_geometry_when_percent_not_positive(double pct)
    {
        var g = Convert(pct);
        Assert.Same(Geometry.Empty, g);
    }

    [Fact]
    public void should_return_empty_geometry_when_value_not_numeric()
    {
        Assert.Same(Geometry.Empty, Convert("not-a-number"));
        Assert.Same(Geometry.Empty, Convert(null));
    }

    [Theory]
    [InlineData(100d)]
    [InlineData(150d)]
    public void should_return_full_circle_when_percent_at_least_hundred(double pct)
    {
        var g = Assert.IsType<EllipseGeometry>(Convert(pct));
        Assert.Equal(new Point(13d, 13d), g.Center);
        Assert.Equal(11.5d, g.RadiusX, 3);
        Assert.Equal(11.5d, g.RadiusY, 3);
    }

    [Fact]
    public void should_start_arc_at_twelve_oclock_when_percent_partial()
    {
        // Arrange/Act — 트랙 동심 조건: 시작점 = (중심 13, 13 - 반경 11.5)
        var g = Assert.IsType<PathGeometry>(Convert(40d));

        // Assert
        var figure = Assert.Single(g.Figures);
        Assert.Equal(new Point(13d, 1.5d), figure.StartPoint);
        var arc = Assert.IsType<ArcSegment>(Assert.Single(figure.Segments));
        Assert.Equal(new Size(11.5d, 11.5d), arc.Size);
        Assert.Equal(SweepDirection.Clockwise, arc.SweepDirection);
    }

    [Fact]
    public void should_end_arc_at_three_oclock_when_percent_is_25()
    {
        // 25% = 90° 시계방향 → 종점 = (중심 + 반경, 중심) = (24.5, 13)
        var g = Assert.IsType<PathGeometry>(Convert(25d));
        var arc = (ArcSegment)((PathFigure)g.Figures[0]).Segments[0];
        Assert.Equal(24.5d, arc.Point.X, 3);
        Assert.Equal(13d, arc.Point.Y, 3);
        Assert.False(arc.IsLargeArc);
    }

    [Theory]
    [InlineData(50d, false)]   // 180° — 대호 아님(경계)
    [InlineData(50.1d, true)]  // 180° 초과 — 대호
    [InlineData(75d, true)]
    public void should_set_large_arc_flag_when_angle_exceeds_half_turn(double pct, bool expectLarge)
    {
        var g = Assert.IsType<PathGeometry>(Convert(pct));
        var arc = (ArcSegment)((PathFigure)g.Figures[0]).Segments[0];
        Assert.Equal(expectLarge, arc.IsLargeArc);
    }

    [Fact]
    public void should_return_frozen_geometry_when_converted()
    {
        // 바인딩 재평가(1초 틱)마다 생성 — Freeze 로 렌더 스레드 승격 비용 제거 계약
        Assert.True(((Freezable)Convert(60d)).IsFrozen);
        Assert.True(((Freezable)Convert(100d)).IsFrozen);
    }
}
