using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Monitoring.Models.Tests;

/// <summary>
/// 센서 설치 위치 · 속성 프리셋(sensor-mount-preset PRD) — 순수 계산 시험(붙는 점 · 자리 계산 · 이관 · 쓰임 · 내보내기 · 저장소 규칙).
/// </summary>
public class SensorPresetModelTests
{
    private static readonly FenceSampleBay Plain = new(3.0, 2.4);
    private static readonly FenceSampleBay Razor = new(3.0, 2.4, HasRazor: true);

    private static SensorModel ModelOf(int id) => SensorPresetCatalog.Models.First(m => m.Id == id);

    #region - 붙는 점(스케치 1 · 2) -
    [Fact, Trait("Scenario", "MNT-001")]
    public void should_place_four_points_per_post_and_three_on_mesh_when_bay_has_no_razor()
    {
        var points = SensorSnapPoints.For(Plain);
        Assert.Equal(4, points.Count(p => p.Name.StartsWith("post_a.")));
        Assert.Equal(4, points.Count(p => p.Name.StartsWith("post_b.")));
        Assert.Equal(3, points.Count(p => p.Name.StartsWith("mesh.")));
        Assert.DoesNotContain(points, p => p.Kind == SensorAnchorKind.Razor);
        // 높이 차례: 꼭대기 > 레일 > 중간 > 아래
        var a = points.Where(p => p.Name.StartsWith("post_a.")).Select(p => p.HeightM).ToList();
        Assert.True(a[0] > a[1] && a[1] > a[2] && a[2] > a[3]);
        Assert.Equal(2.4 + FenceSampleBay.POST_CAP_M, a[0], 3);
        Assert.Equal(2.4, points.Single(p => p.Name == "mesh.rail").HeightM, 3);
    }

    [Fact, Trait("Scenario", "MNT-002")]
    public void should_add_coil_top_points_and_end_loops_when_bay_has_razor()
    {
        var points = SensorSnapPoints.For(Razor);
        var razor = points.Where(p => p.Kind == SensorAnchorKind.Razor).ToList();
        Assert.Contains(razor, p => p.Name == "razor.top.50" && Math.Abs(p.AlongRatio - 0.5) < 1e-9);
        Assert.Contains(razor, p => p.Name == "razor.mid");
        var loops = razor.Where(p => p.Anchor == SensorMountAnchor.RazorLoop).ToList();
        Assert.Equal(2, loops.Count);
        Assert.True(loops.All(p => p.AlongRatio < 0.1 || p.AlongRatio > 0.9));            // 기둥 쪽 끝 고리
        // 코일 높이 · 지름은 펜스 구성(2.5D 그림과 같은 식)에서 읽는다
        Assert.Equal(FenceLayoutMath.RazorCoilCenterM(2.4), points.Single(p => p.Name == "razor.mid").HeightM, 6);
        Assert.Equal(FenceLayoutMath.RazorCoilCenterM(2.4) + 2.4 * FenceLayoutMath.RAZOR_COIL_DIAMETER_RATIO / 2,
                     points.Single(p => p.Name == "razor.top.25").HeightM, 6);
        Assert.True(razor.All(p => p.HeightM > 2.4));                                         // 상단 레일 위
    }

    [Fact, Trait("Scenario", "MNT-003")]
    public void should_offer_only_allowed_kinds_when_model_restricts_points()
    {
        Assert.True(SensorSnapPoints.Allowed(Razor, ModelOf(SensorPresetCatalog.FENCE)).All(p => p.Kind is SensorAnchorKind.Mesh or SensorAnchorKind.Rail or SensorAnchorKind.Razor));
        Assert.True(SensorSnapPoints.Allowed(Razor, ModelOf(SensorPresetCatalog.UNDERGROUND)).All(p => p.Kind == SensorAnchorKind.Ground));
        var multi = SensorSnapPoints.Allowed(Razor, ModelOf(SensorPresetCatalog.MULTI));
        Assert.Contains(multi, p => p.Kind == SensorAnchorKind.Razor);
        Assert.DoesNotContain(multi, p => p.Kind == SensorAnchorKind.Mesh);
    }

    [Fact, Trait("Scenario", "MNT-004")]
    public void should_use_wall_points_instead_of_posts_when_bay_is_wall()
    {
        var points = SensorSnapPoints.For(new FenceSampleBay(3, 2.2, HasRazor: true, IsWall: true));
        Assert.DoesNotContain(points, p => p.Kind is SensorAnchorKind.Post or SensorAnchorKind.Mesh or SensorAnchorKind.Razor);
        Assert.Contains(points, p => p.Name == "wall.top");
    }

    [Fact, Trait("Scenario", "MNT-005")]
    public void should_snap_to_nearest_allowed_point_when_dragging_near_post_b_middle()
    {
        var multi = ModelOf(SensorPresetCatalog.MULTI);
        var hit = SensorSnapPoints.Nearest(Plain, multi, alongM: 2.9, heightM: 1.75);
        Assert.Equal("post_b.mid", hit!.Name);
        Assert.Equal("기둥 B · 중간 · 1.8 m 에 붙음", hit.AttachText);
        // 펜스센서는 기둥 점에 붙지 않는다 — 가장 가까운 메쉬 점
        Assert.StartsWith("mesh.", SensorSnapPoints.Nearest(Plain, ModelOf(SensorPresetCatalog.FENCE), 2.9, 1.75)!.Name);
    }

    [Fact, Trait("Scenario", "MNT-006")]
    public void should_cycle_through_allowed_points_when_pressing_tab()
    {
        var model = ModelOf(SensorPresetCatalog.UNDERGROUND);
        Assert.Equal("ground.out", SensorSnapPoints.Next(Plain, model, null)!.Name);
        Assert.Equal("ground.in", SensorSnapPoints.Next(Plain, model, "ground.out")!.Name);
        Assert.Equal("ground.out", SensorSnapPoints.Next(Plain, model, "ground.in")!.Name);          // 끝에서 처음으로
        Assert.Equal("ground.out", SensorSnapPoints.Next(Plain, model, "ground.in", backwards: true)!.Name);
    }
    #endregion

    #region - 자리 계산 -
    [Fact, Trait("Scenario", "MNT-010")]
    public void should_follow_fence_height_when_preset_is_snapped_to_point()
    {
        var preset = SensorPresetCatalog.MountPresets.First(p => p.ModelId == SensorPresetCatalog.MULTI && p.IsDefault);   // 기둥 B 중간
        var tall = SensorMountResolver.Resolve(preset, null, new FenceSampleBay(4.0, 3.0));
        Assert.Equal(3.0 * FenceSampleBay.MID_RATIO, tall.HeightM, 3);
        Assert.Equal(4.0, tall.AlongM, 3);                                                     // 칸 폭에 비례(기둥 B = 1.0)
    }

    [Fact, Trait("Scenario", "MNT-011")]
    public void should_never_float_above_post_or_dig_below_ground_when_override_is_extreme()
    {
        var preset = SensorPresetCatalog.MountPresets.First(p => p.ModelId == SensorPresetCatalog.MULTI && p.IsDefault);
        var up = SensorMountResolver.Resolve(preset, new SensorMountOverride { HeightM = 9 }, Plain);
        Assert.Equal(Plain.PostTopM, up.HeightM, 3);
        var down = SensorMountResolver.Resolve(preset, new SensorMountOverride { HeightM = -3 }, Plain);
        Assert.Equal(SensorMountResolver.MIN_ABOVE_GROUND_M, down.HeightM, 3);
        // 철조망 칸이면 코일 꼭대기까지
        Assert.Equal(Razor.CoilTopM, SensorMountResolver.Resolve(preset, new SensorMountOverride { HeightM = 9 }, Razor).HeightM, 3);
        // 지중은 땅속만
        var seismic = SensorPresetCatalog.MountPresets.First(p => p.ModelId == SensorPresetCatalog.UNDERGROUND);
        Assert.Equal(0, SensorMountResolver.Resolve(seismic, new SensorMountOverride { HeightM = 1 }, Plain).HeightM, 3);
    }

    [Fact, Trait("Scenario", "MNT-012")]
    public void should_put_razor_preset_on_coil_when_bay_has_razor_and_fall_back_to_rail_when_not()
    {
        var onCoil = SensorPresetCatalog.MountPresets.First(p => p.SnapPoint == "razor.loop_b");
        Assert.Equal(Razor.CoilCenterM, SensorMountResolver.Resolve(onCoil, null, Razor).HeightM, 3);
        var noRazor = SensorMountResolver.Resolve(onCoil, null, Plain);
        Assert.Equal(2.4, noRazor.HeightM, 3);                                                   // 상단 레일 가운데로
        Assert.Equal(SensorMountAnchor.Rail, noRazor.Anchor);
    }

    [Fact, Trait("Scenario", "MNT-013")]
    public void should_snap_yaw_to_fifteen_degrees_unless_alt_is_held()
    {
        Assert.Equal(60, SensorMountResolver.SnapYaw(53, fine: false));
        Assert.Equal(53, SensorMountResolver.SnapYaw(53.2, fine: true));
        Assert.Equal(-90, SensorMountResolver.SnapYaw(270, fine: false));
        Assert.Equal(90, SensorMountResolver.YawFromHandle(0, 1, fine: false));                // 바깥쪽 정면
        Assert.Equal(0, SensorMountResolver.YawFromHandle(1, 0.05, fine: false));              // 펜스 따라
    }

    [Fact, Trait("Scenario", "MNT-014")]
    public void should_move_ten_centimetres_or_one_with_shift_when_nudging()
    {
        var preset = SensorPresetCatalog.MountPresets.First(p => p.ModelId == SensorPresetCatalog.FENCE && p.IsDefault);
        var up = SensorMountResolver.Nudge(preset, Plain, 0, 1, fine: false);
        Assert.Equal(1.8 + 0.1, up.HeightM, 3);
        Assert.Null(up.SnapPoint);                                                                // 자유 위치
        var fine = SensorMountResolver.Nudge(preset, Plain, 1, 0, fine: true);
        Assert.Equal(1.5 + 0.01, SensorMountResolver.Resolve(fine, null, Plain).AlongM, 3);
    }

    [Fact, Trait("Scenario", "MNT-015")]
    public void should_suggest_name_that_tells_the_place_when_saving()
    {
        var multi = SensorPresetCatalog.MountPresets.First(p => p.ModelId == SensorPresetCatalog.MULTI && p.IsDefault);
        Assert.Equal("기둥 중간 1.8 m · 밖향 · 브래킷 0.3 m", multi.Name);
        var top = SensorPresetCatalog.MountPresets.First(p => p.ModelId == SensorPresetCatalog.MULTI && p.SnapPoint == "post_b.top");
        Assert.Equal("기둥 꼭대기 2.6 m · 밖향 · 아래로 10°", top.Name);
    }

    [Fact, Trait("Scenario", "MNT-016")]
    public void should_record_only_changed_fields_when_diffing_against_preset()
    {
        var preset = SensorPresetCatalog.MountPresets.First(p => p.ModelId == SensorPresetCatalog.MULTI && p.IsDefault);
        var pose = SensorMountResolver.Resolve(preset, null, Plain);
        Assert.Null(SensorMountResolver.Diff(preset, pose, Plain));
        var moved = SensorMountResolver.Diff(preset, pose with { HeightM = pose.HeightM + 0.2 }, Plain);
        Assert.Equal(1, moved!.Count);
        Assert.Equal(pose.HeightM + 0.2, moved.HeightM!.Value, 3);
    }
    #endregion

    #region - 이관 · 결선 자리 -
    [Fact, Trait("Scenario", "MNT-020")]
    public void should_pick_nearest_preset_and_keep_height_difference_when_migrating_legacy_mount()
    {
        var presets = SensorPresetCatalog.MountPresets.Where(p => p.ModelId == SensorPresetCatalog.MULTI).ToList();
        var legacy = new SensorMountSpec(1, FenceMountSpot.PostTop, HeightOffsetM: 0.2);           // 기둥 위 2.4 + 0.2
        var r = SensorMountMigration.FromLegacy(legacy, presets, Plain)!.Value;
        Assert.Equal("post_b.top", r.Preset.SnapPoint);
        var pose = SensorMountResolver.Resolve(r.Preset, r.Override, Plain);
        Assert.Equal(2.6, pose.HeightM, 3);                                                          // 자리는 그대로
    }

    [Fact, Trait("Scenario", "MNT-021")]
    public void should_keep_legacy_mount_without_override_when_it_matches_preset()
    {
        var presets = SensorPresetCatalog.MountPresets.Where(p => p.ModelId == SensorPresetCatalog.FENCE).ToList();
        var legacy = new SensorMountSpec(0, FenceMountSpot.PanelCenter);                            // 망 가운데 1.2 m = 메쉬 아래 0.5H
        var r = SensorMountMigration.FromLegacy(legacy, presets, Plain)!.Value;
        Assert.Equal("mesh.low", r.Preset.SnapPoint);
        Assert.Null(r.Override);
    }

    [Fact, Trait("Scenario", "MNT-022")]
    public void should_write_legacy_spot_for_chain_order_when_applying_pose()
    {
        var preset = SensorPresetCatalog.MountPresets.First(p => p.ModelId == SensorPresetCatalog.MULTI && p.IsDefault);
        var pose = SensorMountResolver.Resolve(preset, null, Plain);
        var spec = SensorMountMigration.WithPose(new SensorMountSpec(2, FenceMountSpot.PanelCenter), 2, pose);
        Assert.Equal(FenceMountSpot.PostMiddle, spec.Spot);
        Assert.Equal(3, spec.Panel);                                                                // 기둥 B = 칸 2 의 오른쪽 기둥
        Assert.Equal(2, SensorMountMigration.BayIndexOf(spec, 5, pose.AlongRatio));
    }

    [Fact, Trait("Scenario", "MNT-023")]
    public void should_round_trip_preset_fields_when_layout_json_is_saved()
    {
        var doc = new FenceLayoutDocument
        {
            ControllerId = 1,
            Panels = new[] { FencePanelSpec.Default() },
            Mounts = new Dictionary<int, SensorMountSpec>
            {
                [104] = new SensorMountSpec(1, FenceMountSpot.PostMiddle) { ModelId = 2, MountPresetId = 4, MountOverride = new SensorMountOverride { HeightM = 2.0 }, AttrPresetId = 2 },
                [105] = new SensorMountSpec(0, FenceMountSpot.PanelCenter),
            },
        };
        var json = FenceLayoutJson.Serialize(doc);
        Assert.Contains("\"mount_preset_id\":4", json);
        Assert.DoesNotContain("mount_override\":null", json);
        var back = FenceLayoutJson.Deserialize(json)!;
        Assert.Equal(4, back.Mounts[104].MountPresetId);
        Assert.Equal(2.0, back.Mounts[104].MountOverride!.HeightM);
        Assert.Null(back.Mounts[105].MountPresetId);                                                // 옛 자리는 그대로
    }
    #endregion

    #region - 쓰임 · 저장소 · 내보내기 -
    [Fact, Trait("Scenario", "MNT-030")]
    public void should_count_sensors_with_current_unsaved_layout_overlaid()
    {
        var stored = new[]
        {
            new SensorPresetUsage("s", 1, 104, 2, 5, null),
            new SensorPresetUsage("s", 1, 105, 2, 5, null),
            new SensorPresetUsage("s", 3, 131, 2, 5, null),
        };
        var current = new[] { new SensorPresetUsage("s", 1, 104, 2, 5, null) };                     // 105 는 지금 화면에서 다른 프리셋으로
        var all = SensorPresetUsageMath.Overlay(stored, current);
        Assert.Equal(2, SensorPresetUsageMath.CountMount(all, 5));
        Assert.Equal("\"기둥 꼭대기\" 을 쓰는 센서 2대가 새 자리로 옮겨집니다.", SensorPresetUsageMath.MoveMessage("기둥 꼭대기", 2));
    }

    [Fact, Trait("Scenario", "MNT-031")]
    public async Task should_keep_one_default_per_model_when_saving_new_default()
    {
        var repo = new InMemorySensorPresetRepository();
        var saved = await repo.SaveMountPresetAsync(new SensorMountPreset { ModelId = SensorPresetCatalog.MULTI, Name = "새 자리", IsDefault = true });
        var list = await repo.GetMountPresetsAsync(SensorPresetCatalog.MULTI);
        Assert.Single(list, p => p.IsDefault);
        Assert.True(list.Single(p => p.IsDefault).Id == saved.Id);
    }

    [Fact, Trait("Scenario", "MNT-032")]
    public async Task should_refuse_deleting_builtin_model_but_allow_hiding()
    {
        var repo = new InMemorySensorPresetRepository();
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.DeletePresetAsync(new PresetKey(PresetKind.Model, SensorPresetCatalog.PIR)));
        var hidden = await repo.SaveModelAsync(ModelOf(SensorPresetCatalog.PIR) with { IsHidden = true, Shape = SensorShape.Fence });
        Assert.True(hidden.IsHidden);
        Assert.Equal(SensorShape.Pir, hidden.Shape);                                                 // 기본 모델은 숨기기 · 이름만
        Assert.False(SensorPresetRules.CanDeleteModel(ModelOf(SensorPresetCatalog.PIR), 0));
        Assert.False(SensorPresetRules.CanDeletePreset(3));
    }

    [Fact, Trait("Scenario", "MNT-033")]
    public async Task should_merge_by_model_code_and_preset_name_when_importing_exported_json()
    {
        var a = new InMemorySensorPresetRepository();
        await a.SaveModelAsync(new SensorModel { Code = "LASER_X", Name = "레이저 X", DeviceType = EnumDeviceType.Laser, Shape = SensorShape.Smart, AllowedKinds = new[] { SensorAnchorKind.Post } });
        var laser = (await a.GetModelsAsync()).Single(m => m.Code == "LASER_X");
        await a.SaveMountPresetAsync(new SensorMountPreset { ModelId = laser.Id, Name = "기둥 위", Anchor = SensorMountAnchor.PostTop, HeightM = 2.5, IsDefault = true });
        await a.SaveMountPresetAsync(new SensorMountPreset { ModelId = SensorPresetCatalog.MULTI, Name = "현장 A 자리", Anchor = SensorMountAnchor.PostMid, HeightM = 1.5 });
        var json = await SensorPresetExchange.ExportAsync(a, new DateTime(2026, 10, 13));

        var b = new InMemorySensorPresetRepository();
        var result = await SensorPresetExchange.ImportAsync(b, SensorPresetExchange.Parse(json)!);
        Assert.Equal(1, result.ModelsAdded);
        Assert.Contains(await b.GetMountPresetsAsync(SensorPresetCatalog.MULTI), p => p.Name == "현장 A 자리");
        var again = await SensorPresetExchange.ImportAsync(b, SensorPresetExchange.Parse(json)!);
        Assert.Equal(0, again.ModelsAdded);
        Assert.Equal(0, again.PresetsAdded);                                                          // 두 번째는 바뀜만
        Assert.Equal(SensorPresetCatalog.Models.Count + 1, (await b.GetModelsAsync()).Count);
    }

    [Fact, Trait("Scenario", "MNT-034")]
    public async Task should_throw_not_supported_when_gop_api_repository_is_used()
    {
        var repo = new GopApiSensorPresetRepository();
        Assert.False(repo.CanWrite);
        await Assert.ThrowsAsync<NotSupportedException>(() => repo.GetModelsAsync());
    }

    [Fact, Trait("Scenario", "MNT-035")]
    public void should_ship_seven_builtin_models_each_with_one_default_preset()
    {
        Assert.Equal(7, SensorPresetCatalog.Models.Count);
        Assert.Equal(7, SensorPresetCatalog.Models.Select(m => m.Shape).Distinct().Count());           // PIR · 광케이블이 스마트 상자가 아니다
        foreach (var m in SensorPresetCatalog.Models)
        {
            Assert.True(m.IsBuiltin);
            Assert.Single(SensorPresetCatalog.MountPresets, p => p.ModelId == m.Id && p.IsDefault);
            Assert.Single(SensorPresetCatalog.AttributePresets, p => p.ModelId == m.Id && p.IsDefault);
        }
        Assert.Equal(20, SensorPresetCatalog.AttributePresets.First(p => p.ModelId == SensorPresetCatalog.MULTI).RangeM);
        Assert.Equal(2.5, SensorPresetCatalog.AttributePresets.First(p => p.ModelId == SensorPresetCatalog.FENCE).SpacingM);
        Assert.Equal(SensorPresetCatalog.SMART2, SensorPresetCatalog.DefaultModelFor(EnumDeviceType.SmartMultisensor2).Id);
    }
    #endregion
}
