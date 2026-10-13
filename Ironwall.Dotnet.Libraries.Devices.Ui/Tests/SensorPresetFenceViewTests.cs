using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.SensorPresets;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Help;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 센서 설치 위치 프리셋 후속(sensor-mount-preset §10 남은 일) — 결선 펜스 보기 탐지 범위 · 간격 알림 · 붙는 점 끌기 · 입구 블록.
/// 창을 띄우지 않는다(뷰모델 · 순수 함수 · 그림 목록만 · 가짜 저장소 · 지도 DB 접속 없음).
/// 실행: <c>dotnet test Ironwall.Dotnet.Libraries.Devices.Ui/Ironwall.Dotnet.Libraries.Devices.Ui.csproj --filter "Scenario~MNT-2"</c>
/// </summary>
public class SensorPresetFenceViewTests
{
    private static async Task<WiringViewModel> WiringWithPresets(string types = "SSFF", InMemorySensorPresetRepository? repo = null, bool razor = false)
    {
        var vm = WiringFenceHeightTests.Build(types);
        if (razor) WiringFenceHeightTests.Razor(vm);
        vm.AttachSensorPresets(repo ?? new InMemorySensorPresetRepository());
        Assert.True(await vm.LoadSensorPresetsAsync());
        return vm;
    }

    /// <summary>첫 센서(스마트센서2)를 ★기본 위치 프리셋(기둥 B · 꼭대기)에 세운다.</summary>
    private static int StandFirstOnPreset(WiringViewModel vm)
    {
        var key = vm.FenceChain.Keys.First();
        vm.FenceSelect(key);
        vm.SelectedMountPreset = vm.PresetMountChoices.First(p => p.IsDefault);
        Assert.True(vm.IsPresetMounted(key));
        return key;
    }

    #region - 탐지 범위 · 간격 알림(FR-14 · W3) -
    [Fact, Trait("Scenario", "MNT-200")]
    public async Task should_draw_detection_range_from_attribute_preset_distance_when_presets_are_loaded()
    {
        var vm = await WiringWithPresets();
        var sensors = vm.FenceSensors();
        var smart = vm.FenceChain.Keys.First();
        var fence = vm.FenceChain.Keys.Last();
        Assert.Equal(20, sensors[smart].RangeM);                                                       // 스마트센서2 ★기본 속성 — 탐지 거리 20 m(종류 기본은 0)
        Assert.Equal(3, sensors[fence].RangeM);                                                        // 펜스센서 — 탐지 반경 3 m
        Assert.True(vm.HasRangeSensors);

        var world = FenceWorld.FromLayout(vm.FenceChain, sensors, vm.FenceLayout, vm.FenceSpacing, vm.FencePoseOf);
        Assert.Equal(20, world.RangeOfKey(smart));
        var shapes = FenceScene.StaticLayout(world, FenceProjector.Tilt, showRange: true, showCables: false, world.ControllerX, 0);
        var ranges = shapes.Where(s => s.Ink == FenceInk.Range).ToList();
        Assert.Equal(4, ranges.Count);
        Assert.Contains(ranges, r => Math.Abs(r.RadiusX - 20 * world.Upm) < 1e-6);
        Assert.Contains(ranges, r => Math.Abs(r.RadiusX - 3 * world.Upm) < 1e-6);
    }

    [Fact, Trait("Scenario", "MNT-201")]
    public async Task should_follow_the_chosen_attribute_preset_when_sensor_switches_preset()
    {
        var repo = new InMemorySensorPresetRepository();
        await repo.SaveAttributePresetAsync(new SensorAttributePreset
        {
            ModelId = SensorPresetCatalog.SMART2, Name = "먼 탐지",
            Values = new[] { new SensorAttributeValue(SensorAttributePreset.KEY_RANGE, "탐지 거리", "35", "m", SensorAttributeUse.Render) },
        });
        var vm = await WiringWithPresets(repo: repo);
        var key = vm.FenceChain.Keys.First();
        vm.FenceSelect(key);
        vm.SelectedAttributePreset = vm.PresetAttributeChoices.First(p => p.Name == "먼 탐지");
        Assert.Equal(35, vm.PresetRangeOf(key));
        Assert.Equal(35, vm.FenceSensors()[key].RangeM);
    }

    [Fact, Trait("Scenario", "MNT-202")]
    public void should_keep_kind_ranges_when_no_presets_are_attached()
    {
        var vm = WiringFenceHeightTests.Build("SSFF");
        var sensors = vm.FenceSensors();
        Assert.All(sensors.Values, s => Assert.Null(s.RangeM));
        Assert.All(sensors.Values, s => Assert.Equal(FenceWorld.RangeOf(s.Kind), FenceWorld.RangeOf(s)));
        Assert.False(vm.HasRangeSensors);                                                              // 스마트 · 펜스 — 종류 기본 반경 없음(전과 같다)
        Assert.Empty(vm.FenceSpacingAlerts());
    }

    [Fact, Trait("Scenario", "MNT-203")]
    public async Task should_mark_neighbours_beyond_recommended_spacing_with_a_warning_triangle_even_with_range_off()
    {
        var repo = new InMemorySensorPresetRepository();
        await repo.SaveAttributePresetAsync(new SensorAttributePreset
        {
            ModelId = SensorPresetCatalog.SMART2, Name = "촘촘", IsDefault = true,
            Values = new[] { new SensorAttributeValue(SensorAttributePreset.KEY_SPACING, "권장 설치 간격", "0.5", "m", SensorAttributeUse.Alert) },
        });
        var vm = await WiringWithPresets(repo: repo);
        var alerts = vm.FenceSpacingAlerts();
        var keys = vm.FenceChain.Keys;
        var alert = Assert.Single(alerts, a => a.Left == keys[0] && a.Right == keys[1]);                 // 스마트센서2 두 대 사이(권장 0.5 m)
        Assert.Equal(0.5, alert.Limit);
        Assert.True(alert.Metres > alert.Limit);
        Assert.StartsWith("간격 ", alert.Text);
        Assert.All(alerts, a => Assert.True(a.Metres > a.Limit));                                       // 펜스센서(권장 2.5 m)도 넘으면 같이 뜬다

        var world = FenceWorld.FromLayout(vm.FenceChain, vm.FenceSensors(), vm.FenceLayout, vm.FenceSpacing, vm.FencePoseOf);
        world.UseSpacingAlerts(alerts.Append(new FenceSpacingAlert(9999, 9998, 3, 1)));                 // 장면에 없는 쌍은 버린다
        Assert.Equal(alerts.Count, world.SpacingAlerts.Count);
        var shapes = FenceScene.StaticLayout(world, FenceProjector.Tilt, showRange: false, showCables: false, world.ControllerX, 0);
        Assert.Equal(alerts.Count, shapes.Count(s => s.Ink == FenceInk.SpacingAlertMark && s.Kind == FenceShapeKind.Polygon && s.Points.Length == 3));   // 삼각형(빈틈 = 마름모)
        Assert.Contains(shapes, s => s.Ink == FenceInk.SpacingAlertText && s.Text == alert.Text);
        Assert.True(FenceScene.IsPrimaryText(FenceInk.SpacingAlertText));                               // 작은 배율에서도 읽힌다
    }
    #endregion

    #region - 펜스 보기 끌기 · 붙는 점(W4) -
    [Fact, Trait("Scenario", "MNT-210")]
    public async Task should_list_bay_snap_points_and_dim_the_kinds_the_model_cannot_use()
    {
        var vm = await WiringWithPresets();
        var key = StandFirstOnPreset(vm);
        var bay = vm.FencePresetBayOf(key)!.Value;
        var points = vm.FencePresetSnapPoints(key, bay);
        Assert.Equal(SensorSnapPoints.For(FenceSampleBay.FromPanel(vm.FenceLayout.Panels[bay])).Count, points.Count);
        Assert.DoesNotContain(points, p => p.Point.Kind == SensorAnchorKind.Razor);                    // 철조망 없는 칸
        Assert.All(points.Where(p => p.Point.Kind is SensorAnchorKind.Post or SensorAnchorKind.Rail), p => Assert.True(p.Allowed));
        Assert.All(points.Where(p => p.Point.Kind is SensorAnchorKind.Mesh or SensorAnchorKind.Ground), p => Assert.False(p.Allowed));   // 스마트2 = 기둥 · 레일 · 철조망
        var geometry = vm.FenceLayout.Geometry.Panels[bay];
        Assert.Equal(geometry.StartM + geometry.SpanM, points.First(p => p.Point.Name == "post_b.mid").XM, 6);

        Assert.Empty(vm.FencePresetSnapPoints(vm.FenceChain.Keys.Last(), bay));                        // 프리셋으로 서지 않는 센서
        Assert.Empty(vm.FencePresetSnapPoints(key, 99));
    }

    [Fact, Trait("Scenario", "MNT-211")]
    public async Task should_show_coil_points_on_razor_bays()
    {
        var vm = await WiringWithPresets(razor: true);
        var key = StandFirstOnPreset(vm);
        var points = vm.FencePresetSnapPoints(key, vm.FencePresetBayOf(key)!.Value);
        Assert.Equal(6, points.Count(p => p.Point.Kind == SensorAnchorKind.Razor));                    // 위 25 · 50 · 75 % · 가운데 · 끝 고리 둘
        Assert.All(points.Where(p => p.Point.Kind == SensorAnchorKind.Razor), p => Assert.True(p.Allowed));
    }

    [Fact, Trait("Scenario", "MNT-212")]
    public async Task should_snap_to_post_b_middle_on_drop_and_keep_it_as_override_until_reset()
    {
        var vm = await WiringWithPresets();
        var key = StandFirstOnPreset(vm);
        var bay = vm.FencePresetBayOf(key)!.Value;
        var h = vm.FenceLayout.Panels[bay].Normalized().HeightM;

        Assert.True(vm.FenceSnapPreset(key, bay, "post_b.mid"));
        Assert.Equal(h * FenceSampleBay.MID_RATIO, vm.FencePoseOf(key)!.HeightM, 2);
        Assert.True(vm.HasMountOverride);                                                               // 따로 고친 값(주황)
        Assert.Contains("기둥 B · 중간", vm.StatusText);
        Assert.Contains("[프리셋으로]", vm.StatusText);

        Assert.True(vm.ResetMountToPreset());                                                           // [프리셋으로] 가 지운다
        Assert.False(vm.HasMountOverride);
        Assert.Equal(FenceSampleBay.FromPanel(vm.FenceLayout.Panels[bay]).PostTopM, vm.FencePoseOf(key)!.HeightM, 2);

        Assert.True(vm.FenceSnapPreset(key, bay, "post_b.mid"));
        vm.Undo();                                                                                     // 되돌리기 한 걸음
        Assert.False(vm.HasMountOverride);
    }

    [Fact, Trait("Scenario", "MNT-213")]
    public async Task should_refuse_points_the_model_cannot_use()
    {
        var vm = await WiringWithPresets();
        var key = StandFirstOnPreset(vm);
        var bay = vm.FencePresetBayOf(key)!.Value;
        var before = vm.FenceLayout.MountOf(key);
        Assert.False(vm.FenceSnapPreset(key, bay, "mesh.mid"));
        Assert.Contains("붙지 않습니다", vm.StatusText);
        Assert.Equal(before, vm.FenceLayout.MountOf(key));
        Assert.False(vm.FenceSnapPreset(key, bay, "no.such.point"));
    }

    [Fact, Trait("Scenario", "MNT-214")]
    public async Task should_move_sensor_into_another_bay_and_stand_it_there_in_the_fence_world()
    {
        var vm = await WiringWithPresets();
        var key = StandFirstOnPreset(vm);
        var count = vm.FenceLayout.Panels.Count;
        Assert.True(count > 1);
        var from = vm.FencePresetBayOf(key)!.Value;
        var to = from + 1 < count ? from + 1 : from - 1;
        Assert.True(vm.FenceSnapPreset(key, to, "mesh.rail"));
        Assert.Equal(to, vm.FencePresetBayOf(key));
        var pose = vm.FencePoseOf(key)!;
        Assert.Equal(0.5, pose.AlongRatio, 3);
        var bay = vm.FenceLayout.Geometry.Panels[to];
        var world = FenceWorld.FromLayout(vm.FenceChain, vm.FenceSensors(), vm.FenceLayout, vm.FenceSpacing, vm.FencePoseOf);
        Assert.InRange(world.X[key] / world.Upm, bay.StartM, bay.StartM + bay.SpanM);                  // 그 칸 안에 선다
    }

    [Fact, Trait("Scenario", "MNT-215")]
    public async Task should_place_freely_to_the_centimetre_when_alt_is_held()
    {
        var vm = await WiringWithPresets();
        var key = StandFirstOnPreset(vm);
        var bay = vm.FencePresetBayOf(key)!.Value;
        Assert.True(vm.FenceMovePresetFree(key, bay, 1.234, 1.567));
        var pose = vm.FencePoseOf(key)!;
        Assert.Equal(1.23, pose.AlongM, 2);
        Assert.Equal(1.57, pose.HeightM, 2);
        Assert.Contains("자유 위치", vm.StatusText);

        Assert.True(vm.FenceMovePresetFree(key, bay, 1.0, 50));                                       // 기둥 위로 뜨지 않는다
        Assert.True(vm.FencePoseOf(key)!.HeightM <= FenceSampleBay.FromPanel(vm.FenceLayout.Panels[bay]).TopLimitM + 1e-9);
    }

    [Fact, Trait("Scenario", "MNT-216")]
    public async Task should_step_through_allowed_snap_points_with_keyboard_and_cross_into_the_next_bay()
    {
        var vm = await WiringWithPresets();
        var key = StandFirstOnPreset(vm);
        var start = vm.FencePresetBayOf(key)!.Value;
        var seen = new List<(int Bay, double Along, double Height)>();
        for (var i = 0; i < 30 && vm.FenceStepPresetSnap(key, 1); i++)
        {
            var pose = vm.FencePoseOf(key)!;
            seen.Add((vm.FencePresetBayOf(key)!.Value, pose.AlongRatio, pose.HeightM));
        }
        Assert.NotEmpty(seen);
        Assert.Equal(seen.Count, seen.Distinct().Count());                                              // 같은 점을 두 번 밟지 않는다
        Assert.Contains(seen, s => s.Bay != start || seen.Count < 2);
        Assert.All(seen, s => Assert.DoesNotContain(vm.FencePresetSnapPoints(key, s.Bay), p => !p.Allowed
                                                    && Math.Abs(p.Point.AlongRatio - s.Along) < 1e-4 && Math.Abs(p.Point.HeightM - s.Height) < 0.005));
        Assert.False(vm.FenceStepPresetSnap(key, 1));                                                  // 끝
        Assert.StartsWith("더 옮길 붙는 점이 없습니다", vm.StatusText);
        Assert.True(vm.FenceStepPresetSnap(key, -1));                                                  // 뒤로
    }

    [Fact, Trait("Scenario", "MNT-217")]
    public async Task should_nudge_height_by_ten_or_one_centimetre_as_override()
    {
        var vm = await WiringWithPresets();
        var key = StandFirstOnPreset(vm);
        var h0 = vm.FencePoseOf(key)!.HeightM;
        Assert.True(vm.FenceNudgePresetHeight(new[] { key }, -SensorMountResolver.NUDGE_M));
        Assert.Equal(h0 - 0.10, vm.FencePoseOf(key)!.HeightM, 3);
        Assert.True(vm.FenceNudgePresetHeight(new[] { key }, -SensorMountResolver.FINE_NUDGE_M));
        Assert.Equal(h0 - 0.11, vm.FencePoseOf(key)!.HeightM, 3);
        Assert.True(vm.HasMountOverride);
        Assert.False(vm.FenceNudgePresetHeight(new[] { vm.FenceChain.Keys.Last() }, 0.1));             // 프리셋 센서가 아니면 옛 길로
    }
    #endregion

    #region - 순수 계산 · 그림 -
    private static FencePresetSnapPoint P(string name, bool allowed, double x, double h)
        => new(0, new SensorSnapPoint(name, SensorAnchorKind.Post, SensorMountAnchor.PostMid, 0, h, 0, name), allowed, x);

    [Fact, Trait("Scenario", "MNT-220")]
    public void should_snap_to_nearest_allowed_point_and_cycle_with_tab()
    {
        var a = P("a", true, 0, 1);
        var b = P("b", false, 1, 1);
        var c = P("c", true, 3, 1);
        var points = new List<(FencePresetSnapPoint, Point)> { (a, new Point(0, 0)), (b, new Point(10, 0)), (c, new Point(30, 0)) };
        Assert.Same(a, FencePresetDrag.Nearest(points, new Point(11, 0)));                             // 흐린 점(b)은 건너뛴다
        Assert.Same(c, FencePresetDrag.Nearest(points, new Point(25, 0)));
        Assert.Same(c, FencePresetDrag.Cycle(points, a, backwards: false));
        Assert.Same(a, FencePresetDrag.Cycle(points, c, backwards: false));                            // 끝에서 처음으로
        Assert.Same(c, FencePresetDrag.Cycle(points, null, backwards: true));
        Assert.Null(FencePresetDrag.Nearest(new List<(FencePresetSnapPoint, Point)> { (b, new Point()) }, new Point()));

        var dots = FencePresetDrag.Dots(points, c);
        Assert.Equal(new[] { FenceSnapState.Mount, FenceSnapState.MountDim, FenceSnapState.MountHot }, dots.Select(d => d.State));
    }

    [Fact, Trait("Scenario", "MNT-221")]
    public void should_turn_pointer_back_into_bay_metres_for_free_placement()
    {
        foreach (var p in new[] { FenceProjector.Tilt, FenceProjector.Flat })
        {
            const double upm = 68.0 / 6, vpm = FenceProjector.H / FenceWorld.REFERENCE_HEIGHT_M, start = 6;
            var at = p.P((start + 1.25) * upm, 1.8 * vpm, 0);
            var (along, height) = FencePresetDrag.FreeAt(at, p, upm, vpm, start);
            Assert.Equal(1.25, along, 2);
            Assert.Equal(1.8, height, 2);
            var point = new FencePresetSnapPoint(2, new SensorSnapPoint("x", SensorAnchorKind.Post, SensorMountAnchor.PostMid, 0.5, 1.8, 0, "x"), true, start + 1.25);
            Assert.Equal(at, FencePresetDrag.PointAt(point, p, upm, vpm));
        }
    }

    [Fact, Trait("Scenario", "MNT-222")]
    public void should_draw_target_point_as_red_dot_with_ring_and_label_others_as_plain_or_faint_dots()
    {
        var dots = new[]
        {
            new FenceSnapDot(new Point(0, 0), FenceSnapState.Mount),
            new FenceSnapDot(new Point(10, 0), FenceSnapState.MountDim),
            new FenceSnapDot(new Point(20, 0), FenceSnapState.MountHot),
        };
        var vm = WiringFenceHeightTests.Build("SS");
        var world = FenceWorld.FromLayout(vm.FenceChain, vm.FenceSensors(), vm.FenceLayout);
        var shapes = FenceScene.Overlay(world, FenceProjector.Tilt, new HashSet<int>(), null, null, null, 1, dots, "기둥 B · 중간 · 1.8 m 에 붙음");
        Assert.Single(shapes, s => s.Ink == FenceInk.MountDot);
        Assert.Single(shapes, s => s.Ink == FenceInk.MountDotDim);
        Assert.Single(shapes, s => s.Ink == FenceInk.MountHot);
        Assert.Single(shapes, s => s.Ink == FenceInk.SnapRing);                                          // 색만이 아니라 고리로도
        var pill = Assert.Single(shapes, s => s.Ink == FenceInk.PillInsert);
        Assert.Equal("기둥 B · 중간 · 1.8 m 에 붙음", pill.Text);
        Assert.Equal(20, pill.Points[0].X);                                                              // 붙을 점 위
    }

    [Fact, Trait("Scenario", "MNT-223")]
    public void should_choose_panel_number_so_the_sensor_is_drawn_in_the_target_bay()
    {
        var panels = new[] { FencePanelSpec.Default(EnumFenceStyle.ChainLink, 3), FencePanelSpec.Default(EnumFenceStyle.ChainLink, 3), FencePanelSpec.Default(EnumFenceStyle.ChainLink, 3) };
        var postB = SensorPresetCatalog.MountPresets.First(p => p.SnapPoint == "post_b.mid");
        var mesh = SensorPresetCatalog.MountPresets.First(p => p.SnapPoint == "mesh.mid");
        foreach (var (spot, preset) in new[] { (FenceMountSpot.PostTop, postB), (FenceMountSpot.PanelCenter, mesh) })
            for (var bay = 0; bay < panels.Length; bay++)
            {
                var mount = WiringPresetPlan.Apply(new SensorMountSpec(0, spot), preset, panels);
                var moved = WiringPresetPlan.MoveTo(mount, preset, panels, bay, 0.5, 1.2);
                Assert.Equal(bay, SensorMountMigration.BayIndexOf(moved, panels.Length, preset.AlongRatio));
                Assert.Equal(1.2, WiringPresetPlan.PoseOf(moved, panels, _ => preset)!.HeightM, 3);
                Assert.Equal(preset.Id, moved.MountPresetId);                                           // 프리셋은 그대로
            }
    }

    [Fact, Trait("Scenario", "MNT-224")]
    public void should_offer_only_ground_points_to_buried_presets_and_keep_ground_out_of_others()
    {
        var bay = new FenceSampleBay(3.0, 2.4, HasRazor: true);
        var optical = SensorPresetCatalog.Models.First(m => m.Id == SensorPresetCatalog.OPTICAL);
        var meshPreset = SensorPresetCatalog.MountPresets.First(p => p.ModelId == SensorPresetCatalog.OPTICAL && p.SnapPoint == "mesh.mid");
        var groundPreset = SensorPresetCatalog.MountPresets.First(p => p.ModelId == SensorPresetCatalog.UNDERGROUND && p.IsDefault);
        Assert.All(WiringPresetPlan.SnapChoices(bay, optical, meshPreset).Where(c => c.Allowed), c => Assert.NotEqual(SensorAnchorKind.Ground, c.Point.Kind));
        Assert.All(WiringPresetPlan.SnapChoices(bay, optical, groundPreset).Where(c => c.Allowed), c => Assert.Equal(SensorAnchorKind.Ground, c.Point.Kind));
        Assert.Contains(WiringPresetPlan.SnapChoices(bay, optical, meshPreset), c => c.Allowed && c.Point.Kind == SensorAnchorKind.Razor);   // 광케이블은 철조망에도
    }
    #endregion

    #region - 입구 · 저장소(W1) -
    private sealed class FakeLauncher : ISensorPresetLauncher
    {
        public int Opened;
        public bool IsSensorModelsAvailable { get; set; } = true;
        public Task<bool> OpenSensorModelsAsync() { Opened++; return Task.FromResult(true); }
    }

    [Fact, Trait("Scenario", "MNT-230")]
    public async Task should_show_store_summary_and_open_the_models_window_from_the_settings_block()
    {
        var launcher = new FakeLauncher();
        var block = new SensorModelsSettingsBlockViewModel(new Lazy<ISensorPresetLauncher>(() => launcher),
                                                           new Lazy<ISensorPresetRepository>(() => SensorPresetRepositoryFactory.Create(SensorPresetSource.GisDb, () => new InMemorySensorPresetRepository())));
        await block.LoadAsync();
        Assert.Equal("센서 모델", block.Title);
        Assert.Equal($"모델 7 · 위치 프리셋 {SensorPresetCatalog.MountPresets.Count} · 속성 프리셋 {SensorPresetCatalog.AttributePresets.Count}", block.SummaryText);
        Assert.True(block.CanEdit);
        Assert.True(block.CanOpen);
        await block.OpenAsync();
        await block.HandleAsync(new OpenSensorModelsMessage(), CancellationToken.None);                // 진입 메시지 — 호스트 레일이 보낸다
        Assert.Equal(2, launcher.Opened);
    }

    [Fact, Trait("Scenario", "MNT-231")]
    public async Task should_mark_gop_api_store_read_only_and_lock_editing()
    {
        var gop = SensorPresetRepositoryFactory.Create(SensorPresetSource.GopApi, () => throw new InvalidOperationException("GIS DB 를 만들면 안 된다"));
        var block = new SensorModelsSettingsBlockViewModel(new Lazy<ISensorPresetLauncher>(() => new FakeLauncher()), new Lazy<ISensorPresetRepository>(() => gop));
        await block.LoadAsync();
        Assert.Equal("저장소: GOP API(읽기 전용)", block.SourceText);
        Assert.False(block.CanEdit);
        Assert.StartsWith("모델 7", block.SummaryText);

        var models = new SensorModelsViewModel(gop);                                                    // 센서 모델 창(W1)도 같은 표지 · 잠금
        Assert.Equal("저장소: GOP API(읽기 전용)", models.SourceText);
        Assert.False(models.CanEdit);

        var vm = WiringFenceHeightTests.Build("SSFF");                                                   // 결선 절은 읽기만 — 새 프리셋 저장은 거부
        vm.AttachSensorPresets(gop);
        Assert.True(await vm.LoadSensorPresetsAsync());
        StandFirstOnPreset(vm);
        Assert.Null(await vm.SaveMountAsNewPresetAsync("새 자리"));
    }

    [Fact, Trait("Scenario", "MNT-232")]
    public async Task should_say_why_when_the_block_has_no_launcher_or_store()
    {
        var block = new SensorModelsSettingsBlockViewModel();
        await block.LoadAsync();
        Assert.False(block.CanOpen);
        Assert.False(block.CanEdit);
        Assert.StartsWith("저장소: 없음", block.SourceText);
        await block.OpenAsync();
        Assert.StartsWith("센서 모델 창을 열 수 없습니다", block.StatusText);

        var hidden = new SensorModelsSettingsBlockViewModel(new Lazy<ISensorPresetLauncher>(() => new FakeLauncher { IsSensorModelsAvailable = false }));
        Assert.False(hidden.CanOpen);
    }

    [Fact, Trait("Scenario", "MNT-233")]
    public void should_open_models_window_through_the_same_launcher_as_wiring_and_register_help()
    {
        Assert.True(typeof(ISensorPresetLauncher).IsAssignableFrom(typeof(WiringLauncher)));             // 장비 창 [⋯] · 설정 블록 · 결선 [프리셋 관리] 가 같은 창
        Assert.True(typeof(IHandle<OpenSensorModelsMessage>).IsAssignableFrom(typeof(SensorModelsSettingsBlockViewModel)));
        var keys = DevicesHelp.Entries.Select(e => e.Key).ToHashSet();
        Assert.Contains("Devices.SensorPresets.Settings", keys);
        Assert.Contains("Devices.Wiring.MountPreset", keys);
    }
    #endregion
}
