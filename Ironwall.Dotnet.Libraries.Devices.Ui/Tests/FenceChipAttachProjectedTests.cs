using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.SensorPresets;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 불변식 "센서는 펜스 물체 밖 허공에 그려지지 않는다"를 <b>그림(투영) 좌표</b>로 — 실창 4회차(027_r24_fen001_plate): 세계 자리는 기둥 1 인데
/// 화면 벌리기가 붙은 칩 무리를 평균 자리로 나란히 세워 왼쪽 끝 75088 이 기둥 없는 허공 · 제어기 상자 위로 밀렸다.
/// 부모 없는 펜스 캔버스(창 없음)에 놓인 칩 자리 + 칩 그림의 브래킷 고정점이 그 물체(기둥 · 칸)의 그림 영역 안인지 센서 1~N 전부 단언한다.
/// 실행: <c>dotnet test Ironwall.Dotnet.Libraries.Devices.Ui/Ironwall.Dotnet.Libraries.Devices.Ui.csproj --filter "Scenario~FLT-0"</c>
/// </summary>
public class FenceChipAttachProjectedTests
{
    #region - Rig -
    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { failure = ex; }
            finally { StaCleanup.ShutdownDispatcher(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new AggregateException(failure);
        return result;
    }

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static ResourceDictionary Theme()
        => (ResourceDictionary)XamlReader.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", "Tokens.Light.xaml")));

    private static void Layout(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    private static FenceCanvas Canvas(WiringViewModel vm, double width, double height)
    {
        var canvas = new FenceCanvas { SuppressMenuPopup = true };
        canvas.Resources.MergedDictionaries.Add(Theme());
        var decorator = new System.Windows.Documents.AdornerDecorator { Child = canvas };
        canvas.ViewModel = vm;
        Layout(decorator, width, height);
        canvas.Rebuild();
        Layout(decorator, width, height);
        return canvas;
    }

    /// <summary>실창 시드와 같은 섞인 망 — 철망 · 철조망+윤형 · 벽돌 · 시멘트 · 디자인펜스 · 철망 … (펜스 길이 = 칸 수 × 3 m).</summary>
    private static readonly EnumFenceStyle[] SeedStyles =
    {
        EnumFenceStyle.ChainLink, EnumFenceStyle.ChainLinkRazor, EnumFenceStyle.Brick, EnumFenceStyle.Concrete, EnumFenceStyle.DesignFence,
        EnumFenceStyle.ChainLink, EnumFenceStyle.ChainLinkRazor, EnumFenceStyle.ChainLink,
    };

    /// <summary>
    /// 센서 <paramref name="sensors"/> 대 · 칸 <paramref name="panels"/> 개 — 앞 센서부터 기둥 위 · 망 가운데 · 기둥 중간 · 망 아래를 번갈아 왼쪽 끝부터 빽빽이(기둥 1 · 첫 칸 포함),
    /// 저장 · 다시 연 뒤처럼 스마트센서2 위치 프리셋으로 이관, 제어기는 <paramref name="end"/> 끝.
    /// </summary>
    private static WiringViewModel Seed(int sensors, int panels, FenceControllerEnd end, bool flat, double span = 3)
    {
        var vm = WiringFenceHeightTests.Build(new string('S', sensors));
        vm.AttachSensorPresets(new InMemorySensorPresetRepository());
        Assert.True(vm.LoadSensorPresetsAsync().GetAwaiter().GetResult());
        var specs = Enumerable.Range(0, panels).Select(i => new FencePanelSpec(SeedStyles[i % SeedStyles.Length], null, i % 3 == 1 ? 3.0 : 2.4, span)).ToArray();
        var spots = new[] { FenceMountSpot.PostTop, FenceMountSpot.PanelCenter, FenceMountSpot.PostMiddle, FenceMountSpot.PanelBottom };
        var keys = vm.FenceChain.Keys;
        var mounts = keys.Select((k, i) =>
        {
            var spot = spots[i % spots.Length];
            var post = spot is FenceMountSpot.PostTop or FenceMountSpot.PostMiddle;
            var at = Math.Min(i / 2, post ? panels : panels - 1);
            return (k, new SensorMountSpec(at, spot));
        }).ToDictionary(t => t.k, t => t.Item2);
        Assert.True(vm.Board.ApplyFenceEdit(l => l.WithPanels(specs).WithMounts(mounts)));
        var smart2 = vm.MountPresetsOf(SensorPresetCatalog.SMART2);
        Assert.True(vm.Board.ApplyFenceEdit(l => l.WithMounts(l.Mounts.ToDictionary(p => p.Key, p => WiringPresetPlan.Migrate(p.Value, SensorPresetCatalog.SMART2, smart2, l.Panels) with { MountPresetChosen = true }))));   // 사용자가 고른 프리셋(펜스 보기가 프리셋으로 그린다)
        Assert.All(keys, k => Assert.True(vm.FenceLayout.MountOf(k)!.HasMountPreset, $"이관 {k}"));
        if (end != vm.FenceLayout.ControllerEnd) Assert.True(vm.Board.ApplyFenceEdit(l => l.WithControllerEnd(end)));
        Assert.All(keys, k => Assert.True(vm.IsPresetMounted(k), $"붙음 {k}"));
        if (flat) vm.ChooseFlat();
        return vm;
    }

    /// <summary>
    /// 캔버스에 놓인 칩마다 — 붙은 센서면 칩 원점(= 그림의 붙는 점 x) · 브래킷 고정점이 그 물체 그림 안인가, 칩 몸이 제어기 상자와 겹치지 않나. 어긋난 것을 말로 돌려준다.
    /// </summary>
    private static List<string> Check(WiringViewModel vm, FenceCanvas canvas, string label)
    {
        var bad = new List<string>();
        var p = vm.IsFlat ? FenceProjector.Flat : FenceProjector.Tilt;
        var world = FenceWorld.FromLayout(vm.FenceChain, vm.FenceSensors(), vm.FenceLayout, vm.FenceSpacing, vm.FencePoseOf, vm.FencePresetBayOf);
        var geometry = world.Geometry!;
        var u = world.Upm;
        var controller = FenceScene.EndControllerBounds(p, world.ControllerX);
        controller.Inflate(-6, -6);                                                        // 누르는 자리 여유를 뺀 상자 · 받침 그림
        var mountedCount = 0;
        foreach (var key in vm.FenceChain.Keys)
        {
            if (world.MountMarkOf(key) is not { } mark || !canvas.SensorChips.TryGetValue(key, out var chip)) continue;
            mountedCount++;
            var anchor = System.Windows.Controls.Canvas.GetLeft(chip) - chip.HitBounds.X;   // 칩 원점 x(그림 좌표 · 깊이 0 이면 세계 x 와 같다)
            var name = $"{label} · 센서 {vm.FenceSensors()[key].Number}(사슬 {vm.FenceChain.Keys.ToList().IndexOf(key) + 1}/{vm.FenceChain.Keys.Count}) {mark.Anchor}";
            // 브래킷 고정점(물체 면 · 깊이 0) — 칩 그림 좌표에 칩 원점을 더한 그림 좌표
            var pin = chip.Picture?.Shapes.FirstOrDefault(s => s.Ink == FenceInk.MountBracket && s.Kind == FenceShapeKind.Ellipse)?.Points[0] is { } q
                ? new Point(q.X + anchor, q.Y)
                : p.P(anchor, mark.Height, 0);
            Rect area;
            if (SensorMountResolver.KindOf(mark.Anchor) == SensorAnchorKind.Post)
            {
                var post = geometry.Posts.Where(o => o.Exists).OrderBy(o => Math.Abs(o.XM * u - mark.X)).First();
                var half = FenceScene.CHAIN_POST_W / 2 + 0.5;
                var top = post.HeightM * world.Vpm + FenceWorld.POST_CAP_H;
                area = new Rect(p.P(post.XM * u - half, top + 0.5, 0), p.P(post.XM * u + half, -0.5, 0));
                if (Math.Abs(post.XM * u - mark.X) > 0.5) bad.Add($"{name}: 세계 자리 {mark.X:0.0} 에 기둥 없음");
            }
            else
            {
                var bay = geometry.Panels[vm.FencePresetBayOf(key)!.Value];
                area = new Rect(p.P(bay.StartM * u - 0.5, world.TopHeight, 0), p.P(bay.EndM * u + 0.5, -0.5, 0));
            }
            // 한 기둥을 나눠 쓰는 칩은 기둥 옆에 서고 고정점 → 몸까지 팔이 있어야 한다 · 그 밖은 칩 원점(붙는 점)이 곧 물체 위
            var origin = p.P(anchor, mark.Height, 0);
            if (Math.Abs(pin.X - origin.X) > 0.5)
            {
                var local = new Point(pin.X - anchor, pin.Y);
                var arm = chip.Picture!.Shapes.Any(s => s.Ink == FenceInk.MountBracket && s.Kind == FenceShapeKind.Line && s.Points.Length == 2
                                                       && (s.Points[0] - local).Length < 0.5 && Math.Abs(s.Points[1].X - p.P(0, mark.Height, 0).X) < 0.5);
                if (!arm) bad.Add($"{name}: 고정점 {pin.X:0.0} 과 칩 {origin.X:0.0} 사이에 팔이 없다(허공)");
            }
            else if (!area.Contains(new Point(anchor, area.Y + area.Height / 2))) bad.Add($"{name}: 칩 붙는 점 x {anchor:0.0} 이 물체 그림 {area.Left:0.0}~{area.Right:0.0} 밖(허공)");
            if (!area.Contains(pin)) bad.Add($"{name}: 브래킷 고정점 {pin.X:0.0},{pin.Y:0.0} 이 물체 그림 {area} 밖");
            var body = new Rect(System.Windows.Controls.Canvas.GetLeft(chip), System.Windows.Controls.Canvas.GetTop(chip), chip.Width, chip.Height);
            if (body.IntersectsWith(controller)) bad.Add($"{name}: 칩 몸 {body} 이 제어기 상자 {controller} 와 겹침");
        }
        if (mountedCount != vm.FenceChain.Keys.Count) bad.Add($"{label}: 붙은 센서 {mountedCount}/{vm.FenceChain.Keys.Count}(시험 전제 — 모두 프리셋으로 서야 한다)");
        return bad;
    }
    #endregion

    [Theory, Trait("Scenario", "FLT-001")]
    [InlineData(FenceControllerEnd.Left, false)]
    [InlineData(FenceControllerEnd.Left, true)]
    [InlineData(FenceControllerEnd.Right, false)]
    [InlineData(FenceControllerEnd.Right, true)]
    public void should_draw_every_one_of_the_twelve_seed_sensors_on_its_post_or_bay_at_both_ends_and_any_zoom(FenceControllerEnd end, bool flat)
    {
        // 실창 시드와 같은 구성 — 센서 12 · 섞인 망(철조망 칸 포함) · 왼쪽 끝 기둥 1 · 첫 칸에 둘. 칩이 커지는 좁은 캔버스(작은 배율)까지.
        var bad = OnSta(() =>
        {
            var vm = Seed(12, 8, end, flat);
            var found = new List<string>();
            foreach (var (w, h) in new[] { (1200.0, 520.0), (900.0, 420.0), (640.0, 360.0) })
                found.AddRange(Check(vm, Canvas(vm, w, h), $"{end} {(flat ? "평면" : "입체")} {w}px"));
            return found;
        });
        Assert.True(bad.Count == 0, string.Join(Environment.NewLine, bad));
    }

    [Theory, Trait("Scenario", "FLT-002")]
    [InlineData(4, 2)]
    [InlineData(6, 3)]
    [InlineData(9, 5)]
    [InlineData(16, 8)]
    [InlineData(20, 12)]
    public void should_keep_first_second_and_last_two_sensors_on_their_objects_for_various_lengths_and_bay_counts(int sensors, int panels)
    {
        var bad = OnSta(() =>
        {
            var found = new List<string>();
            foreach (var end in new[] { FenceControllerEnd.Left, FenceControllerEnd.Right })
                foreach (var flat in new[] { false, true })
                {
                    var vm = Seed(sensors, panels, end, flat);
                    found.AddRange(Check(vm, Canvas(vm, 900, 420), $"센서 {sensors} · 칸 {panels} · {end} {(flat ? "평면" : "입체")}"));
                }
            return found;
        });
        Assert.True(bad.Count == 0, string.Join(Environment.NewLine, bad));
    }

    [Fact, Trait("Scenario", "FLT-003")]
    public void should_still_spread_two_chips_sharing_one_bay_point_inside_that_bay()
    {
        // 벌리기를 칸 안으로 자르되 칸에 붙은 칩끼리는 계속 나란히 선다(MNT-R28-610 과 같은 처지) — 둘 다 그 칸 그림 안 · 겹치지 않음.
        // 칩 둘이 들어갈 만한 6 m 칸(3 m 칸은 칩 둘보다 좁아 칸 양 끝까지만 벌리고 겹친다 — 허공보다 붙음이 먼저).
        var result = OnSta(() =>
        {
            var vm = Seed(4, 3, FenceControllerEnd.Left, false, span: 6);
            var keys = vm.FenceChain.Keys;
            Assert.True(vm.Board.ApplyFenceEdit(l => l.WithMounts(l.Mounts.ToDictionary(p => p.Key, p => p.Key == keys[1] ? l.Mounts[keys[3]] : p.Value))));
            var canvas = Canvas(vm, 1200, 520);
            Rect R(int k) { var c = canvas.SensorChips[k]; return new Rect(System.Windows.Controls.Canvas.GetLeft(c), System.Windows.Controls.Canvas.GetTop(c), c.Width, c.Height); }
            return (Bad: Check(vm, canvas, "같은 점 둘"), Same: vm.FencePoseOf(keys[1]) == vm.FencePoseOf(keys[3]), A: R(keys[1]), B: R(keys[3]));
        });
        Assert.True(result.Bad.Count == 0, string.Join(Environment.NewLine, result.Bad));
        Assert.True(result.Same);
        Assert.True(Rect.Intersect(result.A, result.B) is var o && (o.IsEmpty || o.Width < 0.5), $"칩 겹침 {result.A} ⟷ {result.B}");
    }

    [Theory, Trait("Scenario", "FLT-004")]
    [InlineData(FenceControllerEnd.Left, false)]
    [InlineData(FenceControllerEnd.Left, true)]
    [InlineData(FenceControllerEnd.Right, false)]
    public void should_hang_two_chips_sharing_the_end_post_beside_it_on_arms_without_touching_the_controller(FenceControllerEnd end, bool flat)
    {
        // 제어기 쪽 끝 기둥(왼쪽 = 기둥 1 · 오른쪽 = 센서가 선 가장 오른쪽 기둥) 위에 둘 — 나란히(겹치지 않음) 서되 고정점은 기둥 위, 기둥 → 몸 팔, 제어기 상자와 겹치지 않음
        var result = OnSta(() =>
        {
            var vm = Seed(8, 3, end, flat);                                                  // 센서 1 = 기둥 1 · 센서 7 = 마지막 기둥(기둥 위)
            var keys = vm.FenceChain.Keys;
            var post = end == FenceControllerEnd.Left ? 0 : 3;
            var before = FenceWorld.FromLayout(vm.FenceChain, vm.FenceSensors(), vm.FenceLayout, vm.FenceSpacing, vm.FencePoseOf, vm.FencePresetBayOf);
            var endX = vm.FenceLayout.Geometry.Posts[post].XM * before.Upm;
            bool OnPost(int k) => before.MountMarkOf(k) is { } m && SensorMountResolver.KindOf(m.Anchor) == SensorAnchorKind.Post;
            var a = keys.OrderBy(k => OnPost(k) ? Math.Abs(before.MountMarkOf(k)!.X - endX) : double.MaxValue).First();          // 끝 기둥(에 가장 가까운 기둥)의 센서
            var b = keys.First(k => k != a && OnPost(k));                                                                         // 다른 기둥 센서를 그 자리로
            Assert.True(vm.Board.ApplyFenceEdit(l => l.WithMounts(l.Mounts.ToDictionary(p => p.Key, p => p.Key == b ? l.Mounts[a] : p.Value))));
            var canvas = Canvas(vm, 1200, 520);
            Rect R(int k) { var c = canvas.SensorChips[k]; return new Rect(System.Windows.Controls.Canvas.GetLeft(c), System.Windows.Controls.Canvas.GetTop(c), c.Width, c.Height); }
            var world = FenceWorld.FromLayout(vm.FenceChain, vm.FenceSensors(), vm.FenceLayout, vm.FenceSpacing, vm.FencePoseOf, vm.FencePresetBayOf);
            return (Bad: Check(vm, canvas, $"끝 기둥 둘 {end} {(flat ? "평면" : "입체")}"), A: R(a), B: R(b),
                    SamePost: world.MountMarkOf(a) is { } ma && world.MountMarkOf(b) is { } mb && Math.Abs(ma.X - mb.X) < 0.5);
        });
        Assert.True(result.SamePost, "시험 전제 — 둘이 한 기둥");
        Assert.True(result.Bad.Count == 0, string.Join(Environment.NewLine, result.Bad));
        Assert.True(Rect.Intersect(result.A, result.B) is var o && (o.IsEmpty || o.Width < 0.5), $"칩 겹침 {result.A} ⟷ {result.B}");
    }

    /// <summary>
    /// 9자리(위치 프리셋 아님) 센서 <paramref name="sensors"/> 대를 제어기 쪽 끝 두 칸 · 기둥 둘에 몰아 둔다(실창 r29 5회차 031 — 섞인 칸 + 끝에 몰린 배치).
    /// </summary>
    private static WiringViewModel SeedNine(int sensors, int panels, FenceControllerEnd end, bool flat)
    {
        var vm = WiringFenceHeightTests.Build(new string('S', sensors));
        var specs = Enumerable.Range(0, panels).Select(i => new FencePanelSpec(SeedStyles[i % SeedStyles.Length], null, i % 3 == 1 ? 3.0 : 2.4, 3)).ToArray();
        var spots = new[] { FenceMountSpot.PostTop, FenceMountSpot.PanelCenter, FenceMountSpot.PostMiddle, FenceMountSpot.PanelBottom };
        var keys = vm.FenceChain.Keys;
        var mounts = keys.Select((k, i) =>
        {
            var spot = spots[i % spots.Length];
            var post = spot is FenceMountSpot.PostTop or FenceMountSpot.PostMiddle;
            var near = i / spots.Length % 2;                                                  // 끝에서 0 · 1 번째 기둥 / 칸
            var at = end == FenceControllerEnd.Left ? near : (post ? panels - near : panels - 1 - near);
            return (k, new SensorMountSpec(at, spot));
        }).ToDictionary(t => t.k, t => t.Item2);
        Assert.True(vm.Board.ApplyFenceEdit(l => l.WithPanels(specs).WithMounts(mounts)));
        if (end != vm.FenceLayout.ControllerEnd) Assert.True(vm.Board.ApplyFenceEdit(l => l.WithControllerEnd(end)));
        Assert.All(keys, k => Assert.False(vm.IsPresetMounted(k), $"9자리 {k}"));
        if (flat) vm.ChooseFlat();
        return vm;
    }

    /// <summary>9자리 칩마다 — 붙는 점 x 가 펜스 시작 ~ 끝 안, 칩 몸이 제어기 상자와 안 겹침, 칩끼리 안 겹침. 어긋난 것을 말로 돌려준다.</summary>
    private static List<string> CheckNine(WiringViewModel vm, FenceCanvas canvas, string label)
    {
        var bad = new List<string>();
        var p = vm.IsFlat ? FenceProjector.Flat : FenceProjector.Tilt;
        var world = FenceWorld.FromLayout(vm.FenceChain, vm.FenceSensors(), vm.FenceLayout, vm.FenceSpacing, vm.FencePoseOf, vm.FencePresetBayOf);
        var start = p.P(0, 0, 0).X;
        var end = p.P(world.Geometry!.LengthM * world.Upm, 0, 0).X;
        var controller = FenceScene.EndControllerBounds(p, world.ControllerX);
        controller.Inflate(-6, -6);
        var bodies = new List<(string Name, Rect Body)>();
        foreach (var key in vm.FenceChain.Keys)
        {
            if (!canvas.SensorChips.TryGetValue(key, out var chip)) { bad.Add($"{label} · {key}: 칩 없음"); continue; }
            var anchor = System.Windows.Controls.Canvas.GetLeft(chip) - chip.HitBounds.X;
            var name = $"{label} · 센서 {vm.FenceSensors()[key].Number}(사슬 {vm.FenceChain.Keys.ToList().IndexOf(key) + 1}/{vm.FenceChain.Keys.Count}) {vm.FenceLayout.MountOf(key)}";
            if (anchor < start - 0.5 || anchor > end + 0.5) bad.Add($"{name}: 붙는 점 x {anchor:0.0} 이 펜스 {start:0.0}~{end:0.0} 밖(허공)");
            var body = new Rect(System.Windows.Controls.Canvas.GetLeft(chip), System.Windows.Controls.Canvas.GetTop(chip), chip.Width, chip.Height);
            if (body.IntersectsWith(controller)) bad.Add($"{name}: 칩 몸 {body} 이 제어기 상자 {controller} 와 겹침");
            foreach (var (other, rect) in bodies)
                if (Rect.Intersect(body, rect) is var o && !o.IsEmpty && o.Width >= 0.5 && o.Height >= 0.5) bad.Add($"{name}: 칩 {body} 이 {other} {rect} 와 겹침");
            bodies.Add((name, body));
        }
        return bad;
    }

    [Theory, Trait("Scenario", "FLT-010")]
    [InlineData(12, 8, FenceControllerEnd.Left, false, 1200)]
    [InlineData(12, 8, FenceControllerEnd.Left, true, 640)]
    [InlineData(12, 8, FenceControllerEnd.Right, false, 640)]
    [InlineData(12, 8, FenceControllerEnd.Right, true, 1200)]
    [InlineData(8, 3, FenceControllerEnd.Left, false, 640)]
    [InlineData(8, 3, FenceControllerEnd.Right, true, 640)]
    [InlineData(8, 8, FenceControllerEnd.Left, true, 1200)]
    [InlineData(16, 12, FenceControllerEnd.Left, false, 1200)]
    [InlineData(16, 12, FenceControllerEnd.Right, true, 1200)]
    public void should_keep_nine_point_sensor_chips_bunched_at_the_controller_end_on_the_fence_off_the_controller_and_apart(
        int sensors, int panels, FenceControllerEnd end, bool flat, double width)
    {
        var bad = OnSta(() =>
        {
            var vm = SeedNine(sensors, panels, end, flat);
            return CheckNine(vm, Canvas(vm, width, width * 0.45), $"9자리 {sensors}대 · 칸 {panels} · {end} {(flat ? "평면" : "입체")} {width}px");
        });
        Assert.True(bad.Count == 0, string.Join(Environment.NewLine, bad));
    }
}
