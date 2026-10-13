using System;
using System.Globalization;

namespace Ironwall.Dotnet.Monitoring.Models.Fences.Presets;

/// <summary>
/// 계산된 설치 자세 — 칸 안 가로(몫 · m) · 높이 · 펜스와 거리 · 바라보는 방향 · 위아래 각도.
/// 편집기 세 그림 · 3D 미리보기 · 결선맵 펜스 보기가 모두 이 값을 그린다(sensor-mount-preset FR-10 · FR-17).
/// </summary>
public sealed record SensorMountPose(SensorMountAnchor Anchor, double AlongRatio, double AlongM, double HeightM, double DepthM, double YawDeg, double TiltDeg, double BracketM)
{
    /// <summary>땅속인가.</summary>
    public bool IsBuried => HeightM < 0;
}

/// <summary>
/// 위치 프리셋 + 따로 고친 값 → 자세(sensor-mount-preset §7) — <b>순수 함수</b>. 편집기와 결선맵이 같은 계산을 써서
/// "편집기에선 되는데 결선맵에선 뜬다"를 막는다. 높이는 늘 바닥 ~ 구조물 꼭대기(<see cref="FenceSampleBay.TopLimitM"/>) 안이다.
/// </summary>
public static class SensorMountResolver
{
    public const double MIN_ABOVE_GROUND_M = 0.05;
    public const double MIN_BURIED_M = -2.0;
    public const double MAX_DEPTH_M = 5.0;
    public const double MAX_BRACKET_M = 2.0;

    /// <summary>방향 손잡이 단위(°) — Alt 는 1°.</summary>
    public const double YAW_STEP_DEG = 15;

    /// <summary>화살표 한 번(m) — Shift 는 1 cm.</summary>
    public const double NUDGE_M = 0.10;
    public const double FINE_NUDGE_M = 0.01;

    /// <summary>프리셋 + 따로 고친 값을 칸 <paramref name="bay"/> 에서 계산한다.</summary>
    public static SensorMountPose Resolve(SensorMountPreset preset, SensorMountOverride? overrides, FenceSampleBay bay)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(bay);
        var b = bay.Normalized();
        // 붙는 점에 붙은 프리셋은 칸 높이에 맞춰 다시 계산한다(예: 기둥 중간 = 어떤 펜스 높이에서도 0.75 H). 칸에 그 점이 없으면(철조망 없는 칸의 razor) 같은 갈래의 대체 점.
        var snap = SensorSnapPoints.Find(b, preset.SnapPoint) ?? Fallback(b, preset);
        var along = overrides?.AlongRatio ?? snap?.AlongRatio ?? preset.AlongRatio;
        var height = overrides?.HeightM ?? snap?.HeightM ?? ScaleHeight(preset, b);
        var depth = overrides?.DepthM ?? preset.DepthM;
        var anchor = snap?.Anchor ?? preset.Anchor;
        return Clamp(new SensorMountPose(anchor, along, 0, height, depth,
                                         overrides?.YawDeg ?? preset.YawDeg, overrides?.TiltDeg ?? preset.TiltDeg, overrides?.BracketM ?? preset.BracketM), b);
    }

    /// <summary>자세를 칸 범위 안으로 — 가로 0~1 · 높이(지중 −2~0 / 그 밖 0.05~꼭대기) · 거리 ±5 · 방향 −180~180 · 각도 ±90.</summary>
    public static SensorMountPose Clamp(SensorMountPose pose, FenceSampleBay bay)
    {
        var b = bay.Normalized();
        var along = Math.Clamp(Finite(pose.AlongRatio, 0.5), 0, 1);
        var height = Finite(pose.HeightM, 0);
        height = pose.Anchor == SensorMountAnchor.Ground
            ? Math.Clamp(height, MIN_BURIED_M, 0)
            : Math.Clamp(height, MIN_ABOVE_GROUND_M, b.TopLimitM);
        return pose with
        {
            AlongRatio = Math.Round(along, 4),
            AlongM = Math.Round(along * b.PanelWidthM, 3),
            HeightM = Math.Round(height, 3),
            DepthM = Math.Round(Math.Clamp(Finite(pose.DepthM, 0), -MAX_DEPTH_M, MAX_DEPTH_M), 3),
            YawDeg = NormalizeYaw(pose.YawDeg),
            TiltDeg = Math.Round(Math.Clamp(Finite(pose.TiltDeg, 0), -90, 90), 1),
            BracketM = Math.Round(Math.Clamp(Finite(pose.BracketM, 0), 0, MAX_BRACKET_M), 3),
        };
    }

    /// <summary>방향을 −180~180(°)으로.</summary>
    public static double NormalizeYaw(double degrees)
    {
        var d = Finite(degrees, 90) % 360;
        if (d > 180) d -= 360;
        if (d <= -180) d += 360;
        return Math.Round(d, 1);
    }

    /// <summary>방향 손잡이 — 15° 단위로 맞춘다(<paramref name="fine"/> = Alt 면 1°).</summary>
    public static double SnapYaw(double degrees, bool fine)
    {
        var step = fine ? 1 : YAW_STEP_DEG;
        return NormalizeYaw(Math.Round(Finite(degrees, 90) / step) * step);
    }

    /// <summary>위에서 본 손잡이 끝(펜스 따라 +x · 바깥쪽 −y)이 센서 중심에서 (dx, dy) 일 때 방향(°) — 0 = 펜스 따라 · 90 = 바깥쪽 정면 · −90 = 안쪽 정면.</summary>
    public static double YawFromHandle(double dxAlong, double dyOutward, bool fine)
        => SnapYaw(Math.Atan2(dyOutward, dxAlong) * 180 / Math.PI, fine);

    /// <summary>바로가기 방향.</summary>
    public const double YAW_ALONG = 0, YAW_OUTWARD = 90, YAW_INWARD = -90;

    /// <summary>
    /// 붙는 점에 붙인 프리셋(놓기 · Tab) — 자리 값은 점에서, 방향 · 각도 · 브래킷은 그대로. 지중 점이면 펜스와 거리도 점에서.
    /// </summary>
    public static SensorMountPreset SnapTo(SensorMountPreset preset, SensorSnapPoint point, FenceSampleBay bay)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(point);
        var b = bay.Normalized();
        return preset with
        {
            Anchor = point.Anchor,
            SnapPoint = point.Name,
            AlongRatio = point.AlongRatio,
            HeightM = Math.Round(point.HeightM, 3),
            DepthM = point.Kind == SensorAnchorKind.Ground ? point.DepthM : preset.Anchor == SensorMountAnchor.Ground ? 0 : preset.DepthM,
            PanelWidthM = b.PanelWidthM,
            FenceHeightM = b.FenceHeightM,
            SampleHasRazor = b.HasRazor,
        };
    }

    /// <summary>
    /// 자유 위치(Alt 끌기 · 화살표 · 수치 칸) — 붙는 점을 떼고 값을 1 cm 단위로 맞춰 칸 안으로. 붙는 곳은 가장 가까운 점의 것을 따른다.
    /// </summary>
    public static SensorMountPreset MoveFree(SensorMountPreset preset, FenceSampleBay bay, double? alongM = null, double? heightM = null, double? depthM = null)
    {
        ArgumentNullException.ThrowIfNull(preset);
        var b = bay.Normalized();
        var current = Resolve(preset, null, b);
        var x = alongM ?? current.AlongM;
        var h = heightM ?? current.HeightM;
        var z = depthM ?? current.DepthM;
        var nearest = SensorSnapPoints.Nearest(b, null, x, h, z);
        var anchor = h < 0 ? SensorMountAnchor.Ground : nearest is { Kind: not SensorAnchorKind.Ground } n ? n.Anchor : preset.Anchor == SensorMountAnchor.Ground ? SensorMountAnchor.Mesh : preset.Anchor;
        var pose = Clamp(current with { Anchor = anchor, AlongRatio = Round01(x) / b.PanelWidthM, HeightM = Round01(h), DepthM = Round01(z) }, b);
        return preset with
        {
            Anchor = pose.Anchor,
            SnapPoint = null,
            AlongRatio = pose.AlongRatio,
            HeightM = pose.HeightM,
            DepthM = pose.DepthM,
            PanelWidthM = b.PanelWidthM,
            FenceHeightM = b.FenceHeightM,
            SampleHasRazor = b.HasRazor,
        };

        static double Round01(double v) => Math.Round(v * 100) / 100;
    }

    /// <summary>화살표 이동 — 앞에서 본 모습 기준 가로(←→) · 높이(↑↓) · <paramref name="fine"/>(Shift)면 1 cm.</summary>
    public static SensorMountPreset Nudge(SensorMountPreset preset, FenceSampleBay bay, int dxSteps, int dySteps, bool fine)
    {
        var b = bay.Normalized();
        var pose = Resolve(preset, null, b);
        var step = fine ? FINE_NUDGE_M : NUDGE_M;
        return MoveFree(preset, b, pose.AlongM + dxSteps * step, pose.HeightM + dySteps * step, pose.DepthM);
    }

    /// <summary>
    /// 따로 고친 값 — 센서 자세와 프리셋 자세의 차이(1 cm · 0.5° 넘는 칸만). 차이가 없으면 <c>null</c>.
    /// </summary>
    public static SensorMountOverride? Diff(SensorMountPreset preset, SensorMountPose actual, FenceSampleBay bay)
    {
        var basePose = Resolve(preset, null, bay);
        var o = new SensorMountOverride
        {
            AlongRatio = Math.Abs(actual.AlongM - basePose.AlongM) >= 0.01 ? actual.AlongRatio : null,
            HeightM = Math.Abs(actual.HeightM - basePose.HeightM) >= 0.01 ? actual.HeightM : null,
            DepthM = Math.Abs(actual.DepthM - basePose.DepthM) >= 0.01 ? actual.DepthM : null,
            YawDeg = Math.Abs(NormalizeYaw(actual.YawDeg - basePose.YawDeg)) >= 0.5 ? actual.YawDeg : null,
            TiltDeg = Math.Abs(actual.TiltDeg - basePose.TiltDeg) >= 0.5 ? actual.TiltDeg : null,
            BracketM = Math.Abs(actual.BracketM - basePose.BracketM) >= 0.01 ? actual.BracketM : null,
        };
        return SensorMountOverride.OrNull(o);
    }

    /// <summary>
    /// 자리를 말하는 이름 제안 — "기둥 중간 1.8 m · 밖향" · "철조망 고리 · 기둥 B 쪽 2.9 m" · "바닥 · 안 0.7 m" · 아래로 기울면 " · 아래로 10°".
    /// </summary>
    public static string SuggestName(SensorMountPreset preset, FenceSampleBay bay)
    {
        ArgumentNullException.ThrowIfNull(preset);
        var b = bay.Normalized();
        var pose = Resolve(preset, null, b);
        var where = AnchorText(pose.Anchor);
        string text;
        if (pose.Anchor == SensorMountAnchor.Ground)
            text = $"{where} · {(pose.DepthM >= 0 ? "안" : "밖")} {Math.Abs(pose.DepthM).ToString("0.0", CultureInfo.InvariantCulture)} m";
        else
            text = $"{where} {pose.HeightM.ToString("0.0", CultureInfo.InvariantCulture)} m · {FacingText(pose.YawDeg)}";
        if (pose.TiltDeg <= -1) text += $" · 아래로 {Math.Abs(pose.TiltDeg):0}°";
        else if (pose.TiltDeg >= 1) text += $" · 위로 {pose.TiltDeg:0}°";
        if (pose.BracketM >= 0.01 && pose.Anchor != SensorMountAnchor.Ground) text += $" · 브래킷 {pose.BracketM.ToString("0.0#", CultureInfo.InvariantCulture)} m";
        return text;
    }

    /// <summary>붙는 곳 이름(쉬운 말).</summary>
    public static string AnchorText(SensorMountAnchor anchor) => anchor switch
    {
        SensorMountAnchor.PostTop => "기둥 꼭대기",
        SensorMountAnchor.PostRail => "기둥 레일 높이",
        SensorMountAnchor.PostMid => "기둥 중간",
        SensorMountAnchor.PostLow => "기둥 아래",
        SensorMountAnchor.Rail => "상단 레일",
        SensorMountAnchor.Mesh => "메쉬",
        SensorMountAnchor.Razor => "철조망 위",
        SensorMountAnchor.RazorLoop => "철조망 고리",
        SensorMountAnchor.WallTop => "담 위",
        SensorMountAnchor.WallFace => "담 앞면",
        _ => "바닥",
    };

    /// <summary>붙는 곳의 갈래.</summary>
    public static SensorAnchorKind KindOf(SensorMountAnchor anchor) => anchor switch
    {
        SensorMountAnchor.PostTop or SensorMountAnchor.PostRail or SensorMountAnchor.PostMid or SensorMountAnchor.PostLow => SensorAnchorKind.Post,
        SensorMountAnchor.Rail => SensorAnchorKind.Rail,
        SensorMountAnchor.Mesh => SensorAnchorKind.Mesh,
        SensorMountAnchor.Razor or SensorMountAnchor.RazorLoop => SensorAnchorKind.Razor,
        SensorMountAnchor.WallTop or SensorMountAnchor.WallFace => SensorAnchorKind.Wall,
        _ => SensorAnchorKind.Ground,
    };

    /// <summary>바라보는 쪽 — 밖향 · 안향 · 펜스 따라.</summary>
    public static string FacingText(double yawDeg)
    {
        var y = NormalizeYaw(yawDeg);
        if (y >= 30 && y <= 150) return "밖향";
        if (y <= -30 && y >= -150) return "안향";
        return "펜스 따라";
    }

    #region - helpers -
    /// <summary>
    /// 칸에 없는 붙는 점(예: 철조망 없는 칸의 razor · 담 칸의 기둥 점)의 대체 — 철조망 → 상단 레일 가운데, 기둥 점 → 담이면 담 위, 담 점 → 메쉬 중간.
    /// 붙는 점이 없던 프리셋(자유 위치)이면 <c>null</c>.
    /// </summary>
    private static SensorSnapPoint? Fallback(FenceSampleBay bay, SensorMountPreset preset)
    {
        if (string.IsNullOrEmpty(preset.SnapPoint)) return null;
        var name = KindOf(preset.Anchor) switch
        {
            SensorAnchorKind.Razor => bay.IsWall ? "wall.top" : "mesh.rail",
            SensorAnchorKind.Post or SensorAnchorKind.Rail => bay.IsWall ? "wall.top" : null,
            SensorAnchorKind.Mesh => bay.IsWall ? "wall.face" : null,
            SensorAnchorKind.Wall => bay.IsWall ? null : preset.Anchor == SensorMountAnchor.WallTop ? "mesh.rail" : "mesh.mid",
            _ => null,
        };
        return SensorSnapPoints.Find(bay, name);
    }

    /// <summary>자유 위치 프리셋의 높이를 정한 샘플 펜스 높이 → 이 칸 높이로 비례(지중은 그대로).</summary>
    private static double ScaleHeight(SensorMountPreset preset, FenceSampleBay bay)
    {
        if (preset.Anchor == SensorMountAnchor.Ground || preset.HeightM <= 0) return preset.HeightM;
        var sample = preset.FenceHeightM > 0 && double.IsFinite(preset.FenceHeightM) ? preset.FenceHeightM : bay.FenceHeightM;
        return preset.HeightM * bay.FenceHeightM / sample;
    }

    private static double Finite(double v, double fallback) => double.IsFinite(v) ? v : fallback;
    #endregion
}
