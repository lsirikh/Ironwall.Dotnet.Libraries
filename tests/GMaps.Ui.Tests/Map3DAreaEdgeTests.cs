using System;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 3D 영역 경계값(영역 B 전수 시나리오, 2026-10-19) — 회전 영역 rect 해석(Map3DRotRect.Parse)의 경계 · 크기 상한(정확히 2000 m) ·
/// 「이 영역 3D 로 보기」 처음 방위(Map3DMath.EnterBearing). 순수 함수만(창 없음).
/// </summary>
public class Map3DAreaEdgeTests
{
    private static JObject Rect(object lon, object lat, object w, object h, object rot) => new()
    {
        ["lon"] = JToken.FromObject(lon), ["lat"] = JToken.FromObject(lat), ["width_m"] = JToken.FromObject(w),
        ["height_m"] = JToken.FromObject(h), ["rotation_deg"] = JToken.FromObject(rot),
    };

    // ── B-27 rect 해석 경계 ──

    [Theory]
    [InlineData(0d, 100d)]     // 가로 0
    [InlineData(100d, 0d)]     // 세로 0
    [InlineData(-5d, 100d)]    // 음수
    [InlineData(100d, -0.1d)]
    public void should_return_null_when_rect_size_is_zero_or_negative(double w, double h)
        => Assert.Null(Map3DRotRect.Parse(Rect(127.0, 37.5, w, h, 30d)));

    [Theory]
    [InlineData(0d)]
    [InlineData(360d)]
    [InlineData(-360d)]
    [InlineData(720d)]
    public void should_return_null_when_rotation_is_multiple_of_360(double rot)
        => Assert.Null(Map3DRotRect.Parse(Rect(127.0, 37.5, 300d, 160d, rot)));   // 북쪽 정렬 영역 = bbox 로

    [Theory]
    [InlineData(-60d, 300d)]
    [InlineData(-360.5d, 359.5d)]
    [InlineData(370d, 10d)]
    [InlineData(359.999d, 359.999d)]
    public void should_wrap_rotation_into_0_360_when_rect_parsed(double rot, double expected)
        => Assert.Equal(expected, Map3DRotRect.Parse(Rect(127.0, 37.5, 300d, 160d, rot))!.RotationDeg, 9);

    [Fact]
    public void should_read_integer_values_when_rect_numbers_are_integers()
    {
        var r = Map3DRotRect.Parse(Rect(127, 37, 300, 160, 45))!;   // JSON 정수(1.0 이 아니라 1)
        Assert.Equal((127d, 37d, 300d, 160d, 45d), (r.Lon, r.Lat, r.WidthM, r.HeightM, r.RotationDeg));
    }

    [Fact]
    public void should_return_null_when_rect_value_is_not_a_number_or_missing()
    {
        Assert.Null(Map3DRotRect.Parse(Rect("127", 37.5, 300d, 160d, 30d)));    // 문자열 수
        Assert.Null(Map3DRotRect.Parse(Rect(127.0, 37.5, 300d, 160d, true)));   // 불리언
        var missing = Rect(127.0, 37.5, 300d, 160d, 30d); missing.Remove("height_m");
        Assert.Null(Map3DRotRect.Parse(missing));
        Assert.Null(Map3DRotRect.Parse(new JArray(1, 2, 3)));
        Assert.Null(Map3DRotRect.Parse(null));
        var nan = Rect(127.0, 37.5, 300d, 160d, 30d); nan["width_m"] = double.NaN;
        Assert.Null(Map3DRotRect.Parse(nan));
    }

    // ── B-10 크기 상한 경계(회전 변 정확히 2000 m) ──

    [Fact]
    public void should_allow_rotated_area_when_side_is_exactly_2000m()
    {
        var area = new Map3DArea(new Map3DRotRect(127.0, 37.5, 2000d, 2000d, 30d).Bbox(), new Map3DRotRect(127.0, 37.5, 2000d, 2000d, 30d));
        var (w, h) = area.SizeMeters;
        Assert.False(Map3DGuide.IsAreaTooLarge(w, h, Map3DGuide.MaxAreaSideM));
        var label = Map3DGuide.AreaDrawLabel(area, Map3DGuide.MaxAreaSideM);
        Assert.False(label.IsWarning, label.Text);
        Assert.DoesNotContain("초과", label.Text);

        var over = new Map3DArea(Array.Empty<double>(), new Map3DRotRect(127.0, 37.5, 2000.01d, 10d, 30d));
        Assert.True(Map3DGuide.AreaDrawLabel(over, Map3DGuide.MaxAreaSideM).IsWarning);
    }

    // ── B-28 「이 영역 3D 로 보기」 처음 방위 ──

    [Theory]
    [InlineData(0d, 300d, 300d)]     // 2D 가 돌아가지 않았어도(회전 막힘) 3D 는 영역 회전각
    [InlineData(-60d, 300d, 300d)]   // 2D 를 영역 방위로 돌린 뒤
    [InlineData(20d, 45d, 45d)]
    public void should_open_3d_at_rect_rotation_regardless_of_2d_bearing_when_area_view(double gmapBearing, double rectRotation, double expected)
        => Assert.Equal(expected, Map3DMath.EnterBearing(gmapBearing, new Map3DRotRect(127.0, 37.5, 300d, 160d, rectRotation)), 9);

    [Fact]
    public void should_open_3d_at_2d_bearing_when_area_not_rotated()
        => Assert.Equal(340d, Map3DMath.EnterBearing(-20d, null), 9);
}
