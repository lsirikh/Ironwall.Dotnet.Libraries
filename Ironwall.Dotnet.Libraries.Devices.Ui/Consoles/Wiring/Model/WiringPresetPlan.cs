using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;

/// <summary>[위치 프리셋 적용…] 범위(sensor-mount-preset FR-16).</summary>
public enum MountPresetApplyScope
{
    /// <summary>고른 센서.</summary>
    Selected = 0,
    /// <summary>고른 센서와 같은 모델 전체(이 제어기).</summary>
    SameModel = 1,
    /// <summary>이 제어기 전체 — 모델이 다른 센서는 모델별로 고른 프리셋.</summary>
    Controller = 2,
}

/// <summary>
/// 결선맵 위치 프리셋 계산 — <b>순수 함수</b>(sensor-mount-preset FR-15 · FR-16 · FR-17 · FR-19). 뷰모델은 결과를 되돌리기 한 걸음으로 적용만 한다.
/// </summary>
public static class WiringPresetPlan
{
    /// <summary>센서의 모델 id — 자리에 적힌 것, 없으면 장비 종류의 기본 모델.</summary>
    public static int ModelIdOf(SensorMountSpec? mount, EnumDeviceType type, IReadOnlyList<SensorModel>? models = null)
        => mount?.ModelId ?? SensorPresetCatalog.DefaultModelFor(type, models).Id;

    /// <summary>
    /// 프리셋으로 서는 센서의 자세 — 프리셋을 모르면(지워졌거나 아직 못 읽음) <c>null</c>(그 센서는 9자리 계산으로 그린다).
    /// </summary>
    public static SensorMountPose? PoseOf(SensorMountSpec? mount, IReadOnlyList<FencePanelSpec> panels, Func<int, SensorMountPreset?> presetOf)
    {
        if (mount is not { MountPresetId: { } id } || panels is null || panels.Count == 0 || presetOf?.Invoke(id) is not { } preset) return null;
        var bay = panels[SensorMountMigration.BayIndexOf(mount, panels.Count, preset.AlongRatio)];
        return SensorMountResolver.Resolve(preset, mount.MountOverride, FenceSampleBay.FromPanel(bay));
    }

    /// <summary>
    /// 프리셋을 센서에 적용 — 센서가 서 있던 칸에 프리셋 자리로 세우고 따로 고친 값은 지운다(그 자리 칸 · 순서용 9자리도 같이 적는다).
    /// </summary>
    public static SensorMountSpec Apply(SensorMountSpec mount, SensorMountPreset preset, IReadOnlyList<FencePanelSpec> panels)
    {
        ArgumentNullException.ThrowIfNull(mount);
        ArgumentNullException.ThrowIfNull(preset);
        if (panels is null || panels.Count == 0) return mount with { ModelId = preset.ModelId, MountPresetId = preset.Id, MountOverride = null };
        var bayIndex = SensorMountMigration.BayIndexOf(mount, panels.Count, preset.AlongRatio);
        var pose = SensorMountResolver.Resolve(preset, null, FenceSampleBay.FromPanel(panels[bayIndex]));
        var next = SensorMountMigration.WithPose(mount, bayIndex, pose) with { ModelId = preset.ModelId, MountPresetId = preset.Id, MountOverride = null };
        return FenceLayoutMath.Normalize(next, panels);
    }

    /// <summary>
    /// 따로 고친 값 하나를 바꾼다(설치 위치 절 수치 칸 · 끌기) — 프리셋과 같아지면 그 칸은 지운다. 프리셋이 없으면 그대로.
    /// </summary>
    public static SensorMountSpec WithOverride(SensorMountSpec mount, SensorMountPreset preset, IReadOnlyList<FencePanelSpec> panels, Func<SensorMountPose, SensorMountPose> change)
    {
        ArgumentNullException.ThrowIfNull(mount);
        ArgumentNullException.ThrowIfNull(change);
        if (panels is null || panels.Count == 0 || preset is null) return mount;
        var bay = FenceSampleBay.FromPanel(panels[SensorMountMigration.BayIndexOf(mount, panels.Count, preset.AlongRatio)]);
        var now = SensorMountResolver.Resolve(preset, mount.MountOverride, bay);
        var next = SensorMountResolver.Clamp(change(now), bay);
        return mount with { MountOverride = SensorMountResolver.Diff(preset, next, bay) };
    }

    /// <summary>
    /// 칸 <paramref name="bayIndex"/> 에 그리도록 자리 번호(<see cref="SensorMountSpec.Panel"/>)를 고른다 — <see cref="SensorMountMigration.BayIndexOf"/> 의 거꿀(기둥 자리는
    /// 프리셋이 기둥 B 면 칸 + 1). 9자리 갈래(Spot)는 그대로 둔다(그 갈래가 칸 고르기 규칙을 정하므로).
    /// </summary>
    public static int PanelForBay(SensorMountSpec mount, SensorMountPreset preset, int bayIndex, int panelCount)
    {
        ArgumentNullException.ThrowIfNull(mount);
        ArgumentNullException.ThrowIfNull(preset);
        var bay = Math.Clamp(bayIndex, 0, Math.Max(0, panelCount - 1));
        return mount.IsPostSpot && preset.AlongRatio >= 0.5 ? bay + 1 : bay;
    }

    /// <summary>
    /// 결선맵 펜스 보기 끌기(sensor-mount-preset W4) — 센서를 칸 <paramref name="bayIndex"/> 의 (가로 몫 · 높이 · 펜스와 거리)로 옮긴다.
    /// 프리셋은 그대로이고 프리셋과 다른 값은 "따로 고친 값"으로 남는다(같아지면 지운다 · 기둥 위로 뜨거나 바닥을 뚫지 않게 칸 범위 안으로).
    /// </summary>
    public static SensorMountSpec MoveTo(SensorMountSpec mount, SensorMountPreset preset, IReadOnlyList<FencePanelSpec> panels, int bayIndex,
                                         double alongRatio, double heightM, double? depthM = null)
    {
        ArgumentNullException.ThrowIfNull(mount);
        ArgumentNullException.ThrowIfNull(preset);
        if (panels is null || panels.Count == 0) return mount;
        var bayAt = Math.Clamp(bayIndex, 0, panels.Count - 1);
        var moved = mount with { Panel = PanelForBay(mount, preset, bayAt, panels.Count) };
        var bay = FenceSampleBay.FromPanel(panels[bayAt]);
        var now = SensorMountResolver.Resolve(preset, mount.MountOverride, bay);
        var next = SensorMountResolver.Clamp(now with { AlongRatio = alongRatio, HeightM = heightM, DepthM = depthM ?? now.DepthM }, bay);
        return moved with { MountOverride = SensorMountResolver.Diff(preset, next, bay) };
    }

    /// <summary>
    /// 칸 하나의 붙는 점과 붙을 수 있나 — 모델이 허용하는 갈래이고, 지중 프리셋이면 바닥 점만 · 그 밖이면 바닥 점 빼고
    /// (따로 고친 값은 붙는 곳 갈래를 바꾸지 못한다 — 갈래를 바꾸려면 다른 프리셋을 고른다).
    /// </summary>
    public static IReadOnlyList<(SensorSnapPoint Point, bool Allowed)> SnapChoices(FenceSampleBay bay, SensorModel? model, SensorMountPreset preset)
    {
        ArgumentNullException.ThrowIfNull(bay);
        ArgumentNullException.ThrowIfNull(preset);
        var ground = preset.Anchor == SensorMountAnchor.Ground;
        return SensorSnapPoints.For(bay)
            .Select(p => (p, (model is null || model.Allows(p.Kind)) && (p.Kind == SensorAnchorKind.Ground) == ground))
            .ToList();
    }

    /// <summary>범위 안 센서 — 고른 센서 · 그 모델 전체 · 제어기 전체(체인 차례).</summary>
    public static IReadOnlyList<int> TargetsOf(MountPresetApplyScope scope, IReadOnlyList<int> selected, IReadOnlyList<int> chain, Func<int, int> modelOf)
    {
        var sel = selected ?? Array.Empty<int>();
        var all = chain ?? Array.Empty<int>();
        return scope switch
        {
            MountPresetApplyScope.Selected => sel.ToList(),
            MountPresetApplyScope.SameModel => sel.Select(modelOf).Distinct().ToHashSet() is var models ? all.Where(k => models.Contains(modelOf(k))).ToList() : new List<int>(),
            _ => all.ToList(),
        };
    }

    /// <summary>
    /// 범위에 프리셋 적용(모델별 프리셋 — 모델이 다른 센서가 섞이면 모델마다 고른 것). 그 모델 프리셋을 고르지 않은 센서는 건너뛴다.
    /// </summary>
    public static (IReadOnlyDictionary<int, SensorMountSpec> Mounts, int Applied) ApplyToTargets(
        IReadOnlyDictionary<int, SensorMountSpec> mounts, IReadOnlyList<int> targets, IReadOnlyDictionary<int, SensorMountPreset> presetByModel,
        Func<int, int> modelOf, IReadOnlyList<FencePanelSpec> panels)
    {
        var result = new Dictionary<int, SensorMountSpec>(mounts ?? new Dictionary<int, SensorMountSpec>());
        var applied = 0;
        foreach (var key in targets ?? Array.Empty<int>())
        {
            if (!result.TryGetValue(key, out var mount) || !presetByModel.TryGetValue(modelOf(key), out var preset)) continue;
            var next = Apply(mount, preset, panels);
            if (next != mount) applied++;
            result[key] = next;
        }
        return (result, applied);
    }

    /// <summary>
    /// 저장할 때 옛 자리(프리셋 없음)를 가장 가까운 프리셋 + 차이로 옮긴다(FR-19) — 화면의 자리는 그대로(자세가 같다). 프리셋 목록이 없으면 그대로.
    /// </summary>
    public static SensorMountSpec Migrate(SensorMountSpec mount, int modelId, IReadOnlyList<SensorMountPreset> presets, IReadOnlyList<FencePanelSpec> panels)
    {
        ArgumentNullException.ThrowIfNull(mount);
        if (mount.HasMountPreset || presets is null || presets.Count == 0 || panels is null || panels.Count == 0) return mount;
        var bayIndex = SensorMountMigration.BayIndexOf(mount, panels.Count, mount.IsPostSpot && mount.Panel > 0 ? 1 : 0);
        var bay = FenceSampleBay.FromPanel(panels[bayIndex]);
        if (SensorMountMigration.FromLegacy(mount, presets, bay) is not { } found) return mount;
        var overrides = found.Override;
        if (mount.IsPostSpot)
        {
            // 그릴 때 고르는 칸(프리셋이 기둥 B 면 왼쪽 칸)에서 이 기둥이 A(0) 인지 B(1) 인지 — 프리셋 쪽과 다르면 가로를 따로 고친 값으로 남겨 기둥이 옮겨지지 않게.
            var renderBay = SensorMountMigration.BayIndexOf(mount, panels.Count, found.Preset.AlongRatio);
            var wanted = mount.Panel == renderBay ? 0.0 : 1.0;
            var presetAlong = SensorMountResolver.Resolve(found.Preset, null, FenceSampleBay.FromPanel(panels[renderBay])).AlongRatio;
            if (Math.Abs(presetAlong - wanted) > 1e-6) overrides = (overrides ?? new SensorMountOverride()) with { AlongRatio = wanted };
        }
        return mount with { ModelId = modelId, MountPresetId = found.Preset.Id, MountOverride = overrides };
    }

    /// <summary>"프리셋에서 바뀜: 높이 +0.20 · 방향 60°" — 고친 칸이 없으면 빈 글자.</summary>
    public static string OverrideText(SensorMountPreset? preset, SensorMountSpec? mount, IReadOnlyList<FencePanelSpec> panels)
    {
        if (preset is null || mount?.MountOverride is not { IsEmpty: false } o || panels is null || panels.Count == 0) return string.Empty;
        var bay = FenceSampleBay.FromPanel(panels[SensorMountMigration.BayIndexOf(mount, panels.Count, preset.AlongRatio)]);
        var basePose = SensorMountResolver.Resolve(preset, null, bay);
        var parts = new List<string>();
        if (o.HeightM is { } h) parts.Add($"높이 {h - basePose.HeightM:+0.00;-0.00}");
        if (o.AlongRatio is { } a) parts.Add($"가로 {(a - basePose.AlongRatio) * bay.PanelWidthM:+0.00;-0.00} m");
        if (o.DepthM is { } d) parts.Add($"펜스와 거리 {d:0.00} m");
        if (o.YawDeg is { } y) parts.Add($"방향 {y:0}°");
        if (o.TiltDeg is { } t) parts.Add($"위아래 {t:0}°");
        if (o.BracketM is { } b) parts.Add($"브래킷 {b:0.00} m");
        return "프리셋에서 바뀜: " + string.Join(" · ", parts);
    }
}
