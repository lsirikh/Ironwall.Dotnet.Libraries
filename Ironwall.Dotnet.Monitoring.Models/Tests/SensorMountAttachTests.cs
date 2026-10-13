using Ironwall.Dotnet.Monitoring.Models.Fences;
using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Monitoring.Models.Tests;

/// <summary>
/// 불변식 "센서는 늘 실제 물체 하나에 붙는다"(sensor-mount-preset FR-22) — 표 기반 · 속성 기반 순수 시험(창 없음).
/// 모델 7종 × 붙는 점 전부 × 펜스 높이 1.8/2.4/3.0 × 철조망 있음/없음 × 담 × 따로 고친 값(큰 ±) × 자유 위치 조합에서 물체와의 거리 = 0.
/// </summary>
public class SensorMountAttachTests
{
    private static readonly double[] FenceHeights = { 1.8, 2.4, 3.0 };

    /// <summary>칸 샘플 — 높이 3종 × (철망 · 철망+철조망 · 담).</summary>
    private static IEnumerable<FenceSampleBay> Bays()
    {
        foreach (var h in FenceHeights)
        {
            yield return new FenceSampleBay(3.0, h);
            yield return new FenceSampleBay(3.0, h, HasRazor: true);
            yield return new FenceSampleBay(2.0, h, IsWall: true);
        }
    }

    private static readonly double?[] HeightOverrides = { null, -9, -2.5, -0.4, 0, 0.02, 0.6, 1.5, 2.3, 2.45, 2.7, 3.2, 4.5, 9 };
    private static readonly double?[] AlongOverrides = { null, -0.5, 0, 0.03, 0.3, 0.5, 0.94, 1, 1.7 };
    private static readonly double?[] DepthOverrides = { null, 0, 0.15, -0.6, 3.5 };

    /// <summary>기본 프리셋 전부 + 모든 칸의 모든 붙는 점으로 만든 프리셋(모델마다).</summary>
    private static IEnumerable<SensorMountPreset> AllPresets()
    {
        foreach (var p in SensorPresetCatalog.MountPresets) yield return p;
        var bays = new[] { new FenceSampleBay(3.0, 2.4, HasRazor: true), new FenceSampleBay(3.0, 2.4, IsWall: true) };
        foreach (var model in SensorPresetCatalog.Models)
            foreach (var bay in bays)
                foreach (var point in SensorSnapPoints.For(bay))
                    yield return SensorMountResolver.SnapTo(new SensorMountPreset { ModelId = model.Id, BracketM = 0.2 }, point, bay);
    }

    #region - 표 기반 -
    [Fact, Trait("Scenario", "MNT-400")]
    public void should_attach_every_preset_point_to_an_object_when_fence_height_razor_and_wall_vary()
    {
        var checkedCount = 0;
        foreach (var preset in AllPresets())
            foreach (var bay in Bays())
            {
                var pose = SensorMountResolver.Resolve(preset, null, bay);
                Assert.True(SensorMountSurfaces.IsAttached(pose, bay), $"{preset.SnapPoint} · {bay} → {pose}");
                checkedCount++;
            }
        Assert.True(checkedCount > 500);
    }

    [Fact, Trait("Scenario", "MNT-401")]
    public void should_never_float_when_overrides_push_height_along_and_depth_far_out()
    {
        var count = 0;
        foreach (var preset in AllPresets().Where((_, i) => i % 3 == 0))
            foreach (var bay in Bays())
                foreach (var h in HeightOverrides)
                    foreach (var a in AlongOverrides)
                        foreach (var d in DepthOverrides)
                        {
                            var o = new SensorMountOverride { HeightM = h, AlongRatio = a, DepthM = d };
                            var att = SensorMountResolver.ResolveAttached(preset, o, bay);
                            Assert.Equal(0, SensorMountSurfaces.GapToObject(att.Pose, bay));
                            Assert.InRange(att.Pose.AlongRatio, 0, 1);
                            if (att.Pose.Anchor == SensorMountAnchor.Ground) Assert.InRange(att.Pose.HeightM, SensorMountResolver.MIN_BURIED_M, 0);
                            else
                            {
                                Assert.InRange(att.Pose.HeightM, SensorMountResolver.MIN_ABOVE_GROUND_M - 1e-9, bay.Normalized().TopLimitM + 1e-9);
                                Assert.True(Math.Abs(att.Pose.DepthM) <= att.Pose.BracketM + 1e-9);   // 브래킷 끝까지만
                            }
                            if (att.Clamped) Assert.False(string.IsNullOrWhiteSpace(att.Reason));      // 끌어 붙였으면 까닭
                            count++;
                        }
        Assert.True(count > 10000);
    }

    [Theory, Trait("Scenario", "MNT-402")]
    [InlineData(1.8)]
    [InlineData(2.4)]
    [InlineData(3.0)]
    public void should_stop_at_the_post_top_and_say_so_when_height_override_is_above_the_post(double fenceH)
    {
        var preset = SensorPresetCatalog.MountPresets.First(p => p.ModelId == SensorPresetCatalog.MULTI && p.IsDefault);   // 기둥 B 중간
        var bay = new FenceSampleBay(3.0, fenceH);
        var att = SensorMountResolver.ResolveAttached(preset, new SensorMountOverride { HeightM = fenceH + 2 }, bay);
        Assert.Equal(bay.PostTopM, att.Pose.HeightM, 3);
        Assert.True(att.Clamped);
        Assert.Contains("기둥 높이를 넘어 기둥 꼭대기에 붙였습니다", att.Reason);
    }

    [Fact, Trait("Scenario", "MNT-403")]
    public void should_move_a_razor_mounted_sensor_onto_the_rail_when_the_bay_has_no_coil()
    {
        // 프리셋은 철조망 칸에서 정했지만 이 칸엔 철조망이 없다 — 코일 높이(허공)에 남지 않는다
        var preset = SensorPresetCatalog.MountPresets.First(p => p.SnapPoint == "razor.top.50");
        foreach (var h in FenceHeights)
        {
            var bay = new FenceSampleBay(3.0, h);
            var pose = SensorMountResolver.Resolve(preset, null, bay);
            Assert.Equal(SensorMountAnchor.Rail, pose.Anchor);
            Assert.Equal(h, pose.HeightM, 3);
            // 따로 고친 높이가 코일 높이여도 레일에
            var raw = new SensorMountPose(SensorMountAnchor.Razor, 0.5, 0, h + 0.4, 0, 0, 0, 0);
            var att = SensorMountResolver.Attach(raw, bay);
            Assert.Equal(SensorMountAnchor.Rail, att.Pose.Anchor);
            Assert.Contains("철조망이 없는 칸", att.Reason);
            Assert.True(SensorMountSurfaces.IsAttached(att.Pose, bay));
        }
    }

    [Fact, Trait("Scenario", "MNT-404")]
    public void should_hang_on_mesh_instead_of_air_when_a_post_preset_is_moved_into_the_middle_of_the_bay()
    {
        var bay = new FenceSampleBay(3.0, 2.4);
        var post = new SensorMountPose(SensorMountAnchor.PostMid, 0.3, 0, 1.6, 0, 90, 0, 0);
        var att = SensorMountResolver.Attach(post, bay);
        Assert.Equal(SensorMountAnchor.Mesh, att.Pose.Anchor);                 // 그 자리를 품은 물체 = 메쉬(자리는 그대로)
        Assert.Equal(0.3, att.Pose.AlongRatio, 4);
        Assert.Equal(1.6, att.Pose.HeightM, 3);
        Assert.False(att.Clamped);

        // 메쉬 위(망 높이를 넘는) 칸 가운데는 허공 — 기둥 갈래 그대로 가장 가까운 기둥에 끌어 붙인다
        var air = SensorMountResolver.Attach(post with { HeightM = 2.55 }, bay);
        Assert.True(air.Clamped);
        Assert.Equal(SensorAnchorKind.Post, SensorMountResolver.KindOf(air.Pose.Anchor));
        Assert.Contains(air.Pose.AlongRatio, new[] { 0.0, 1.0 });
        Assert.Contains("기둥이 없는 자리", air.Reason);
    }

    [Fact, Trait("Scenario", "MNT-405")]
    public void should_keep_the_sensor_within_bracket_reach_when_depth_exceeds_the_bracket()
    {
        var bay = new FenceSampleBay(3.0, 2.4);
        var noBracket = SensorMountResolver.Attach(new SensorMountPose(SensorMountAnchor.Mesh, 0.5, 0, 1.2, -2.0, 90, 0, 0), bay);
        Assert.Equal(0, noBracket.Pose.DepthM, 3);
        Assert.Contains("브래킷이 없어", noBracket.Reason);

        var withBracket = SensorMountResolver.Attach(new SensorMountPose(SensorMountAnchor.PostMid, 1, 0, 1.8, 1.5, 90, 0, 0.3), bay);
        Assert.Equal(0.3, withBracket.Pose.DepthM, 3);
        Assert.Contains("브래킷 길이(0.3 m)", withBracket.Reason);

        var buried = SensorMountResolver.Attach(new SensorMountPose(SensorMountAnchor.Ground, 0.5, 0, -0.3, 3.0, 0, 0, 0), bay);
        Assert.Equal(3.0, buried.Pose.DepthM, 3);                              // 지중은 땅이 물체 — 브래킷과 무관
        Assert.False(buried.Clamped);
    }

    [Fact, Trait("Scenario", "MNT-406")]
    public void should_use_the_wall_top_or_face_when_the_bay_is_a_wall()
    {
        var wall = new FenceSampleBay(2.0, 2.2, IsWall: true);
        var high = SensorMountResolver.Attach(new SensorMountPose(SensorMountAnchor.PostTop, 1, 0, 2.6, 0, 90, 0, 0), wall);
        Assert.Equal(SensorMountAnchor.WallTop, high.Pose.Anchor);
        Assert.Equal(2.2, high.Pose.HeightM, 3);                                // 담 위(+0.3 허공 아님)
        var low = SensorMountResolver.Attach(new SensorMountPose(SensorMountAnchor.Mesh, 0.5, 0, 1.0, 0, 90, 0, 0), wall);
        Assert.Equal(SensorMountAnchor.WallFace, low.Pose.Anchor);
        Assert.Equal(1.0, low.Pose.HeightM, 3);
        var above = SensorMountResolver.Attach(new SensorMountPose(SensorMountAnchor.WallTop, 0.5, 0, 2.5, 0, 90, 0, 0), wall);
        Assert.Equal(2.2, above.Pose.HeightM, 3);
        Assert.True(above.Clamped);
    }
    #endregion

    #region - 자유 위치(Alt · 화살표 · 수치 칸) -
    [Fact, Trait("Scenario", "MNT-410")]
    public void should_keep_free_placement_exact_on_an_object_surface_and_snap_air_to_the_nearest_surface()
    {
        var bay = new FenceSampleBay(3.0, 2.4);
        var multi = SensorPresetCatalog.MountPresets.First(p => p.ModelId == SensorPresetCatalog.MULTI && p.IsDefault);
        var onMesh = SensorMountResolver.MoveFreeAttached(multi, bay, 1.234, 1.567);
        Assert.False(onMesh.Attachment.Clamped);                                // 메쉬 면 위 — 1 cm 단위 그대로
        Assert.Equal(1.23, SensorMountResolver.Resolve(onMesh.Preset, null, bay).AlongM, 2);
        Assert.Equal(1.57, SensorMountResolver.Resolve(onMesh.Preset, null, bay).HeightM, 2);

        var inAir = SensorMountResolver.MoveFreeAttached(multi, bay, 1.5, 2.55);       // 망 위 · 기둥 사이 = 허공
        Assert.True(inAir.Attachment.Clamped);
        Assert.False(string.IsNullOrWhiteSpace(inAir.Attachment.Reason));
        Assert.True(SensorMountSurfaces.IsAttached(SensorMountResolver.Resolve(inAir.Preset, null, bay), bay));
    }

    [Fact, Trait("Scenario", "MNT-411")]
    public void should_stay_attached_after_any_sequence_of_nudges_and_free_moves()
    {
        var rng = new Random(20261013);
        foreach (var bay in Bays())
            foreach (var preset in SensorPresetCatalog.MountPresets)
            {
                var p = preset;
                for (var i = 0; i < 60; i++)
                {
                    p = (rng.Next(3)) switch
                    {
                        0 => SensorMountResolver.Nudge(p, bay, rng.Next(-3, 4), rng.Next(-3, 4), rng.Next(2) == 0),
                        1 => SensorMountResolver.MoveFree(p, bay, rng.NextDouble() * 5 - 1, rng.NextDouble() * 6 - 1.5, rng.NextDouble() * 4 - 2),
                        _ => p with { BracketM = rng.NextDouble() * 0.8 },
                    };
                    var pose = SensorMountResolver.Resolve(p, null, bay);
                    Assert.True(SensorMountSurfaces.IsAttached(pose, bay), $"{i}: {pose} @ {bay}");
                }
            }
    }

    [Fact, Trait("Scenario", "MNT-412")]
    public void should_attach_random_poses_of_every_anchor_in_property_based_sweep()
    {
        var rng = new Random(4242);
        var anchors = Enum.GetValues<SensorMountAnchor>();
        for (var i = 0; i < 20000; i++)
        {
            var bay = new FenceSampleBay(0.5 + rng.NextDouble() * 8, 0.5 + rng.NextDouble() * 4, rng.Next(2) == 0, rng.Next(4) == 0);
            var raw = new SensorMountPose(anchors[rng.Next(anchors.Length)], rng.NextDouble() * 1.6 - 0.3, 0, rng.NextDouble() * 12 - 4,
                                          rng.NextDouble() * 12 - 6, rng.NextDouble() * 720 - 360, rng.NextDouble() * 400 - 200, rng.NextDouble() * 3 - 0.5);
            var att = SensorMountResolver.Attach(raw, bay);
            Assert.Equal(0, SensorMountSurfaces.GapToObject(att.Pose, bay));
            Assert.Equal(att.Pose, SensorMountResolver.Attach(att.Pose, bay).Pose);           // 한 번 붙인 자세는 다시 붙여도 그대로(멱등)
            Assert.False(SensorMountResolver.Attach(att.Pose, bay).Clamped);
        }
    }
    #endregion

    #region - 이관 · 저장 보정 -
    [Fact, Trait("Scenario", "MNT-420")]
    public void should_attach_legacy_mounts_with_extreme_height_offsets_when_migrating()
    {
        foreach (var bay in Bays())
            foreach (var model in SensorPresetCatalog.Models)
            {
                var presets = SensorPresetCatalog.MountPresets.Where(p => p.ModelId == model.Id).ToList();
                foreach (var spot in Enum.GetValues<FenceMountSpot>())
                    foreach (var offset in new[] { -3.0, -1.0, 0, 0.6, 3.0 })
                    {
                        var legacy = new SensorMountSpec(1, spot, offset);
                        if (SensorMountMigration.FromLegacy(legacy, presets, bay) is not { } r) continue;
                        var pose = SensorMountResolver.Resolve(r.Preset, r.Override, bay);
                        Assert.True(SensorMountSurfaces.IsAttached(pose, bay), $"{model.Code} {spot} {offset} → {pose}");
                    }
            }
    }

    [Fact, Trait("Scenario", "MNT-421")]
    public void should_normalize_a_stored_floating_free_preset_and_report_it_as_corrected()
    {
        var bay = new FenceSampleBay(3.0, 2.4);
        var floating = new SensorMountPreset { Id = 99, ModelId = SensorPresetCatalog.FENCE, Anchor = SensorMountAnchor.Mesh, AlongRatio = 0.5, HeightM = 3.4, DepthM = -1, FenceHeightM = 2.4 };
        var (fixedPreset, att) = SensorMountResolver.Normalize(floating, bay);
        Assert.True(att.Clamped);
        Assert.Equal(2.4, fixedPreset.HeightM, 3);
        Assert.Equal(0, fixedPreset.DepthM, 3);
        Assert.True(SensorMountSurfaces.IsAttached(SensorMountResolver.Resolve(fixedPreset, null, bay), bay));
        Assert.False(SensorMountResolver.Normalize(fixedPreset, bay).Attachment.Clamped);   // 다시 하면 바뀜 없음

        var snapped = SensorPresetCatalog.MountPresets.First(p => p.SnapPoint == "post_b.top");
        var (same, sameAtt) = SensorMountResolver.Normalize(snapped, bay);
        Assert.False(sameAtt.Clamped);
        Assert.True(same.SamePlace(snapped));                                          // 붙은 프리셋은 그대로
    }

    [Fact, Trait("Scenario", "MNT-422")]
    public void should_report_zero_gap_only_for_attached_poses()
    {
        var bay = new FenceSampleBay(3.0, 2.4);
        Assert.Equal(0, SensorMountSurfaces.GapToObject(new SensorMountPose(SensorMountAnchor.Rail, 0.5, 1.5, 2.4, 0, 0, 0, 0), bay));
        Assert.True(SensorMountSurfaces.GapToObject(new SensorMountPose(SensorMountAnchor.Rail, 0.5, 1.5, 2.0, 0, 0, 0, 0), bay) > 0.3);
        Assert.True(SensorMountSurfaces.GapToObject(new SensorMountPose(SensorMountAnchor.Mesh, 0.5, 1.5, 1.0, 0.4, 0, 0, 0.1), bay) > 0.25);
        Assert.Equal(double.PositiveInfinity, SensorMountSurfaces.GapToObject(new SensorMountPose(SensorMountAnchor.Razor, 0.5, 1.5, 2.9, 0, 0, 0, 0), bay));
        Assert.Equal(SensorMountAnchor.PostTop, SensorMountSurfaces.PostAnchorAt(bay, 2.6));
        Assert.Equal(SensorMountAnchor.PostLow, SensorMountSurfaces.PostAnchorAt(bay, 1.1));
    }
    #endregion
}
