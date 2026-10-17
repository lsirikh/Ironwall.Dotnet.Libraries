using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 9점 격자 배치(2026-10-01 · 사용자: "1개의 판망을 중심으로 9개의 포인트 · 끌면 빨강 포인트 · 스냅 · 멀티셀렉트로 함께") — 격자 모양 · 옛 자리 옮기기 ·
/// 화면 반경 맞추기 · 여럿 옮기기 계획 · Ctrl+←/→ · 3×3 고르개 · 열 차례.
/// </summary>
public class FenceGridTests
{
    private static IReadOnlyList<FencePanelSpec> Panels(params EnumFenceStyle[] styles) => styles.Select(s => FencePanelSpec.Default(s)).ToList();

    #region - Geometry -
    [Fact]
    public void should_offer_nine_points_per_mesh_panel_inset_from_the_posts_and_two_per_standing_post_for_non_fence_sensors()
    {
        var panels = Panels(EnumFenceStyle.ChainLink, EnumFenceStyle.DesignFence);
        var geometry = FenceLayoutMath.Geometry(panels);

        var points = FenceLayoutMath.GridPoints(panels, FenceSensorCategory.Smart);

        Assert.Equal(2 * 9 + 3 * 2, points.Count);
        var panel0 = points.Where(p => p.Mount.IsPanelSpot && p.Mount.Panel == 0).ToList();
        Assert.Equal(9, panel0.Count);
        var xs = panel0.Select(p => FenceLayoutMath.PointOf(p.Mount, geometry).XM).Distinct().OrderBy(x => x).ToList();
        var span = geometry.Panels[0].SpanM;
        Assert.Equal(new[] { span * 0.2, span * 0.5, span * 0.8 }, xs, new Tolerance(1e-9));          // 기둥에서 안쪽으로 들인 세 열
        var heights = panel0.Select(p => FenceLayoutMath.PointOf(p.Mount, geometry).HeightM).Distinct().OrderBy(h => h).ToList();
        var h = geometry.Panels[0].Spec.HeightM;
        Assert.Equal(new[] { h * 0.2, h * 0.5, h * 0.8 }, heights, new Tolerance(1e-9));               // 하단 · 중단 · 상단
        Assert.Equal(new[] { 0, 4, 8 }, points.Where(p => p.Mount.IsPostSpot).Select(p => p.Cell.Gx).Distinct());
    }

    [Fact]
    public void should_offer_nine_wall_face_points_and_no_posts_between_walls()
    {
        var walls = Panels(EnumFenceStyle.Brick, EnumFenceStyle.Concrete);

        var points = FenceLayoutMath.GridPoints(walls, FenceSensorCategory.Smart);

        Assert.Equal(18, points.Count);                                                       // 담 사이에는 기둥이 없다
        Assert.All(points, p => Assert.True(SensorMountSpec.IsWall(p.Mount.Spot)));
        Assert.Equal(new[] { FenceMountSpot.WallBottom, FenceMountSpot.WallFace, FenceMountSpot.WallTop },
            points.Where(p => p.Mount.Panel == 1 && p.Mount.Column == FenceColumn.Left).Select(p => p.Mount.Spot));
    }

    [Fact]
    public void should_add_three_coil_points_on_razor_panels_for_fence_sensors_only_and_keep_fence_sensors_off_posts()
    {
        var razor = Panels(EnumFenceStyle.ChainLinkRazor, EnumFenceStyle.ChainLink);

        var fence = FenceLayoutMath.GridPoints(razor, FenceSensorCategory.Fence);
        var smart = FenceLayoutMath.GridPoints(razor, FenceSensorCategory.Smart);

        Assert.Equal(3, fence.Count(p => p.Mount.Spot == FenceMountSpot.RazorCoil));
        Assert.All(fence.Where(p => p.Mount.Spot == FenceMountSpot.RazorCoil), p => Assert.Equal((FenceLane.Upper, 0, 3), (p.Mount.Lane, p.Mount.Panel, p.Cell.Gy)));
        Assert.DoesNotContain(fence, p => p.Mount.IsPostSpot);
        Assert.DoesNotContain(smart, p => p.Mount.Spot == FenceMountSpot.RazorCoil);
        Assert.Equal(18, fence.Count - 3);
    }
    #endregion

    #region - Migration (옛 자리) -
    [Theory]
    [InlineData("PanelCenter", 2, FenceColumn.Center, 1)]       // 망 가운데 → 가운데 · 중단
    [InlineData("PanelBottom", 2, FenceColumn.Center, 0)]       // 망 아래 → 가운데 · 하단
    [InlineData("PostTop", 0, FenceColumn.Center, 2)]           // 기둥 위 → 기둥 줄 위
    [InlineData("PostMiddle", 0, FenceColumn.Center, 1)]        // 기둥 중간 → 기둥 줄 중간
    [InlineData("WallFace", 2, FenceColumn.Center, 1)]
    [InlineData("WallTop", 2, FenceColumn.Center, 2)]
    public void should_read_an_old_spot_without_a_column_as_the_matching_grid_point(string spot, int gxInPanel, FenceColumn column, int gy)
    {
        // 옛 문서 — column 칸이 없다
        var panels = spot.StartsWith("Wall") ? Panels(EnumFenceStyle.Brick, EnumFenceStyle.Brick) : Panels(EnumFenceStyle.ChainLink, EnumFenceStyle.ChainLink);
        var json = "{\"schema\":2,\"controller_id\":1,\"panels\":" + Newtonsoft.Json.JsonConvert.SerializeObject(panels)
                   + ",\"mounts\":{\"7\":{\"panel\":1,\"spot\":\"" + spot + "\",\"lane\":\"Lower\"}}}";

        var mount = FenceLayoutJson.Deserialize(json)!.Mounts[7];

        Assert.Equal(column, mount.Column);
        Assert.Equal(new FenceGridCell(4 + gxInPanel, gy), FenceLayoutMath.GridOf(mount));
    }

    [Fact]
    public void should_read_an_old_coil_spot_as_the_centre_coil_point_and_keep_the_column_through_json()
    {
        var doc = new FenceLayoutDocument
        {
            Panels = Panels(EnumFenceStyle.ChainLinkRazor).ToList(),
            Mounts = new Dictionary<int, SensorMountSpec>
            {
                [1] = new(0, FenceMountSpot.RazorCoil, Lane: FenceLane.Upper),
                [2] = new(0, FenceMountSpot.PanelTop, Column: FenceColumn.Right),
            },
        };

        var back = FenceLayoutJson.Deserialize(FenceLayoutJson.Serialize(doc))!;

        Assert.Equal(new FenceGridCell(2, 3), FenceLayoutMath.GridOf(back.Mounts[1]));
        Assert.Equal(doc.Mounts[2], back.Mounts[2]);
    }
    #endregion

    #region - Snapping · planning -
    [Fact]
    public void should_snap_to_the_nearest_point_within_the_screen_radius_only()
    {
        var points = new[] { new Point(100, 100), new Point(130, 100), new Point(300, 300) };

        Assert.Equal(1, FenceGesture.NearestSnap(points, new Point(120, 104), 24));
        Assert.Equal(0, FenceGesture.NearestSnap(points, new Point(110, 100), 24));
        Assert.Equal(-1, FenceGesture.NearestSnap(points, new Point(200, 200), 24));
        Assert.Equal(-1, FenceGesture.NearestSnap(points, new Point(100, 125), 24));         // 반경 밖(25px)
    }

    [Fact]
    public void should_plan_a_group_move_by_the_same_grid_offset_and_block_members_that_land_off_grid_or_on_taken_points()
    {
        var panels = Panels(EnumFenceStyle.ChainLink, EnumFenceStyle.ChainLink);
        var moving = new Dictionary<int, SensorMountSpec>
        {
            [1] = new(0, FenceMountSpot.PostTop),                                            // gx 0
            [2] = new(1, FenceMountSpot.PostTop),                                            // gx 4
        };
        var occupied = new[] { (new FenceGridCell(6, 2), FenceLane.Lower) };

        var ok = FenceLayoutMath.PlanGridMove(moving, 1, new FenceGridCell(1, 2), _ => FenceSensorCategory.Smart, panels, null);
        var off = FenceLayoutMath.PlanGridMove(moving, 1, new FenceGridCell(5, 2), _ => FenceSensorCategory.Smart, panels, null);
        var taken = FenceLayoutMath.PlanGridMove(moving, 1, new FenceGridCell(2, 2), _ => FenceSensorCategory.Smart, panels, occupied);

        Assert.Equal(new SensorMountSpec(0, FenceMountSpot.PanelTop, Column: FenceColumn.Left), ok[1]);
        Assert.Equal(new SensorMountSpec(1, FenceMountSpot.PanelTop, Column: FenceColumn.Left), ok[2]);
        Assert.Equal(new SensorMountSpec(1, FenceMountSpot.PanelTop, Column: FenceColumn.Left), off[1]);     // 1: gx 0 → 5
        Assert.Null(off[2]);                                                                  // 2: gx 4 → 9 — 격자 밖(망 둘 · 끝 기둥 gx 8)
        Assert.NotNull(taken[1]);
        Assert.Null(taken[2]);                                                                // gx 6 에는 다른 센서
    }

    [Fact]
    public void should_put_a_fence_sensor_into_the_coil_on_the_upper_lane_and_back_to_the_lower_lane_by_grid_cell()
    {
        var razor = Panels(EnumFenceStyle.ChainLinkRazor);
        var mesh = new SensorMountSpec(0, FenceMountSpot.PanelTop, Column: FenceColumn.Left);

        var coil = FenceLayoutMath.MountAt(new FenceGridCell(1, 3), mesh, FenceSensorCategory.Fence, razor)!;
        var back = FenceLayoutMath.MountAt(new FenceGridCell(3, 0), coil, FenceSensorCategory.Fence, razor)!;

        Assert.Equal((FenceMountSpot.RazorCoil, FenceLane.Upper, FenceColumn.Left), (coil.Spot, coil.Lane, coil.Column));
        Assert.Equal((FenceMountSpot.PanelBottom, FenceLane.Lower, FenceColumn.Right), (back.Spot, back.Lane, back.Column));
        Assert.Null(FenceLayoutMath.MountAt(new FenceGridCell(1, 3), mesh, FenceSensorCategory.Smart, razor));   // 스마트는 코일 점이 없다
    }
    #endregion

    #region - Keyboard · order -
    [Fact]
    public void should_step_to_the_next_grid_point_crossing_from_the_right_column_to_the_next_post_and_panel()
    {
        var panels = Panels(EnumFenceStyle.ChainLink, EnumFenceStyle.ChainLink);
        var m = new SensorMountSpec(0, FenceMountSpot.PanelCenter, Column: FenceColumn.Right);              // gx 3 · gy 1

        var post = FenceLayoutMath.StepGridX(m, 1, FenceSensorCategory.Smart, panels);
        var next = FenceLayoutMath.StepGridX(post, 1, FenceSensorCategory.Smart, panels);
        var back = FenceLayoutMath.StepGridX(next, -1, FenceSensorCategory.Smart, panels);
        var bottom = FenceLayoutMath.StepGridX(new SensorMountSpec(0, FenceMountSpot.PanelBottom, Column: FenceColumn.Right), 1, FenceSensorCategory.Smart, panels);
        var end = new SensorMountSpec(2, FenceMountSpot.PostTop);

        Assert.Equal(new SensorMountSpec(1, FenceMountSpot.PostMiddle), post);                // 같은 줄(중단)의 다음 점 = 기둥 1 중간
        Assert.Equal(new SensorMountSpec(1, FenceMountSpot.PanelCenter, Column: FenceColumn.Left), next);
        Assert.Equal(post, back);
        Assert.Equal(new SensorMountSpec(1, FenceMountSpot.PanelBottom, Column: FenceColumn.Left), bottom);   // 기둥에는 하단이 없어 건너뛴다
        Assert.Same(end, FenceLayoutMath.StepGridX(end, 1, FenceSensorCategory.Smart, panels));               // 끝
    }

    [Fact]
    public void should_order_sensors_in_one_panel_by_column_along_the_lane_direction()
    {
        var mounts = new[]
        {
            (1, new SensorMountSpec(0, FenceMountSpot.PanelCenter, Column: FenceColumn.Right)),
            (2, new SensorMountSpec(0, FenceMountSpot.PanelBottom, Column: FenceColumn.Left)),
            (3, new SensorMountSpec(0, FenceMountSpot.PanelTop, Lane: FenceLane.Upper, Column: FenceColumn.Left)),
            (4, new SensorMountSpec(0, FenceMountSpot.PanelTop, Lane: FenceLane.Upper, Column: FenceColumn.Right)),
        };

        var order = FenceLayoutMath.ChainOrder(mounts, FenceControllerEnd.Left);

        Assert.Equal(new[] { 2, 1, 4, 3 }, order);                                              // 아래 줄 왼쪽 → 오른쪽 · 위 줄 오른쪽 → 왼쪽
    }
    #endregion

    #region - 3×3 picker -
    [Fact]
    public void should_move_selected_sensors_to_the_picked_point_of_their_own_panel_in_one_undo_step()
    {
        var vm = WiringFenceHeightTests.Build("SSSS");                                       // 기둥 0 … 3
        vm.FenceSelectSensors(new[] { 102, 104 });                                            // 104 는 끝 기둥 → 왼쪽 망

        var ok = vm.FenceSetGridPoint(FenceColumn.Left, 0);
        var moved = (vm.FenceLayout.MountOf(102)!, vm.FenceLayout.MountOf(104)!);
        vm.FenceSelect(102);
        var picked = vm.IsGridLeftBottom;
        vm.Undo();

        Assert.True(ok);
        Assert.Equal(new SensorMountSpec(1, FenceMountSpot.PanelBottom, Column: FenceColumn.Left), moved.Item1);
        Assert.Equal(new SensorMountSpec(2, FenceMountSpot.PanelBottom, Column: FenceColumn.Left), moved.Item2);
        Assert.True(picked);
        Assert.Equal(FenceMountSpot.PostTop, vm.FenceLayout.MountOf(102)!.Spot);
        Assert.Equal(FenceMountSpot.PostTop, vm.FenceLayout.MountOf(104)!.Spot);
    }

    [Fact]
    public void should_name_the_column_in_the_mount_text()
    {
        var vm = WiringFenceHeightTests.Build("SSSS");
        vm.FenceSelect(102);

        vm.FenceSetGridPoint(FenceColumn.Right, 2);

        Assert.Equal("망 2 · 오른쪽 · 망 위", vm.MountPlaceText);
        Assert.True(vm.IsGridRightTop);
    }
    #endregion

    [Fact]
    public void should_wire_nine_styled_picker_buttons_and_the_yaw_buttons_in_the_property_pane()
    {
        var root = System.IO.Path.GetFullPath(System.IO.Path.Combine(ThisDirectory(), "..", ".."));
        var xaml = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Ironwall.Dotnet.Libraries.Devices.Ui", "Consoles", "Wiring", "WiringView.xaml"));

        foreach (var column in new[] { "Left", "Center", "Right" })
            foreach (var row in new[] { "Top", "Middle", "Bottom" })
            {
                var tag = System.Text.RegularExpressions.Regex.Match(xaml, $"<Button[^>]*AutomationId=\"Devices\\.Wiring\\.Fence\\.Grid\\.{column}\\.{row}\"[^>]*/>");
                Assert.True(tag.Success, $"{column}.{row}");
                Assert.Contains("Click=\"OnGridPoint\"", tag.Value);
                Assert.Contains("Style=\"{StaticResource Console.Button}\"", tag.Value);
            }
        foreach (var id in new[] { "Yaw.Cw", "Yaw.Ccw", "Yaw.0", "Yaw.90", "Yaw.180", "Yaw.270" })
            Assert.Contains($"AutomationProperties.AutomationId=\"Devices.Wiring.Fence.{id}\"", xaml);
    }

    private static string ThisDirectory([System.Runtime.CompilerServices.CallerFilePath] string? file = null) => System.IO.Path.GetDirectoryName(file)!;

    private sealed class Tolerance : IEqualityComparer<double>
    {
        private readonly double _eps;
        public Tolerance(double eps) => _eps = eps;
        public bool Equals(double a, double b) => System.Math.Abs(a - b) <= _eps;
        public int GetHashCode(double value) => 0;
    }
}
