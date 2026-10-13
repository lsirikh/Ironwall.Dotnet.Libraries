using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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

    /// <summary>프리셋 + 따로 고친 값을 칸 <paramref name="bay"/> 에서 계산한다(물체에 붙은 자세 — <see cref="Attach"/>).</summary>
    public static SensorMountPose Resolve(SensorMountPreset preset, SensorMountOverride? overrides, FenceSampleBay bay)
        => ResolveAttached(preset, overrides, bay).Pose;

    /// <summary>
    /// <see cref="Resolve"/> 와 같되 붙임 결과(붙은 물체 · 끌어 붙였나 · 까닭)까지 — 결선맵 상태 줄 · 편집기 · 저장 보정이 쓴다(sensor-mount-preset FR-22).
    /// </summary>
    public static SensorMountAttachment ResolveAttached(SensorMountPreset preset, SensorMountOverride? overrides, FenceSampleBay bay)
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
        return Attach(new SensorMountPose(anchor, along, 0, height, depth,
                                          overrides?.YawDeg ?? preset.YawDeg, overrides?.TiltDeg ?? preset.TiltDeg, overrides?.BracketM ?? preset.BracketM), b);
    }

    /// <summary>
    /// 자세를 칸 범위 안으로 — 물체에 붙인다(<see cref="Attach"/>) · 방향 −180~180 · 각도 ±90 · 브래킷 0~2 m.
    /// </summary>
    public static SensorMountPose Clamp(SensorMountPose pose, FenceSampleBay bay) => Attach(pose, bay).Pose;

    /// <summary>
    /// <b>불변식</b>(sensor-mount-preset FR-22) — 센서의 최종 자리는 늘 실제 물체 하나(기둥 · 상단 레일 · 메쉬 · 철조망 코일 · 담 위/앞면 · 바닥)에 붙는다.
    /// <list type="number">
    /// <item>칸에 없는 갈래(철조망 없는 칸의 코일 · 담 칸의 기둥 · 철망 칸의 담)는 그 칸의 물체로 바꾼다.</item>
    /// <item>붙은 갈래 물체 면 위면 그대로. 아니면 그 자리를 품은 다른 물체(지중 ↔ 지상은 바꾸지 않는다)로 갈래를 바꾸고,
    /// 어느 물체도 품지 않는 허공이면 붙은 갈래 물체 면 위 가장 가까운 자리로 끌어 붙인다(<see cref="SensorMountAttachment.Clamped"/> · 까닭).</item>
    /// <item>지상 센서의 펜스와 거리는 브래킷 길이 안(브래킷이 물체까지 닿는다). 지중은 ±5 m.</item>
    /// </list>
    /// </summary>
    public static SensorMountAttachment Attach(SensorMountPose pose, FenceSampleBay bay)
    {
        ArgumentNullException.ThrowIfNull(pose);
        ArgumentNullException.ThrowIfNull(bay);
        var b = bay.Normalized();
        var w = b.PanelWidthM;
        var x = Math.Clamp(Finite(pose.AlongRatio, 0.5), 0, 1) * w;
        var h = Finite(pose.HeightM, 0);
        var reasons = new List<string>();
        var clamped = false;

        var anchor = Substitute(pose.Anchor, b, h, out var swapReason);
        if (swapReason is not null) { reasons.Add(swapReason); clamped = true; }

        var own = SensorMountSurfaces.OfKind(b, anchor);
        var ownBest = own.OrderBy(s => s.DistanceTo(x, h)).First();
        double fx, fh;
        if (ownBest.DistanceTo(x, h) <= SensorMountSurfaces.EPS_M)
        {
            (fx, fh) = ownBest.Project(x, h);
        }
        else
        {
            var buried = anchor == SensorMountAnchor.Ground;
            var holder = SensorMountSurfaces.For(b).Where(s => (s.Kind == SensorAnchorKind.Ground) == buried)
                                                  .FirstOrDefault(s => s.DistanceTo(x, h) <= SensorMountSurfaces.EPS_M);
            if (holder is not null)
            {
                // 다른 물체가 그 자리를 품는다(예: 기둥 프리셋을 칸 가운데로 → 메쉬) — 갈래만 바꾼다(자리는 그대로).
                anchor = holder.Kind == SensorAnchorKind.Post ? SensorMountSurfaces.PostAnchorAt(b, h) : holder.Anchor;
                (fx, fh) = holder.Project(x, h);
            }
            else
            {
                (fx, fh) = ownBest.Project(x, h);
                reasons.Add(ClampReason(ownBest, b, x, h, fx, fh));
                clamped = true;
            }
        }

        var bracket = Math.Clamp(Finite(pose.BracketM, 0), 0, MAX_BRACKET_M);
        var depth = Finite(pose.DepthM, 0);
        if (anchor == SensorMountAnchor.Ground)
        {
            depth = Math.Clamp(depth, -MAX_DEPTH_M, MAX_DEPTH_M);
        }
        else if (Math.Abs(depth) > bracket + 1e-9)
        {
            // 펜스에서 떨어진 거리 = 브래킷이 닿는 만큼까지(그 너머는 허공)
            reasons.Add(bracket < 0.005
                ? "브래킷이 없어 펜스 면에 붙였습니다 — 떨어뜨리려면 브래킷 길이를 늘리세요"
                : $"브래킷 길이({bracket.ToString("0.0#", CultureInfo.InvariantCulture)} m)보다 멀리 둘 수 없어 브래킷 끝에 붙였습니다");
            depth = Math.Sign(depth) * bracket;
            clamped = true;
        }

        var along = Math.Round(w > 0 ? fx / w : 0.5, 4);
        var result = pose with
        {
            Anchor = anchor,
            AlongRatio = along,
            AlongM = Math.Round(along * w, 3),                               // 반올림한 몫에서 — 다시 붙여도 같은 값(멱등)
            HeightM = Math.Round(fh, 3),
            DepthM = Math.Round(depth, 3),
            YawDeg = NormalizeYaw(pose.YawDeg),
            TiltDeg = Math.Round(Math.Clamp(Finite(pose.TiltDeg, 0), -90, 90), 1),
            BracketM = Math.Round(bracket, 3),
        };
        return new SensorMountAttachment(result, KindOf(anchor), clamped, reasons.Count == 0 ? null : string.Join(" · ", reasons));
    }

    /// <summary>
    /// 프리셋의 날 값(가로 · 높이 · 거리 · 브래킷 · 샘플)을 물체에 붙은 값으로 맞춘다 — 저장 직전 · 편집기를 열 때(옛 값 보정). 붙는 점 이름은 그 칸에 있는 점이면 남긴다.
    /// 바뀐 것이 없으면 <see cref="SensorMountAttachment.Clamped"/> 거짓.
    /// </summary>
    public static (SensorMountPreset Preset, SensorMountAttachment Attachment) Normalize(SensorMountPreset preset, FenceSampleBay bay)
    {
        ArgumentNullException.ThrowIfNull(preset);
        var b = bay.Normalized();
        var att = ResolveAttached(preset, null, b);
        var pose = att.Pose;
        if (!string.IsNullOrEmpty(preset.SnapPoint))
        {
            // 붙는 점에 붙은 프리셋 — 점은 늘 물체 위라 점 이름 · 자리는 두고(칸마다 다시 계산) 펜스와 거리 · 브래킷만 맞춘다
            var kept = preset with { DepthM = pose.DepthM, BracketM = pose.BracketM };
            return (kept, att);
        }
        var next = preset with
        {
            Anchor = pose.Anchor,
            AlongRatio = pose.AlongRatio,
            HeightM = pose.HeightM,
            DepthM = pose.DepthM,
            YawDeg = pose.YawDeg,
            TiltDeg = pose.TiltDeg,
            BracketM = pose.BracketM,
            PanelWidthM = b.PanelWidthM,
            FenceHeightM = b.FenceHeightM,
            SampleHasRazor = b.HasRazor,
        };
        return (next, att);
    }

    /// <summary>방향을 −180~180(°)으로.</summary>
    public static double NormalizeYaw(double degrees)
    {
        // 반올림을 먼저 — −179.96 이 −180.0 으로 굳어 다시 정규화하면 180 이 되던 흔들림을 막는다(멱등)
        var d = Math.Round(Finite(degrees, 90) % 360, 1);
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
        => MoveFreeAttached(preset, bay, alongM, heightM, depthM).Preset;

    /// <summary>
    /// <see cref="MoveFree"/> 와 같되 붙임 결과까지 — Alt 자유 위치도 <b>물체 면 위에서만</b> 자유다(1 cm 단위 · 허공이면 가장 가까운 물체 면으로 끌어 붙이고 까닭).
    /// 붙는 곳은 그 자리에서 가장 가까운 물체(지중 ↔ 지상은 높이 부호로 — 땅 아래면 바닥).
    /// </summary>
    public static (SensorMountPreset Preset, SensorMountAttachment Attachment) MoveFreeAttached(SensorMountPreset preset, FenceSampleBay bay,
                                                                                             double? alongM = null, double? heightM = null, double? depthM = null)
    {
        ArgumentNullException.ThrowIfNull(preset);
        var b = bay.Normalized();
        var current = Resolve(preset, null, b);
        var x = Round01(Finite(alongM ?? current.AlongM, current.AlongM));
        var h = Round01(Finite(heightM ?? current.HeightM, current.HeightM));
        var z = Round01(Finite(depthM ?? current.DepthM, current.DepthM));
        var surface = SensorMountSurfaces.Nearest(b, Math.Clamp(x, 0, b.PanelWidthM), h, buried: h < 0, prefer: current.Anchor);
        var (_, ph) = surface.Project(Math.Clamp(x, 0, b.PanelWidthM), h);
        var anchor = surface.Kind == SensorAnchorKind.Post ? SensorMountSurfaces.PostAnchorAt(b, ph)
                   : SensorMountSurfaces.SameKind(surface.Anchor, current.Anchor) ? current.Anchor : surface.Anchor;
        var att = Attach(current with { Anchor = anchor, AlongRatio = x / b.PanelWidthM, HeightM = h, DepthM = z }, b);
        var pose = att.Pose;
        var moved = preset with
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
        return (moved, att);

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

    /// <summary>
    /// 칸에 없는 물체 갈래를 그 칸의 물체로 — 담 칸: 기둥 · 레일 · 메쉬 · 철조망 → 담(높이 H 이상이면 담 위, 아니면 담 앞면) · 철망 칸: 담 위 → 상단 레일 · 담 앞면 → 메쉬 ·
    /// 철조망 없는 칸: 철조망 → 상단 레일. 바꿨으면 <paramref name="reason"/> 에 까닭.
    /// </summary>
    private static SensorMountAnchor Substitute(SensorMountAnchor anchor, FenceSampleBay bay, double heightM, out string? reason)
    {
        reason = null;
        if (anchor == SensorMountAnchor.Ground) return anchor;
        if (bay.IsWall)
        {
            if (anchor is SensorMountAnchor.WallTop or SensorMountAnchor.WallFace) return anchor;
            var wall = heightM >= bay.FenceHeightM - SensorMountSurfaces.EPS_M ? SensorMountAnchor.WallTop : SensorMountAnchor.WallFace;
            reason = $"담 칸이라 {AnchorText(wall)}에 붙였습니다";
            return wall;
        }
        switch (anchor)
        {
            case SensorMountAnchor.WallTop:
                reason = "담이 없는 칸이라 상단 레일에 붙였습니다";
                return SensorMountAnchor.Rail;
            case SensorMountAnchor.WallFace:
                reason = "담이 없는 칸이라 메쉬에 붙였습니다";
                return SensorMountAnchor.Mesh;
            case SensorMountAnchor.Razor or SensorMountAnchor.RazorLoop when !bay.HasRazor:
                reason = "철조망이 없는 칸이라 상단 레일에 붙였습니다";
                return SensorMountAnchor.Rail;
            default:
                return anchor;
        }
    }

    /// <summary>허공 자리를 물체 면으로 끌어 붙인 까닭(쉬운 말).</summary>
    private static string ClampReason(SensorMountSurface surface, FenceSampleBay bay, double x, double h, double fx, double fh)
    {
        var up = h > fh + SensorMountSurfaces.EPS_M;
        var down = h < fh - SensorMountSurfaces.EPS_M;
        var side = Math.Abs(x - fx) > SensorMountSurfaces.EPS_M;
        var at = $"{fh.ToString("0.00", CultureInfo.InvariantCulture)} m";
        return surface.Kind switch
        {
            SensorAnchorKind.Post when side => $"기둥이 없는 자리라 가까운 {surface.Label}에 붙였습니다({at})",
            SensorAnchorKind.Post when up => bay.HasRazor ? $"기둥 · 철조망 꼭대기를 넘어 꼭대기에 붙였습니다({at})" : $"기둥 높이를 넘어 기둥 꼭대기에 붙였습니다({at})",
            SensorAnchorKind.Rail => $"상단 레일 높이({at})에 붙였습니다",
            SensorAnchorKind.Mesh when up => $"망 높이를 넘어 망 꼭대기에 붙였습니다({at})",
            SensorAnchorKind.Razor => $"철조망 코일 밖이라 코일에 붙였습니다({at})",
            SensorAnchorKind.Wall when surface.Anchor == SensorMountAnchor.WallTop => $"담 위({at})에 붙였습니다",
            SensorAnchorKind.Wall when up => $"담 높이를 넘어 담 꼭대기에 붙였습니다({at})",
            SensorAnchorKind.Ground when up => "지중 센서는 땅 위로 올라올 수 없어 지면에 붙였습니다",
            SensorAnchorKind.Ground => $"땅속 {Math.Abs(MIN_BURIED_M):0} m 보다 깊이 둘 수 없어 그 깊이에 붙였습니다",
            _ when down => $"땅에 닿지 않게 가장 낮은 자리({at})에 붙였습니다",
            _ => $"{surface.Label}에 붙였습니다({at})",
        };
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
