using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Monitoring.Models.Fences.Presets;

/// <summary>
/// 결선 자리(<see cref="SensorMountSpec"/> — 망 · 기둥 번호 + 9자리) ↔ 프리셋 자세 — <b>순수 함수</b>(sensor-mount-preset FR-17 · FR-19).
/// </summary>
public static class SensorMountMigration
{
    /// <summary>
    /// 센서가 서 있는 칸(망 번호)과 그 칸 기준 기둥 쪽 — 기둥 자리 q 는 프리셋이 기둥 B(가로 ≥ 0.5)면 망 q−1, 아니면 망 q(끝은 범위 안으로).
    /// </summary>
    public static int BayIndexOf(SensorMountSpec mount, int panelCount, double presetAlong = 0.5)
    {
        ArgumentNullException.ThrowIfNull(mount);
        if (panelCount <= 0) return 0;
        if (!mount.IsPostSpot) return Math.Clamp(mount.Panel, 0, panelCount - 1);
        var q = mount.Panel;
        var bay = presetAlong >= 0.5 ? q - 1 : q;
        if (bay < 0) bay = 0;
        if (bay > panelCount - 1) bay = panelCount - 1;
        return bay;
    }

    /// <summary>
    /// 프리셋 자세를 결선 자리 칸(망 · 기둥 번호 + 9자리)에도 적어 둔다 — 사슬 순서 · 줄 · 칩 쌓기 등 기존 계산이 그대로 돈다.
    /// 높이 조정은 0(높이는 프리셋 자세가 정한다).
    /// </summary>
    public static SensorMountSpec WithPose(SensorMountSpec mount, int bayIndex, SensorMountPose pose)
    {
        ArgumentNullException.ThrowIfNull(mount);
        ArgumentNullException.ThrowIfNull(pose);
        var postSide = pose.AlongRatio >= 0.5 ? 1 : 0;
        var (spot, isPost) = pose.Anchor switch
        {
            SensorMountAnchor.PostTop or SensorMountAnchor.PostRail => (FenceMountSpot.PostTop, true),
            SensorMountAnchor.PostMid or SensorMountAnchor.PostLow => (FenceMountSpot.PostMiddle, true),
            SensorMountAnchor.Rail => (FenceMountSpot.PanelTop, false),
            SensorMountAnchor.Razor or SensorMountAnchor.RazorLoop => (mount.Lane == FenceLane.Upper ? FenceMountSpot.RazorCoil : FenceMountSpot.PanelTop, false),
            SensorMountAnchor.WallTop => (FenceMountSpot.WallTop, false),
            SensorMountAnchor.WallFace => (FenceMountSpot.WallFace, false),
            SensorMountAnchor.Mesh when pose.HeightM < 0 => (FenceMountSpot.PanelBottom, false),
            _ => (FenceMountSpot.PanelCenter, false),
        };
        var column = isPost ? FenceColumn.Center : pose.AlongRatio < 0.35 ? FenceColumn.Left : pose.AlongRatio > 0.65 ? FenceColumn.Right : FenceColumn.Center;
        return mount with
        {
            Panel = isPost ? bayIndex + postSide : bayIndex,
            Spot = spot,
            Column = column,
            HeightOffsetM = 0,
            FacesBack = SensorMountResolver.NormalizeYaw(pose.YawDeg) < -30 && SensorMountResolver.NormalizeYaw(pose.YawDeg) > -150,
        };
    }

    /// <summary>옛 자리(프리셋 없음)의 높이(m) — <see cref="FenceLayoutMath.PointOf"/> 와 같은 규칙(칸 하나 기준).</summary>
    public static double LegacyHeightM(SensorMountSpec mount, FenceSampleBay bay)
    {
        var h = bay.FenceHeightM;
        var height = mount.Spot switch
        {
            FenceMountSpot.PostTop or FenceMountSpot.WallTop => h,
            FenceMountSpot.PanelBottom or FenceMountSpot.WallBottom => h * FenceLayoutMath.PANEL_BOTTOM_RATIO,
            FenceMountSpot.PanelTop => h * FenceLayoutMath.PANEL_TOP_RATIO,
            FenceMountSpot.RazorCoil => FenceLayoutMath.RazorCoilCenterM(h),
            _ => h / 2,
        };
        return height + mount.HeightOffsetM;
    }

    /// <summary>
    /// 옛 자리 → 가장 가까운 프리셋 + 차이(FR-19). 기둥 자리는 기둥 갈래 프리셋을, 망 자리는 그 밖을 먼저 본다(없으면 전체).
    /// 지중 모델(바닥만 허용)은 바닥 프리셋으로. 차이는 1 cm · 0.5° 넘는 칸만 따로 고친 값에 남는다. 프리셋이 없으면 <c>null</c>.
    /// </summary>
    public static (SensorMountPreset Preset, SensorMountOverride? Override)? FromLegacy(SensorMountSpec mount, IReadOnlyList<SensorMountPreset> presets, FenceSampleBay bay)
    {
        ArgumentNullException.ThrowIfNull(mount);
        if (presets is null || presets.Count == 0) return null;
        var b = bay.Normalized();
        var legacyHeight = LegacyHeightM(mount, b);
        var wantPost = mount.IsPostSpot;
        var ground = presets.All(p => p.Anchor == SensorMountAnchor.Ground);
        var candidates = presets.Where(p => (SensorMountResolver.KindOf(p.Anchor) == SensorAnchorKind.Post) == wantPost).ToList();
        if (candidates.Count == 0) candidates = presets.ToList();

        (SensorMountPreset Preset, SensorMountPose Actual, double Distance)? best = null;
        foreach (var p in candidates.OrderByDescending(p => p.IsDefault).ThenBy(p => p.Id))
        {
            var pose = SensorMountResolver.Resolve(p, null, b);
            var along = wantPost ? pose.AlongRatio : FenceLayoutMath.ColumnRatio(mount.Column);
            var actual = ground
                ? pose
                : SensorMountResolver.Clamp(pose with
                {
                    AlongRatio = along,
                    HeightM = legacyHeight,
                    YawDeg = mount.FacesBack ? SensorMountResolver.YAW_INWARD : pose.YawDeg,
                }, b);
            var dx = (actual.AlongRatio - pose.AlongRatio) * b.PanelWidthM;
            var dy = actual.HeightM - pose.HeightM;
            var d = dx * dx + dy * dy;
            if (best is null || d < best.Value.Distance - 1e-9) best = (p, actual, d);
        }
        return best is { } r ? (r.Preset, SensorMountResolver.Diff(r.Preset, r.Actual, b)) : null;
    }
}

/// <summary>프리셋을 쓰는 센서 한 대(어느 서버 · 제어기 · 센서).</summary>
public sealed record SensorPresetUsage(string ServerKey, int ControllerId, int SensorId, int? ModelId, int? MountPresetId, int? AttrPresetId, string? Label = null)
{
    /// <summary>칩 글자 "제어기 1 · 104".</summary>
    public string ChipText => Label ?? $"제어기 {ControllerId} · {SensorId}";
}

/// <summary>모든 결선 레이아웃의 프리셋 쓰임을 읽는다(GIS DB 구현이 함께 낸다 — 없으면 화면은 지금 제어기만 센다).</summary>
public interface ISensorPresetUsageSource
{
    Task<IReadOnlyList<SensorPresetUsage>> GetUsagesAsync(System.Threading.CancellationToken ct = default);
}

/// <summary>쓰임 셈 — <b>순수 함수</b>(W1 "위치 3 · 31대" · W5 "센서 N대").</summary>
public static class SensorPresetUsageMath
{
    /// <summary>레이아웃 문서 하나의 쓰임.</summary>
    public static IReadOnlyList<SensorPresetUsage> FromDocument(string serverKey, FenceLayoutDocument document, Func<int, string?>? labelOf = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Mounts
            .Where(p => p.Value is not null && (p.Value.ModelId is not null || p.Value.MountPresetId is not null || p.Value.AttrPresetId is not null))
            .Select(p => new SensorPresetUsage(serverKey, document.ControllerId, p.Key, p.Value.ModelId, p.Value.MountPresetId, p.Value.AttrPresetId, labelOf?.Invoke(p.Key)))
            .OrderBy(u => u.ControllerId).ThenBy(u => u.SensorId)
            .ToList();
    }

    /// <summary>
    /// 저장된 쓰임 위에 지금 열린 제어기의 (저장 전) 자리를 덮는다 — 같은 서버 · 제어기 줄은 지금 것으로 바꾼다.
    /// </summary>
    public static IReadOnlyList<SensorPresetUsage> Overlay(IReadOnlyList<SensorPresetUsage>? stored, IReadOnlyList<SensorPresetUsage>? current)
    {
        var now = current ?? Array.Empty<SensorPresetUsage>();
        var keys = now.Select(u => (u.ServerKey, u.ControllerId)).ToHashSet();
        return (stored ?? Array.Empty<SensorPresetUsage>()).Where(u => !keys.Contains((u.ServerKey, u.ControllerId))).Concat(now)
            .OrderBy(u => u.ControllerId).ThenBy(u => u.SensorId).ToList();
    }

    public static IReadOnlyList<SensorPresetUsage> UsingMount(IEnumerable<SensorPresetUsage> usages, int presetId)
        => (usages ?? Enumerable.Empty<SensorPresetUsage>()).Where(u => u.MountPresetId == presetId).ToList();

    public static int CountMount(IEnumerable<SensorPresetUsage> usages, int presetId) => UsingMount(usages, presetId).Count;

    public static int CountAttribute(IEnumerable<SensorPresetUsage> usages, int presetId)
        => (usages ?? Enumerable.Empty<SensorPresetUsage>()).Count(u => u.AttrPresetId == presetId);

    public static int CountModel(IEnumerable<SensorPresetUsage> usages, int modelId)
        => (usages ?? Enumerable.Empty<SensorPresetUsage>()).Count(u => u.ModelId == modelId);

    /// <summary>W5 첫 줄 — "'기둥 꼭대기 2.6 m · 밖향' 을 쓰는 센서 4대가 새 자리로 옮겨집니다."</summary>
    public static string MoveMessage(string presetName, int count) => $"\"{presetName}\" 을 쓰는 센서 {count}대가 새 자리로 옮겨집니다.";
}
