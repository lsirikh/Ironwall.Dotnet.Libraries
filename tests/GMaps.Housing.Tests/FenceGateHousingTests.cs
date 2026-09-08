using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>PRD FR-11/FR-12 — 통문·함체 문 관절(HousingJoint), DoorOpen 경량 DP, 거울 규약, R-06 닫힌 크기 보존.</summary>
public class FenceGateHousingTests
{
    private static IEnumerable<Point3D> Rotated(HousingModel model, HousingJoint joint, double open)
    {
        var rotation = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), HousingMath.DoorAngle(joint, open, model.DoorOpenAngle)), model.Pivot(joint));
        return model.Parts.Where(p => p.Joint == joint).SelectMany(p => p.Mesh.Positions).Select(v => rotation.Transform(v));
    }

    [Fact]
    public void should_route_gate_device_key_to_fencegate_not_infra_gate()
    {
        Assert.Equal("fencegate", HousingModels.DeviceKey(EnumDeviceType.Gate));
        var gate = HousingModels.Get("fencegate"); var building = HousingModels.Get("infra.gate");
        Assert.NotEqual(building.Bounds, gate.Bounds);
        Assert.True(gate.HasDoor); Assert.False(building.HasDoor);
        Assert.Equal(FenceDefaults.GateOpenAngleDeg, gate.DoorOpenAngle);
        Assert.Contains(gate.Parts, p => p.Joint == HousingJoint.DoorLeft); Assert.Contains(gate.Parts, p => p.Joint == HousingJoint.DoorRight);
        Assert.Contains(gate.Parts, p => p.Material == "mat_status"); Assert.Contains(gate.Parts, p => p.Material == "mat_mesh");
        Assert.Equal(-.43, gate.Pivot(HousingJoint.DoorLeft).X, 9); Assert.Equal(.43, gate.Pivot(HousingJoint.DoorRight).X, 9);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, -80, 80)]
    [InlineData(.5, -40, 40)]
    [InlineData(2, -80, 80)]          // 범위 밖은 클램프
    [InlineData(double.NaN, 0, 0)]    // 비정상 값은 닫힘
    public void should_mirror_door_angles_when_open_fraction_given(double open, double left, double right)
    {
        Assert.Equal(left, HousingMath.DoorAngle(HousingJoint.DoorLeft, open, -80), 9);
        Assert.Equal(right, HousingMath.DoorAngle(HousingJoint.DoorRight, open, -80), 9);
        Assert.Equal(0, HousingMath.DoorAngle(HousingJoint.Head, open, -80));
    }

    [Fact]
    public void should_swing_gate_leaves_outward_and_symmetric_when_door_open()
    {
        var gate = HousingModels.Get("fencegate");
        double closedLeft = Rotated(gate, HousingJoint.DoorLeft, 0).Max(p => p.Z), closedRight = Rotated(gate, HousingJoint.DoorRight, 0).Max(p => p.Z);
        Assert.InRange(closedLeft, -.1, .1); Assert.InRange(closedRight, -.1, .1);
        var openLeft = Rotated(gate, HousingJoint.DoorLeft, 1).ToList(); var openRight = Rotated(gate, HousingJoint.DoorRight, 1).ToList();
        Assert.True(openLeft.Max(p => p.Z) > .3, "좌측 문짝 자유단이 +Z(전면)로 나가야 한다");
        Assert.True(openRight.Max(p => p.Z) > .3, "우측 문짝 자유단이 +Z(전면)로 나가야 한다");
        // 거울 대칭: 좌측 열린 문짝의 X 범위는 우측의 부호 반전과 일치
        Assert.Equal(-openRight.Max(p => p.X), openLeft.Min(p => p.X), 6);
        Assert.Equal(-openRight.Min(p => p.X), openLeft.Max(p => p.X), 6);
    }

    [Theory]
    [InlineData(32, 32)]
    [InlineData(80, 24)]
    [InlineData(25, 90)]
    public void should_keep_open_door_inside_fit_box_for_all_yaw(double width, double height)
    {
        // 통문은 열림 스윕이 bounds 에 포함되므로 어떤 개폐 단계·yaw 에서도 박스 안에 머문다.
        var model = HousingModels.Get("fencegate"); var b = model.Bounds;
        double scale = HousingMath.Fit(width, height, b.SizeX, b.SizeY, b.SizeZ);
        foreach (double open in new[] { 0, .25, .5, .75, 1 })
        {
            var points = model.Parts.Where(p => p.Joint == HousingJoint.None).SelectMany(p => p.Mesh.Positions)
                .Concat(Rotated(model, HousingJoint.DoorLeft, open)).Concat(Rotated(model, HousingJoint.DoorRight, open));
            for (int yaw = 0; yaw < 360; yaw += 15)
                foreach (var p in points)
                {
                    var q = HousingMath.Project(p.X - b.X - b.SizeX / 2, p.Y - b.Y, p.Z - b.Z - b.SizeZ / 2, yaw, scale, width, height, b.SizeY);
                    Assert.InRange(q.x, -.0001, width + .0001); Assert.InRange(q.y, -.0001, height + .0001);
                }
        }
    }

    [Fact]
    public void should_swing_enclosure_door_outward_when_open()
    {
        var box = HousingModels.Get("enclosure");
        Assert.True(box.HasDoor); Assert.Equal(FenceDefaults.EnclosureOpenAngleDeg, box.DoorOpenAngle);
        Assert.Equal(new Point3D(-.26, 0, .2), box.Pivot(HousingJoint.DoorLeft));
        Assert.DoesNotContain(box.Parts, p => p.Joint == HousingJoint.DoorRight);
        var doorParts = box.Parts.Where(p => p.Joint == HousingJoint.DoorLeft).ToList();
        Assert.Contains(doorParts, p => p.Material == "mat_body"); Assert.Contains(doorParts, p => p.Material == "mat_status");
        double closedMaxZ = Rotated(box, HousingJoint.DoorLeft, 0).Max(p => p.Z);
        double openMaxZ = Rotated(box, HousingJoint.DoorLeft, 1).Max(p => p.Z);
        Assert.True(openMaxZ > closedMaxZ + .3, $"자유단이 +Z 로 나가야 한다 (closed {closedMaxZ:F3} → open {openMaxZ:F3})");
    }

    [Fact]
    public void should_keep_enclosure_closed_bounds_when_door_joint_added()
    {
        // R-06: 함체는 열림 스윕을 bounds 에 넣지 않는다 — 닫힌 형상의 union 과 정확히 같아야 한다(크기 회귀 방지).
        var box = HousingModels.Get("enclosure");
        var closed = Rect3D.Empty; foreach (var part in box.Parts) closed.Union(part.Mesh.Bounds);
        Assert.Equal(closed.SizeX, box.Bounds.SizeX, 9); Assert.Equal(closed.SizeY, box.Bounds.SizeY, 9); Assert.Equal(closed.SizeZ, box.Bounds.SizeZ, 9);
        Assert.InRange(box.Bounds.SizeZ, .4, .6);   // 종전 닫힌 깊이(.48 베이스) 유지
    }

    [Fact]
    public void should_not_raise_projection_changed_when_door_open_animates() => HousingTests.Sta(() =>
    {
        var view = new HousingVisual { ModelKey = "fencegate" };
        HousingTests.Layout(view, 83, 41);
        int raised = 0; view.ProjectionChanged += (_, _) => raised++;
        view.DoorOpen = 1;
        Assert.Equal(0, raised);
        Assert.Equal((-80d, 80d), view.DoorAngles);
        view.DoorOpen = .5; Assert.Equal((-40d, 40d), view.DoorAngles);
        view.DoorOpen = 0; Assert.Equal((0d, 0d), view.DoorAngles);
        Assert.Equal(0, raised);
        // 문 관절 파트는 경첩 피벗을 중심으로 회전한다
        var viewport = Assert.IsType<Viewport3D>(view.Children[0]);
        var scene = Assert.IsType<Model3DGroup>(viewport.Children[0].GetValue(ModelVisual3D.ContentProperty));
        var objects = Assert.IsType<Model3DGroup>(scene.Children.Last());
        var hinges = objects.Children.OfType<GeometryModel3D>().Select(g => g.Transform).OfType<RotateTransform3D>().Select(r => Math.Round(r.CenterX, 3)).Distinct().OrderBy(x => x).ToList();
        Assert.Equal(new[] { -.43, .43 }, hinges);
    });

    [Fact]
    public void should_keep_door_closed_when_model_has_no_door() => HousingTests.Sta(() =>
    {
        var view = new HousingVisual { ModelKey = "camera", DoorOpen = 1 };
        HousingTests.Layout(view, 83, 41);
        Assert.Equal((0d, 0d), view.DoorAngles);
    });

    [Fact]
    public void should_recolor_status_and_mesh_materials_without_rebuild() => HousingTests.Sta(() =>
    {
        var view = new HousingVisual { ModelKey = "fencegate" };
        HousingTests.Layout(view, 83, 41);
        var before = HousingModels.Get("fencegate").Parts.First(p => p.Material == "mat_status").Mesh;
        view.StatusBrush = Brushes.Red;
        var viewport = Assert.IsType<Viewport3D>(view.Children[0]);
        var scene = Assert.IsType<Model3DGroup>(viewport.Children[0].GetValue(ModelVisual3D.ContentProperty));
        var objects = Assert.IsType<Model3DGroup>(scene.Children.Last());
        var materials = objects.Children.OfType<GeometryModel3D>().Select(g => g.Material).OfType<MaterialGroup>().SelectMany(m => m.Children).OfType<DiffuseMaterial>().ToList();
        Assert.Contains(materials, m => ReferenceEquals(m.Brush, Brushes.Red));                         // mat_status → StatusBrush
        Assert.Contains(materials, m => m.Brush is SolidColorBrush s && s.Color.A < 255);                 // mat_mesh → 반투명
        Assert.Same(before, HousingModels.Get("fencegate").Parts.First(p => p.Material == "mat_status").Mesh);   // 공유 메시 불변
    });
}
