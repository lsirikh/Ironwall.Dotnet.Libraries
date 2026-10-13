using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.SensorPresets;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 불변식 "센서는 늘 실제 물체 하나에 붙는다"(sensor-mount-preset FR-22) — 결선 펜스 보기(입체 · 평면) · 편집기 · 결선 저장 · 펜스 끝 제어기.
/// 창을 띄우지 않는다(뷰모델 · 순수 함수 · 그림 목록 · 부모 없는 렌더). 실행:
/// <c>dotnet test Ironwall.Dotnet.Libraries.Devices.Ui/Ironwall.Dotnet.Libraries.Devices.Ui.csproj --filter "Scenario~MNT-4"</c>
/// </summary>
public class SensorMountAttachViewTests
{
    /// <summary>섞인 펜스 — 철조망+윤형(2.4 · 3.0 · 1.8) · 철망(1.8 · 3.0) · 벽돌 · 시멘트 · 디자인펜스.</summary>
    private static readonly FencePanelSpec[] Mixed =
    {
        new(EnumFenceStyle.ChainLinkRazor, null, 2.4, 3), new(EnumFenceStyle.ChainLink, null, 1.8, 3), new(EnumFenceStyle.Brick, null, 2.4, 3),
        new(EnumFenceStyle.Concrete, null, 3.0, 3), new(EnumFenceStyle.DesignFence, null, 2.4, 3), new(EnumFenceStyle.ChainLinkRazor, null, 3.0, 3),
        new(EnumFenceStyle.ChainLink, null, 3.0, 3), new(EnumFenceStyle.ChainLinkRazor, null, 1.8, 3),
    };

    private sealed record Scene(FenceWorld World, int Key, SensorMountPose Pose, FencePanelGeometry Bay, FenceSensor Sensor);

    /// <summary>센서 하나(첫 키)를 모델 <paramref name="model"/> · 프리셋 · 칸 · 따로 고친 값으로 세운 세계(나머지는 9자리).</summary>
    private static Scene Stand(WiringViewModel vm, SensorModel model, SensorMountPreset preset, int bay, SensorMountOverride? o = null,
                               FencePanelSpec[]? panels = null, FenceControllerEnd end = FenceControllerEnd.Left)
    {
        var list = panels ?? Mixed;
        var key = vm.FenceChain.Keys[0];
        var mounts = vm.FenceChain.Keys.ToDictionary(k => k, k => vm.FenceLayout.MountOf(k) ?? new SensorMountSpec(0, FenceMountSpot.PanelCenter));
        var mount = WiringPresetPlan.Apply(new SensorMountSpec(bay, FenceMountSpot.PanelCenter), preset, list) with { MountOverride = o };
        mounts[key] = mount;
        var layout = WiringFenceLayout.Create(list, mounts, null, controllerEnd: end);
        var placed = layout.MountOf(key)!;
        var bayIndex = SensorMountMigration.BayIndexOf(placed, list.Length, preset.AlongRatio);
        var sensors = vm.FenceSensors().ToDictionary(p => p.Key, p => p.Key == key ? p.Value with { Type = model.DeviceType, ModelShape = model.Shape } : p.Value);
        var world = FenceWorld.FromLayout(vm.FenceChain, sensors, layout, vm.FenceSpacing,
                                          k => k == key ? WiringPresetPlan.PoseOf(layout.MountOf(k), layout.Panels, _ => preset) : null,
                                          k => k == key ? bayIndex : null);
        var pose = SensorMountResolver.Clamp(WiringPresetPlan.PoseOf(placed, layout.Panels, _ => preset)!, FenceSampleBay.FromPanel(layout.Panels[bayIndex]));
        return new Scene(world, key, pose, layout.Geometry.Panels[bayIndex], sensors[key]);
    }

    /// <summary>모델마다 — ★기본 프리셋 + 철조망 · 담 칸의 붙는 점 전부로 만든 프리셋.</summary>
    private static IEnumerable<(SensorModel Model, SensorMountPreset Preset)> Cases()
    {
        var bays = new[] { new FenceSampleBay(3.0, 2.4, HasRazor: true), new FenceSampleBay(3.0, 2.4, IsWall: true) };
        foreach (var model in SensorPresetCatalog.Models)
        {
            foreach (var p in SensorPresetCatalog.MountPresets.Where(p => p.ModelId == model.Id)) yield return (model, p);
            foreach (var bay in bays)
                foreach (var point in SensorSnapPoints.For(bay))
                    yield return (model, SensorMountResolver.SnapTo(new SensorMountPreset { Id = 900, ModelId = model.Id, BracketM = 0.2 }, point, bay));
        }
    }

    private static readonly SensorMountOverride?[] Overrides =
    {
        null, new() { HeightM = 9 }, new() { HeightM = -4 }, new() { AlongRatio = 0.3, HeightM = 2.55 }, new() { AlongRatio = 0.03 }, new() { DepthM = -2.5 },
    };

    #region - 펜스 보기: 붙는 자리 · 닿음 -
    [Fact, Trait("Scenario", "MNT-430")]
    public void should_draw_every_model_touching_its_object_when_presets_overrides_and_bays_vary()
    {
        var vm = WiringFenceHeightTests.Build("SSSSSSS");
        var count = 0;
        foreach (var (model, preset) in Cases())
            for (var bay = 0; bay < Mixed.Length; bay++)
                foreach (var o in Overrides)
                {
                    var s = Stand(vm, model, preset, bay, o);
                    var world = s.World;
                    var mark = world.MountMarkOf(s.Key);
                    Assert.NotNull(mark);
                    Assert.True(SensorMountSurfaces.IsAttached(s.Pose, FenceSampleBay.FromPanel(s.Bay.Spec)), $"{model.Code} {preset.SnapPoint} bay {bay} {o} → {s.Pose}");
                    // 칩 x = 붙는 점 x(옆으로 벌리지 않는다) · 그 칸 안
                    Assert.Equal((s.Bay.StartM + s.Bay.SpanM * s.Pose.AlongRatio) * world.Upm, world.X[s.Key], 6);
                    Assert.Equal(mark!.X, world.X[s.Key], 6);
                    Assert.InRange(mark.Height, mark.Low - 1e-6, mark.High + 1e-6);
                    if (s.Sensor.Kind != FenceKind.Underground)
                    {
                        var (bottom, top) = FenceScene.BodySpan(s.Sensor, mounted: true);
                        var lift = world.LiftOf(s.Key);
                        Assert.InRange(mark.Height, lift + bottom - 0.01, lift + top + 0.01);     // 몸이 붙는 높이에 닿는다
                        if (mark.Anchor != SensorMountAnchor.Ground) Assert.True(lift + bottom >= -0.01, $"땅 뚫음 {model.Code} {mark}");
                    }
                    if (SensorMountResolver.KindOf(mark.Anchor) == SensorAnchorKind.Post)
                        Assert.Contains(FenceScene.PostSensorXs(world), x => Math.Abs(x - mark.X) < 0.5);         // 기둥 솎기가 그 기둥을 남긴다
                    count++;
                }
        Assert.True(count > 2000);
    }

    [Fact, Trait("Scenario", "MNT-431")]
    public void should_hang_rail_sensors_below_the_drawn_rail_and_seat_post_top_sensors_on_the_cap()
    {
        var vm = WiringFenceHeightTests.Build("SSSS");
        var smart2 = SensorPresetCatalog.Models.First(m => m.Id == SensorPresetCatalog.SMART2);
        var plain = new[] { new FencePanelSpec(EnumFenceStyle.ChainLink, null, 2.4, 3), new FencePanelSpec(EnumFenceStyle.ChainLink, null, 2.4, 3) };
        var rail = Stand(vm, smart2, SensorMountResolver.SnapTo(new SensorMountPreset { ModelId = smart2.Id }, SensorSnapPoints.Find(new FenceSampleBay(), "mesh.rail")!, new FenceSampleBay()), 0, panels: plain);
        var m = rail.World.MountMarkOf(rail.Key)!;
        var (bottom, top) = FenceScene.BodySpan(rail.Sensor, true);
        Assert.Equal(FenceSeatMode.Hang, m.Mode);
        Assert.Equal(m.Height, rail.World.LiftOf(rail.Key) + top, 6);                 // 몸 위 = 레일 선(레일 위로 솟아 허공에 얹히지 않는다)

        var postTop = Stand(vm, smart2, SensorPresetCatalog.MountPresets.First(p => p.ModelId == smart2.Id && p.IsDefault), 0, panels: plain);
        var pm = postTop.World.MountMarkOf(postTop.Key)!;
        Assert.Equal(FenceSeatMode.Sit, pm.Mode);
        Assert.Equal(2.4 * postTop.World.Vpm + FenceWorld.POST_CAP_H, pm.Height, 6);   // 그린 기둥 갓 위
        Assert.Equal(pm.Height, postTop.World.LiftOf(postTop.Key) + bottom, 6);
    }

    [Fact, Trait("Scenario", "MNT-432")]
    public void should_not_spread_preset_sensors_sideways_even_when_two_share_a_post()
    {
        var vm = WiringFenceHeightTests.Build("SSSS");
        var keys = vm.FenceChain.Keys;
        var preset = SensorPresetCatalog.MountPresets.First(p => p.ModelId == SensorPresetCatalog.SMART2 && p.IsDefault);
        var mounts = keys.ToDictionary(k => k, k => vm.FenceLayout.MountOf(k)!);
        foreach (var k in keys.Take(2)) mounts[k] = WiringPresetPlan.Apply(new SensorMountSpec(1, FenceMountSpot.PanelCenter), preset, Mixed);
        var layout = WiringFenceLayout.Create(Mixed, mounts, null);
        var world = FenceWorld.FromLayout(vm.FenceChain, vm.FenceSensors(), layout, vm.FenceSpacing,
                                          k => keys.Take(2).Contains(k) ? WiringPresetPlan.PoseOf(layout.MountOf(k), layout.Panels, _ => preset) : null,
                                          k => keys.Take(2).Contains(k) ? SensorMountMigration.BayIndexOf(layout.MountOf(k)!, Mixed.Length, preset.AlongRatio) : null);
        var postX = layout.Geometry.Posts[2].XM * world.Upm;                          // 칸 1 의 기둥 B
        Assert.Equal(postX, world.X[keys[0]], 6);
        Assert.Equal(postX, world.X[keys[1]], 6);
        Assert.True(world.IsMounted(keys[0]) && !world.IsMounted(keys[2]));
    }

    [Fact, Trait("Scenario", "MNT-433")]
    public void should_draw_in_the_same_bay_as_the_view_model_when_a_post_sensor_is_moved_left_of_centre()
    {
        // 기둥 B 프리셋을 칸 가운데 왼쪽(몫 0.3)으로 고친 값 — 자세의 몫으로 칸을 다시 고르면 옆 칸에 그렸다(조사 5)
        var vm = WiringFenceHeightTests.Build("SSSS");
        var multi = SensorPresetCatalog.Models.First(m => m.Id == SensorPresetCatalog.MULTI);
        var preset = SensorPresetCatalog.MountPresets.First(p => p.ModelId == multi.Id && p.IsDefault);
        var s = Stand(vm, multi, preset, 3, new SensorMountOverride { AlongRatio = 0.3, HeightM = 1.2 }, panels: Enumerable.Repeat(new FencePanelSpec(EnumFenceStyle.ChainLink, null, 2.4, 3), 6).ToArray());
        Assert.Equal(3, s.Bay.Index);
        Assert.InRange(s.World.X[s.Key] / s.World.Upm, s.Bay.StartM, s.Bay.StartM + s.Bay.SpanM);
        Assert.Equal(SensorMountAnchor.Mesh, s.World.MountMarkOf(s.Key)!.Anchor);    // 칸 가운데 = 메쉬에 붙는다
    }

    [Fact, Trait("Scenario", "MNT-434")]
    public void should_draw_a_bracket_from_the_object_and_drop_the_multi_sensor_own_pole_when_mounted()
    {
        var vm = WiringFenceHeightTests.Build("SSSS");
        var multi = SensorPresetCatalog.Models.First(m => m.Id == SensorPresetCatalog.MULTI);
        var s = Stand(vm, multi, SensorPresetCatalog.MountPresets.First(p => p.ModelId == multi.Id && p.IsDefault), 1);
        var mark = s.World.MountMarkOf(s.Key);
        foreach (var p in new[] { FenceProjector.Tilt, FenceProjector.Flat })
        {
            var mounted = FenceScene.Sensor(s.Sensor, WiringShape.Ring, p, false, 1, s.World.LiftOf(s.Key), 0, false, mark);
            var bracket = Assert.Single(mounted.Shapes, sh => sh.Ink == FenceInk.MountBracket && sh.Kind == FenceShapeKind.Line);
            Assert.Equal(p.P(0, mark!.Height, 0), bracket.Points[0]);                  // 물체 면(깊이 0)의 붙는 높이에서 시작
            Assert.DoesNotContain(mounted.Shapes, sh => sh.Ink == FenceInk.PostFront);  // 자기 기둥(허공에 뜨던 것) 없음
            var loose = FenceScene.Sensor(s.Sensor, WiringShape.Ring, p, false, 1, s.World.LiftOf(s.Key), 0, false);
            Assert.Contains(loose.Shapes, sh => sh.Ink == FenceInk.PostFront);          // 옛 9자리 그림은 그대로
            Assert.DoesNotContain(loose.Shapes, sh => sh.Ink == FenceInk.MountBracket);
        }
    }
    #endregion

    #region - 결선 저장 · 열 때 보정 · 상태 줄 -
    [Fact, Trait("Scenario", "MNT-440")]
    public void should_rewrite_a_stored_floating_override_on_save_and_show_the_correction_until_then()
    {
        var panels = new[] { new FencePanelSpec(EnumFenceStyle.ChainLink, null, 2.4, 3), new FencePanelSpec(EnumFenceStyle.ChainLink, null, 2.4, 3) };
        var preset = SensorPresetCatalog.MountPresets.First(p => p.ModelId == SensorPresetCatalog.FENCE && p.IsDefault);       // 메쉬 중간
        var stored = WiringPresetPlan.Apply(new SensorMountSpec(0, FenceMountSpot.PanelCenter), preset, panels) with
        {
            MountOverride = new SensorMountOverride { HeightM = 3.4, DepthM = -1.5 },                                          // 옛 문서 — 망 위 허공 · 펜스 밖 허공
        };
        Assert.NotNull(WiringPresetPlan.CorrectionOf(stored, preset, panels));
        var saved = WiringPresetPlan.NormalizeOverride(stored, preset, panels);
        Assert.Equal(2.4, saved.MountOverride!.HeightM!.Value, 3);
        Assert.Null(saved.MountOverride.DepthM);                                                                             // 브래킷 0 → 펜스 면(프리셋과 같아 지운다)
        Assert.Null(WiringPresetPlan.CorrectionOf(saved, preset, panels));
        Assert.Same(saved, WiringPresetPlan.NormalizeOverride(saved, preset, panels));                                         // 붙은 값은 손대지 않는다
    }

    [Fact, Trait("Scenario", "MNT-441")]
    public async Task should_say_why_when_a_fence_view_drop_or_height_step_would_float()
    {
        var vm = WiringFenceHeightTests.Build("SSFF");
        vm.AttachSensorPresets(new InMemorySensorPresetRepository());
        Assert.True(await vm.LoadSensorPresetsAsync());
        var key = vm.FenceChain.Keys.First();
        vm.FenceSelect(key);
        vm.SelectedMountPreset = vm.PresetMountChoices.First(p => p.IsDefault);           // 기둥 B · 꼭대기
        var bay = vm.FencePresetBayOf(key)!.Value;
        var sample = FenceSampleBay.FromPanel(vm.FenceLayout.Panels[bay]);

        Assert.True(vm.FenceMovePresetFree(key, bay, sample.PanelWidthM / 2, sample.FenceHeightM + 0.15));   // 칸 가운데 · 망 위 = 허공
        Assert.Contains("붙였습니다", vm.StatusText);
        Assert.True(SensorMountSurfaces.IsAttached(vm.FencePoseOf(key)!, sample));

        for (var i = 0; i < 40; i++) vm.FenceNudgePresetHeight(new[] { key }, SensorMountResolver.NUDGE_M);
        Assert.True(SensorMountSurfaces.IsAttached(vm.FencePoseOf(key)!, sample));
        Assert.True(vm.FencePoseOf(key)!.HeightM <= sample.TopLimitM + 1e-9);
    }
    #endregion

    #region - 편집기(W2) -
    [Fact, Trait("Scenario", "MNT-445")]
    public async Task should_correct_a_floating_preset_when_the_editor_opens_and_save_the_attached_value()
    {
        var repo = new InMemorySensorPresetRepository();
        var model = SensorPresetCatalog.Models.First(m => m.Id == SensorPresetCatalog.SMART);
        var floating = new SensorMountPreset { Id = 0, ModelId = model.Id, Name = "옛 자리", Anchor = SensorMountAnchor.PostTop, AlongRatio = 0.5, HeightM = 3.5, DepthM = 0.8, FenceHeightM = 2.4 };
        var vm = new MountPresetEditorViewModel(repo, model, floating, new FenceSampleBay(3.0, 2.4));
        Assert.Contains("고쳤습니다", vm.StatusText);
        Assert.True(vm.ChangeCount >= 1);                                                          // "바뀜" 표시
        Assert.True(SensorMountSurfaces.IsAttached(vm.Pose, vm.Bay));
        var saved = await vm.SaveAsNewAsync();
        Assert.NotNull(saved);
        Assert.True(SensorMountSurfaces.IsAttached(SensorMountResolver.Resolve(saved!, null, vm.Bay), vm.Bay));
        Assert.False(SensorMountResolver.Normalize(saved!, vm.Bay).Attachment.Clamped);              // 저장된 값은 허공이 아니다
    }

    [Fact, Trait("Scenario", "MNT-446")]
    public void should_keep_alt_free_drag_and_numeric_fields_on_object_surfaces_in_every_view()
    {
        var repo = new InMemorySensorPresetRepository();
        foreach (var model in SensorPresetCatalog.Models)
            foreach (var bay in new[] { new FenceSampleBay(3.0, 1.8), new FenceSampleBay(3.0, 2.4, HasRazor: true), new FenceSampleBay(2.0, 3.0, IsWall: true) })
            {
                var preset = SensorPresetCatalog.MountPresets.First(p => p.ModelId == model.Id && p.IsDefault);
                var vm = new MountPresetEditorViewModel(repo, model, preset, bay);
                foreach (var view in new[] { MountView.Front, MountView.Top, MountView.Side })
                    foreach (var (u, v) in new[] { (1.5, 9.0), (0.4, 2.0), (2.9, -3.0), (-1.0, 1.0), (1.5, 0.02) })
                    {
                        vm.DragTo(view, u, v, free: true);
                        Assert.True(SensorMountSurfaces.IsAttached(vm.Pose, vm.Bay), $"{model.Code} {view} ({u},{v}) → {vm.Pose}");
                        vm.FinishDrag(commit: true);
                    }
                vm.HeightText = "12";
                vm.DepthText = "-4";
                vm.ApplyFields();
                Assert.True(SensorMountSurfaces.IsAttached(vm.Pose, vm.Bay));
                vm.BracketText = "0";
                vm.ApplyFields();
                Assert.True(SensorMountSurfaces.IsAttached(vm.Pose, vm.Bay));
                var line = new MountBayProjection(MountView.Side, 300, 300, vm.Bay).AttachLine(vm.Pose);
                if (vm.Pose.Anchor != SensorMountAnchor.Ground) Assert.Equal(line.Sensor.Y, line.Object.Y, 6);   // 옆 그림 — 브래킷은 같은 높이에서 물체 면까지
            }
    }
    #endregion

    #region - 펜스 끝 제어기 -
    [Theory, Trait("Scenario", "MNT-450")]
    [InlineData(FenceControllerEnd.Left)]
    [InlineData(FenceControllerEnd.Right)]
    public void should_always_draw_the_controller_at_its_fence_end_even_with_cables_off(FenceControllerEnd end)
    {
        var vm = WiringFenceHeightTests.Build("SSFF");
        var smart2 = SensorPresetCatalog.Models.First(m => m.Id == SensorPresetCatalog.SMART2);
        var s = Stand(vm, smart2, SensorPresetCatalog.MountPresets.First(p => p.ModelId == smart2.Id && p.IsDefault), 0, end: end);
        var world = s.World;
        var length = world.Geometry!.LengthM * world.Upm;
        Assert.True(end == FenceControllerEnd.Left ? world.ControllerX < 0 : world.ControllerX > length);
        foreach (var p in new[] { FenceProjector.Tilt, FenceProjector.Flat })
        {
            var off = FenceScene.StaticLayout(world, p, showRange: false, showCables: false, world.ControllerX, 0);
            var box = off.Where(sh => sh.Ink == FenceInk.EndController).SelectMany(sh => sh.Points).ToList();
            Assert.NotEmpty(box);
            Assert.InRange((box.Min(q => q.X) + box.Max(q => q.X)) / 2, p.P(world.ControllerX, 0, 0).X - 30, p.P(world.ControllerX, 0, 0).X + 30);   // 제어기 x 에(깊이만큼 비스듬)
            Assert.True(end == FenceControllerEnd.Left ? box.Max(q => q.X) < p.P(0, 0, 0).X : box.Min(q => q.X) > p.P(length, 0, 0).X - 20);           // 펜스 그 끝 바깥
            Assert.Contains(off, sh => sh.Ink == FenceInk.ControllerText && sh.Text == "제어기");
            Assert.Contains(off, sh => sh.Ink == FenceInk.Caption && sh.Text?.Contains(end == FenceControllerEnd.Left ? "왼쪽 끝" : "오른쪽 끝") == true);
            var fit = world.FitBounds(p);
            var bounds = FenceScene.EndControllerBounds(p, world.ControllerX);
            Assert.True(fit.Left <= bounds.Left + 6 && fit.Right >= bounds.Right - 6, "맞춤 보기에 제어기가 들어온다");
            var on = FenceScene.StaticLayout(world, p, showRange: false, showCables: true, world.ControllerX, 0);
            Assert.DoesNotContain(on, sh => sh.Ink == FenceInk.EndController);          // 케이블 보기 = 함체 칩(기존)
        }
    }
    #endregion

    #region - 부모 없는 렌더 픽셀 -
    private static readonly HashSet<FenceInk> ObjectInks = new()
    {
        FenceInk.Mesh, FenceInk.Rail, FenceInk.PostFront, FenceInk.PostSide, FenceInk.PostTop, FenceInk.CapFront, FenceInk.CapSide, FenceInk.CapTop,
        FenceInk.Razor, FenceInk.RazorArm, FenceInk.RazorBand, FenceInk.RazorCoil, FenceInk.RazorBarb, FenceInk.RazorStrand,
        FenceInk.BrickFront, FenceInk.BrickSide, FenceInk.BrickTop, FenceInk.BrickMortarFace, FenceInk.BrickTone0, FenceInk.BrickTone1, FenceInk.BrickTone2,
        FenceInk.ConcreteFront, FenceInk.ConcreteSeam, FenceInk.ConcreteSpeckleDark, FenceInk.ConcreteSpeckleLight, FenceInk.WallSide, FenceInk.WallTopFace, FenceInk.WallCap,
        FenceInk.DesignFace, FenceInk.DesignRail, FenceInk.DesignPost, FenceInk.DesignWire, FenceInk.DesignFold, FenceInk.DesignClamp,
    };

    private static readonly HashSet<FenceInk> BodyInks = new()
    {
        FenceInk.OliveFront, FenceInk.OliveSide, FenceInk.OliveTop, FenceInk.GlandFront, FenceInk.GlandSide, FenceInk.GlandTop, FenceInk.Pir, FenceInk.Ball,
    };

    private static bool[,] Mask(IEnumerable<FenceShape> shapes, Rect area)
    {
        var w = (int)Math.Ceiling(area.Width);
        var h = (int)Math.Ceiling(area.Height);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new TranslateTransform(-area.X, -area.Y));
            FenceRenderer.Draw(dc, new Border(), shapes);
            dc.Pop();
        }
        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        var px = new byte[w * h * 4];
        bmp.CopyPixels(px, w * 4, 0);
        var mask = new bool[w, h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                mask[x, y] = px[(y * w + x) * 4 + 3] > 40;
        return mask;
    }

    /// <summary>몸 픽셀(2 px 넓힘)이 물체 픽셀과 겹치나 — 몸 둘레 40 px 안만 렌더한다.</summary>
    private static bool Touches(IReadOnlyList<FenceShape> body, IReadOnlyList<FenceShape> objects)
    {
        var pts = body.SelectMany(sh => sh.Points).ToList();
        var area = new Rect(new Point(pts.Min(q => q.X) - 40, pts.Min(q => q.Y) - 40), new Point(pts.Max(q => q.X) + 40, pts.Max(q => q.Y) + 40));
        var a = Mask(body, area);
        var b = Mask(objects, area);
        if (!a.Cast<bool>().Any()) return false;                                  // 몸이 그려지지 않으면 닿음 아님
        for (var yy = 0; yy < a.GetLength(1); yy++)
            for (var xx = 0; xx < a.GetLength(0); xx++)
            {
                if (!a[xx, yy]) continue;
                for (var dy = -2; dy <= 2; dy++)
                    for (var dx = -2; dx <= 2; dx++)
                    {
                        var nx = xx + dx; var ny = yy + dy;
                        if (nx >= 0 && ny >= 0 && nx < a.GetLength(0) && ny < a.GetLength(1) && b[nx, ny]) return true;
                    }
            }
        return false;
    }

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new System.Threading.Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { failure = ex; }
            finally { StaCleanup.ShutdownDispatcher(); }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new AggregateException(failure);
        return result;
    }

    [Fact, Trait("Scenario", "MNT-455")]
    public void should_render_each_sensor_body_touching_object_pixels_in_tilt_and_flat_views()
    {
        var picks = new (int Model, string Point, int Bay)[]
        {
            (SensorPresetCatalog.SMART2, "post_b.top", 1), (SensorPresetCatalog.SMART, "post_a.mid", 6), (SensorPresetCatalog.SMART2, "mesh.rail", 6),
            (SensorPresetCatalog.FENCE, "mesh.low", 1), (SensorPresetCatalog.FENCE, "razor.mid", 0), (SensorPresetCatalog.MULTI, "post_b.mid", 6),
            (SensorPresetCatalog.MULTI, "razor.loop_b", 5), (SensorPresetCatalog.PIR, "wall.face", 2), (SensorPresetCatalog.PIR, "wall.top", 3),
            (SensorPresetCatalog.OPTICAL, "razor.top.50", 7), (SensorPresetCatalog.SMART, "post_b.low", 4),
        };
        var failures = OnSta(() =>
        {
            var vm = WiringFenceHeightTests.Build("SSSSSSS");
            var bad = new List<string>();
            foreach (var (modelId, point, bay) in picks)
            {
                var model = SensorPresetCatalog.Models.First(m => m.Id == modelId);
                var sample = FenceSampleBay.FromPanel(Mixed[bay]);
                var snap = SensorSnapPoints.Find(sample, point) ?? SensorSnapPoints.Find(new FenceSampleBay(3, 2.4, HasRazor: true, IsWall: point.StartsWith("wall")), point)!;
                var preset = SensorMountResolver.SnapTo(new SensorMountPreset { Id = 900, ModelId = modelId }, snap, sample);
                var s = Stand(vm, model, preset, bay);
                foreach (var p in new[] { FenceProjector.Tilt, FenceProjector.Flat })
                {
                    var world = s.World;
                    var x = world.X[s.Key];
                    var chip = FenceScene.Sensor(s.Sensor, WiringShape.Ring, p, false, 1, world.LiftOf(s.Key), world.CoilOf(s.Key), false, world.MountMarkOf(s.Key));
                    var body = chip.Shapes.Where(sh => BodyInks.Contains(sh.Ink))
                                          .Select(sh => sh with { Points = sh.Points.Select(q => new Point(q.X + x, q.Y)).ToArray() }).ToList();
                    var objects = FenceScene.StaticLayout(world, p, false, false, world.ControllerX, 0, zoom: 2).Where(sh => ObjectInks.Contains(sh.Ink)).ToList();
                    if (!Touches(body, objects)) bad.Add($"{model.Code} {point} 칸 {bay} K={p.K}");
                    // 대조군 — 같은 몸을 허공(물체 위 400)으로 옮기면 닿지 않아야 한다(시험이 헛돌지 않는다)
                    var lifted = body.Select(sh => sh with { Points = sh.Points.Select(q => new Point(q.X, q.Y - 400)).ToArray() }).ToList();
                    if (Touches(lifted, objects)) bad.Add($"대조군 실패 {model.Code} {point} 칸 {bay} K={p.K}");
                }
            }
            return bad;
        });
        Assert.True(failures.Count == 0, "물체 픽셀과 닿지 않음: " + string.Join(" / ", failures));
    }
    #endregion
}
