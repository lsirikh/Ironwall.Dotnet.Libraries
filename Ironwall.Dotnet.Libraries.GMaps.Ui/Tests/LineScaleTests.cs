using System;
using System.Collections.Generic;
using System.Windows;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>LineArea_Symbol_Resize FR-01/FR-03 — 순수 지오 스케일 수학(LineGeometryUtils) 단위테스트.
/// 가짜 선형 투영 주입(WPF/맵 무관). 퇴화·극단 가드(§5-C R-08/C6, V-10) 검증.</summary>
public class LineScaleTests
{
    // 가짜 선형 투영: local=(Lng*1000, -Lat*1000), 역변환 왕복 항등. 스케일=위경도 델타 배율.
    private static readonly Func<PointLatLng, Point> ToLocal = p => new Point(p.Lng * 1000.0, -p.Lat * 1000.0);
    private static readonly Func<Point, PointLatLng> ToLatLng = pt => new PointLatLng(-pt.Y / 1000.0, pt.X / 1000.0);

    private static readonly PointLatLng Center = new PointLatLng(37.5, 127.0);

    [Fact]
    public void should_scale_points_uniformly_when_corner()
    {
        var pts = new List<PointLatLng> { new PointLatLng(37.6, 127.2) };   // 중심에서 (+0.1,+0.2)
        var r = LineGeometryUtils.Scale(pts, Center, 2.0, 2.0, ToLocal, ToLatLng);
        Assert.Equal(37.7, r[0].Lat, 9);   // 37.5 + 2*0.1
        Assert.Equal(127.4, r[0].Lng, 9);  // 127.0 + 2*0.2
    }

    [Fact]
    public void should_keep_center_point_fixed_when_scale()
    {
        var pts = new List<PointLatLng> { Center };
        var r = LineGeometryUtils.Scale(pts, Center, 3.0, 0.5, ToLocal, ToLatLng);
        Assert.Equal(Center.Lat, r[0].Lat, 9);
        Assert.Equal(Center.Lng, r[0].Lng, 9);
    }

    [Fact]
    public void should_preserve_relative_spacing_when_scale()
    {
        var pts = new List<PointLatLng> { new PointLatLng(37.4, 126.9), new PointLatLng(37.6, 127.1) };
        var r = LineGeometryUtils.Scale(pts, Center, 2.0, 2.0, ToLocal, ToLatLng);
        // 두 점 간 위도차 원래 0.2 → 스케일 후 0.4
        Assert.Equal(0.4, r[1].Lat - r[0].Lat, 9);
        Assert.Equal(0.4, r[1].Lng - r[0].Lng, 9);
    }

    [Fact]
    public void should_return_unchanged_ratio_when_degenerate_extent()
    {
        // 수직선(Δlng=0)·중첩점 → old extent 0 → 배율 1(0나눗셈 방지)
        Assert.Equal(1.0, LineGeometryUtils.SafeRatio(0.0, 5.0));
        Assert.Equal(1.0, LineGeometryUtils.SafeRatio(1e-12, 5.0));
    }

    [Fact]
    public void should_clamp_ratio_when_sign_flip()
    {
        // 반대편 통과(음수)·0 붕괴 → MIN_SCALE 클램프(부호반전 방지)
        Assert.Equal(LineGeometryUtils.MIN_SCALE, LineGeometryUtils.SafeRatio(10.0, -5.0));
        Assert.True(LineGeometryUtils.SafeRatio(1.0, 1000.0) <= LineGeometryUtils.MAX_SCALE);
    }

    [Fact]
    public void should_guard_nonfinite_scale_factor()
    {
        var pts = new List<PointLatLng> { new PointLatLng(37.6, 127.2) };
        // sx=NaN → 1로 방어(x 불변), sy=2 정상
        var r = LineGeometryUtils.Scale(pts, Center, double.NaN, 2.0, ToLocal, ToLatLng);
        Assert.Equal(127.2, r[0].Lng, 9);   // x축(Lng) 불변
        Assert.Equal(37.7, r[0].Lat, 9);    // y축(Lat) 2배
    }

    [Fact]
    public void should_compute_bbox_center()
    {
        var pts = new List<PointLatLng>
        {
            new PointLatLng(37.4, 126.8), new PointLatLng(37.6, 127.2), new PointLatLng(37.5, 127.0)
        };
        var c = LineGeometryUtils.BoundsCenter(pts);
        Assert.Equal(37.5, c.Lat, 9);   // (37.4+37.6)/2
        Assert.Equal(127.0, c.Lng, 9);  // (126.8+127.2)/2
    }

    [Fact]
    public void should_return_empty_when_no_points()
    {
        var r = LineGeometryUtils.Scale(new List<PointLatLng>(), Center, 2.0, 2.0, ToLocal, ToLatLng);
        Assert.Empty(r);
    }
}
