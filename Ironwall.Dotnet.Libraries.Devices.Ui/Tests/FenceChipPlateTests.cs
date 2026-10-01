using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 센서 번호판을 몸 아래로(2026-10-01 · 사용자: "80100 이 글씨 아래로 내려라" · "계속 가리잖아") — 몸과 겹치지 않음 · 선택 윤곽이 번호판까지 ·
/// 땅 밑으로 빠지면 몸 위 · 땅 번호 줄 머리 틈.
/// </summary>
public class FenceChipPlateTests
{
    private static FenceSensor Sensor(EnumDeviceType type, WiringFacing facing = WiringFacing.Front)
        => new(1, 1, 80100, "센서", type, null, 1, 1, null, false, false, false, facing);

    private static Rect Span(IEnumerable<Point> points)
    {
        var list = points.ToList();
        return new Rect(new Point(list.Min(p => p.X), list.Min(p => p.Y)), new Point(list.Max(p => p.X), list.Max(p => p.Y)));
    }

    private static readonly FenceInk[] BodyInks =
    {
        FenceInk.OliveFront, FenceInk.OliveSide, FenceInk.OliveTop, FenceInk.GlandFront, FenceInk.GlandSide, FenceInk.GlandTop, FenceInk.Pir,
    };

    [Theory]
    [InlineData(EnumDeviceType.SmartSensor2, 1.0, 1.0)]
    [InlineData(EnumDeviceType.SmartSensor2, 0.0, 1.0)]
    [InlineData(EnumDeviceType.SmartSensor2, 1.0, 0.4)]
    [InlineData(EnumDeviceType.Multi, 1.0, 1.0)]
    [InlineData(EnumDeviceType.Multi, 1.0, 0.5)]
    [InlineData(EnumDeviceType.Fence, 1.0, 1.0)]
    [InlineData(EnumDeviceType.Fence, 0.0, 0.5)]
    public void should_put_the_number_plate_fully_below_the_sensor_body_and_wrap_both_in_the_selection(EnumDeviceType type, double k, double zoom)
    {
        // 기둥 위 높이로 올린 칩(땅에서 충분히 높다)
        var chip = FenceScene.Sensor(Sensor(type), WiringShape.Ring, new FenceProjector(k), selected: true, zoom, lift: 60);

        var plate = Span(chip.Shapes.Single(s => s.Ink == FenceInk.Plate).Points);
        var body = Span(chip.Shapes.Where(s => BodyInks.Contains(s.Ink)).SelectMany(s => s.Points));
        var select = Span(chip.Shapes.Single(s => s.Ink == FenceInk.Select).Points);

        Assert.True(plate.Top > body.Bottom, $"번호판 {plate} · 몸 {body}");                      // 몸을 가리지 않는다
        Assert.True(plate.Top - body.Bottom < 12 / zoom + 1, "번호판은 몸 바로 아래(틈만큼)");
        Assert.True(select.Contains(plate) && select.Contains(body), "선택 윤곽이 몸 + 번호판을 감싼다");
        Assert.True(chip.Hit.Contains(plate), "번호판도 칩을 누르는 자리");
        Assert.Contains(chip.Shapes, s => s.Kind == FenceShapeKind.Text && s.Text == "80100");
    }

    [Fact]
    public void should_flip_the_plate_above_the_body_when_below_would_go_under_the_ground()
    {
        // 땅 바로 위(낮은 자리)의 스마트 — 아래에 두면 땅 밑으로 빠진다
        var low = FenceScene.Sensor(Sensor(EnumDeviceType.SmartSensor2), WiringShape.Ring, FenceProjector.Tilt, false, 1, lift: -40);

        var plate = Span(low.Shapes.Single(s => s.Ink == FenceInk.Plate).Points);
        var body = Span(low.Shapes.Where(s => BodyInks.Contains(s.Ink)).SelectMany(s => s.Points));

        Assert.True(plate.Bottom < body.Top, $"번호판 {plate} · 몸 {body}");
    }

    [Fact]
    public void should_put_the_plate_above_the_body_when_asked_so_it_cannot_reach_a_razor_coil_below()
    {
        var chip = FenceScene.Sensor(Sensor(EnumDeviceType.SmartSensor2), WiringShape.Ring, FenceProjector.Tilt, false, 1, lift: 120, plateAbove: true);

        var plate = Span(chip.Shapes.Single(s => s.Ink == FenceInk.Plate).Points);
        var body = Span(chip.Shapes.Where(s => BodyInks.Contains(s.Ink)).SelectMany(s => s.Points));

        Assert.True(plate.Bottom < body.Top);
    }

    [Fact]
    public void should_keep_a_gap_between_the_ground_number_caption_and_the_first_number()
    {
        var vm = WiringFenceHeightTests.Build("SSSS");
        var world = FenceWorld.FromLayout(vm.FenceChain, vm.FenceSensors(), vm.FenceLayout);

        var shapes = FenceScene.StaticLayout(world, FenceProjector.Tilt, false, false, world.ControllerX, 0, 1);
        var caption = shapes.Single(s => s.Kind == FenceShapeKind.Text && s.Text == "번호");
        var first = shapes.Where(s => s.Ink == FenceInk.PostNumber && s.Kind == FenceShapeKind.Text).OrderBy(s => s.Points[0].X).First();
        var firstLeft = first.Points[0].X - FenceScene.EstimateWidth(first.Text!, first.FontSize) / 2;

        Assert.True(caption.Points[0].X <= firstLeft - FenceScene.NUMBER_CAPTION_GAP + 1e-6, $"머리 끝 {caption.Points[0].X} · 첫 번호 왼쪽 {firstLeft}");
    }
}
