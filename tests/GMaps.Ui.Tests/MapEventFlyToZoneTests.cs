using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 이벤트 지도 이동 — 구역 선(FenceGroup) 목표 · 구역 줌 · 3D 경로 · 3D 조작 양보 · 3D 구역 탐지 구간(헤드리스, 창 없음).
/// </summary>
public class MapEventFlyToZoneTests
{
    // 동서로 곧게 뻗은 약 176 m 구역 선(위도 37°, 경도 0.002° 간격 두 칸)
    private static readonly (double Lat, double Lng)[] Straight = { (37.0, 127.000), (37.0, 127.001), (37.0, 127.002) };

    // ── 구역 그룹 후보 ──

    [Fact]
    public void should_order_entry_groups_then_device_groups_when_both_given()
    {
        var ids = MapFlyToMath.ZoneGroupCandidates(new[] { 5, 0, 7 }, new[] { 7, 9 }, isGroupEvent: false, deviceId: 100);
        Assert.Equal(new[] { 5, 7, 9 }, ids);
    }

    [Fact]
    public void should_add_device_id_last_when_event_is_zone_event()
    {
        var ids = MapFlyToMath.ZoneGroupCandidates(null, new[] { 3 }, isGroupEvent: true, deviceId: 12);
        Assert.Equal(new[] { 3, 12 }, ids);
    }

    [Fact]
    public void should_return_no_candidates_when_nothing_links_to_a_zone()
        => Assert.Empty(MapFlyToMath.ZoneGroupCandidates(null, null, isGroupEvent: false, deviceId: 12));

    // ── 구역 목표 ──

    [Fact]
    public void should_target_bounds_center_when_no_sensor_is_on_the_line()
    {
        var t = MapFlyToMath.ResolveZoneTarget(Straight, false, Array.Empty<(double, double)>())!.Value;
        Assert.False(t.IsSection);
        Assert.Equal(37.0, t.Lat, 9);
        Assert.Equal(127.001, t.Lng, 9);
    }

    [Fact]
    public void should_target_section_middle_when_detecting_sensors_are_on_the_line()
    {
        // 선에서 약 5 m 떨어진 두 센서 — 경도 127.0005 · 127.0015 에 투영 → 구간 가운데 127.001
        var sensors = new[] { (37.00005, 127.0005), (37.00005, 127.0015) };
        var t = MapFlyToMath.ResolveZoneTarget(Straight, false, sensors)!.Value;
        Assert.True(t.IsSection);
        Assert.Equal(37.0, t.Lat, 9);
        Assert.Equal(127.001, t.Lng, 6);
    }

    [Fact]
    public void should_target_the_sensor_point_when_only_one_sensor_detects()
    {
        var t = MapFlyToMath.ResolveZoneTarget(Straight, false, new[] { (37.0, 127.0018) })!.Value;
        Assert.True(t.IsSection);
        Assert.Equal(127.0018, t.Lng, 6);
    }

    [Fact]
    public void should_ignore_sensor_when_it_is_far_from_the_line()
    {
        // 위도 0.001° ≈ 111 m 북쪽 — 50 m 밖이라 구간이 아니다
        var t = MapFlyToMath.ResolveZoneTarget(Straight, false, new[] { (37.001, 127.0005) })!.Value;
        Assert.False(t.IsSection);
    }

    [Fact]
    public void should_return_null_when_zone_line_has_no_points()
        => Assert.Null(MapFlyToMath.ResolveZoneTarget(Array.Empty<(double, double)>(), false, Array.Empty<(double, double)>()));

    [Fact]
    public void should_measure_section_along_closing_edge_when_zone_is_closed()
    {
        // 정사각형(시계 반대로 네 변) — 마지막 닫는 변(왼쪽 변) 위 센서는 4 번째 변 위 비율(0.75 ~ 1)
        var square = new[] { (37.0, 127.0), (37.0, 127.001), (37.001, 127.001), (37.001, 127.0) };
        var s = MapFlyToMath.ZoneSection(square, true, new[] { (37.0005, 127.0) })!.Value;
        Assert.InRange(s.From, 0.75, 1.0);
        Assert.Equal(s.From, s.To, 9);
        Assert.Null(MapFlyToMath.ZoneSection(square, false, new[] { (37.0005, 127.0) }));   // 열린 선이면 왼쪽 변이 없어 양 끝점에서 약 55 m
    }

    [Fact]
    public void should_return_point_on_line_when_walking_by_fraction()
    {
        Assert.Equal(127.0, MapFlyToMath.PointAlong(Straight, false, 0).Lng, 9);
        Assert.Equal(127.001, MapFlyToMath.PointAlong(Straight, false, 0.5).Lng, 9);
        Assert.Equal(127.002, MapFlyToMath.PointAlong(Straight, false, 1).Lng, 9);
        Assert.Equal(127.002, MapFlyToMath.PointAlong(Straight, false, 3).Lng, 9);   // 범위 밖은 끝점
    }

    // ── 구역 줌 ──

    [Fact]
    public void should_fit_zone_inside_view_when_zone_is_large()
    {
        // 약 0.02° 폭(≈1.8 km) 구역 · 1000 DIP 화면 — 256·2^z·(0.02/360) ≤ 800 → z ≤ 15.8 → 15.5
        var wide = new[] { (37.0, 127.0), (37.0, 127.02) };
        Assert.Equal(15.5, MapFlyToMath.FitZoom(wide, 1000, 800));
    }

    [Fact]
    public void should_return_infinity_when_zone_is_a_point_or_view_unknown()
    {
        Assert.True(double.IsPositiveInfinity(MapFlyToMath.FitZoom(new[] { (37.0, 127.0) }, 1000, 800)));
        Assert.True(double.IsPositiveInfinity(MapFlyToMath.FitZoom(Straight, 0, 0)));
    }

    [Fact]
    public void should_zoom_out_to_fit_zone_when_current_zoom_is_larger_and_whole_zone_is_target()
        => Assert.Equal(15.5, MapFlyToMath.ResolveZoneZoom(isSection: false, maxZoomSetting: 19, mapMinZoom: 10, mapMaxZoom: 20, currentZoom: 19.5, fitZoom: 15.5));

    [Fact]
    public void should_cap_at_setting_zoom_when_zone_fits_at_higher_zoom()
        => Assert.Equal(19, MapFlyToMath.ResolveZoneZoom(false, 19, 10, 20, 14, double.PositiveInfinity));

    [Fact]
    public void should_use_device_zoom_rule_when_target_is_section()
        => Assert.Equal(19.5, MapFlyToMath.ResolveZoneZoom(isSection: true, 19, 10, 20, currentZoom: 19.5, fitZoom: 12));

    [Fact]
    public void should_clamp_to_map_min_when_zone_is_huge()
        => Assert.Equal(10, MapFlyToMath.ResolveZoneZoom(false, 19, 10, 20, 15, 4.5));

    // ── 3D 경로 ──

    [Theory]
    [InlineData(false, true, FlyToPath.TwoD)]
    [InlineData(false, false, FlyToPath.TwoD)]
    [InlineData(true, true, FlyToPath.ThreeD)]
    [InlineData(true, false, FlyToPath.Leave3DThenTwoD)]
    public void should_choose_path_when_3d_shown_and_target_stays_in_scene(bool shown, bool stays, FlyToPath expected)
        => Assert.Equal(expected, MapFlyToMath.ChoosePath(shown, stays));

    // ── 3D 조작 양보 ──

    [Theory]
    [InlineData("user", true)]
    [InlineData("live", true)]
    [InlineData("fly", false)]
    [InlineData(null, false)]
    public void should_count_3d_camera_as_user_input_when_reason_is_user_or_live(string? reason, bool expected)
        => Assert.Equal(expected, MapFlyToMath.IsUserCameraReason(reason));

    [Fact]
    public void should_yield_when_3d_user_camera_came_within_quiet_time()
    {
        // 3D 조작 시각을 2D 와 같은 마지막 조작 시각으로 쓰면 같은 2 s 규칙이 걸린다
        Assert.True(MapFlyToMath.ShouldYield(new FlyToUserState(false, false, false, false, 500)));
        Assert.False(MapFlyToMath.ShouldYield(new FlyToUserState(false, false, false, false, MapFlyToMath.UserQuietMs)));
    }

    // ── 3D 구역 탐지 구간(state.activeSection) ──

    [Fact]
    public void should_send_active_section_in_zone_state_when_known()
    {
        var line = Map3DOverlay.Line(Map3DOverlay.KindPidsGroup, 11, "구역 A", Straight, false, "#FF0000", 2,
            true, null, null, true, EnumCompositeEventStatus.Detecting, true, true, 0) with { ActiveSection = (0.25, 0.5) };
        var section = (JArray)Map3DOverlay.ToJson(line)["state"]!["activeSection"]!;
        Assert.Equal(new[] { 0.25, 0.5 }, section.Select(v => v.Value<double>()).ToArray());
    }

    [Fact]
    public void should_send_null_section_when_zone_section_unknown_and_omit_it_for_plain_lines()
    {
        var zone = Map3DOverlay.ToJson(Map3DOverlay.Line(Map3DOverlay.KindPidsGroup, 11, "구역 A", Straight, false, "#FF0000", 2,
            true, null, null, true, null, true, true, 0));
        Assert.Equal(JTokenType.Null, zone["state"]!["activeSection"]!.Type);

        var plain = Map3DOverlay.ToJson(Map3DOverlay.Line(Map3DOverlay.KindLine, 2, "선", Straight, false, "#2196F3", 1,
            false, null, null, false, null, true, true, 0));
        Assert.Null(plain["state"]!["activeSection"]);
    }

    [Fact]
    public void should_patch_only_active_section_when_section_changes()
    {
        var a = Map3DOverlay.Line(Map3DOverlay.KindPidsGroup, 11, "구역 A", Straight, false, "#FF0000", 2,
            true, null, null, true, EnumCompositeEventStatus.Detecting, true, true, 0);
        var set = Map3DOverlay.Diff(Map3DOverlay.ToJson(a), Map3DOverlay.ToJson(a with { ActiveSection = (0.1, 0.2) }))!;
        Assert.Equal(new[] { "state" }, set.Properties().Select(p => p.Name).ToArray());
        Assert.Equal(new[] { "activeSection" }, ((JObject)set["state"]!).Properties().Select(p => p.Name).ToArray());
    }
}
