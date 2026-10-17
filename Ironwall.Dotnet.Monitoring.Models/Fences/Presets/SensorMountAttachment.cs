using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ironwall.Dotnet.Monitoring.Models.Fences.Presets;

/// <summary>
/// 붙임 결과(sensor-mount-preset FR-22 · 불변식 "센서는 늘 실제 물체 하나에 붙는다") — 붙은 자세 · 붙은 물체 · 끌어 붙였나 · 그 까닭(쉬운 말).
/// </summary>
/// <param name="Pose">물체에 붙은 자세(가로 · 높이 · 펜스와 거리 · 각도가 모두 범위 안).</param>
/// <param name="Target">붙은 물체 갈래(기둥 · 상단 레일 · 메쉬 · 철조망 · 담 · 바닥).</param>
/// <param name="Clamped">요청한 자리가 허공이라 가장 가까운 붙는 자리로 끌어 붙였나.</param>
/// <param name="Reason">끌어 붙인 까닭 — "기둥 높이를 넘어 기둥 꼭대기에 붙였습니다". 그대로 붙었으면 <c>null</c>.</param>
public sealed record SensorMountAttachment(SensorMountPose Pose, SensorAnchorKind Target, bool Clamped, string? Reason);

/// <summary>
/// 칸 하나의 물체 면 하나 — 앞에서 본 모습(가로 m · 높이 m)의 선분 또는 사각형. 기둥은 세로 선, 상단 레일 · 담 위는 가로 선,
/// 메쉬 · 담 앞면 · 철조망 코일 · 바닥은 사각형이다.
/// </summary>
/// <param name="Anchor">이 면에 붙으면 쓰는 붙는 곳(기둥은 높이로 다시 고른다 — <see cref="SensorMountSurfaces.PostAnchorAt"/>).</param>
/// <param name="X0">가로 시작(m · 칸 안).</param>
/// <param name="X1">가로 끝(m).</param>
/// <param name="H0">높이 아래(m).</param>
/// <param name="H1">높이 위(m).</param>
/// <param name="Label">물체 이름(쉬운 말) — "기둥 A" · "메쉬" · "철조망 코일".</param>
public sealed record SensorMountSurface(SensorMountAnchor Anchor, double X0, double X1, double H0, double H1, string Label)
{
    /// <summary>붙는 곳 갈래.</summary>
    public SensorAnchorKind Kind => SensorMountResolver.KindOf(Anchor);

    /// <summary>(가로, 높이)를 이 면 위 가장 가까운 자리로.</summary>
    public (double X, double H) Project(double x, double h) => (Math.Clamp(x, X0, X1), Math.Clamp(h, H0, H1));

    /// <summary>(가로, 높이)에서 이 면까지 거리(m) — 면 위면 0.</summary>
    public double DistanceTo(double x, double h)
    {
        var (px, ph) = Project(x, h);
        return Math.Sqrt((px - x) * (px - x) + (ph - h) * (ph - h));
    }
}

/// <summary>
/// 담장 한 칸의 물체 면들 — <b>순수 함수</b>(sensor-mount-preset FR-22). 붙는 점(<see cref="SensorSnapPoints"/>)은 이 면들 위의 이름 붙은 점이고,
/// 자유 위치(Alt · 화살표 · 수치 칸 · 따로 고친 값)는 이 면 위에서만 움직인다.
/// </summary>
/// <remarks>
/// 철망 칸: 기둥 A · B(세로 선 · 0.05 m ~ 꼭대기 — 철조망 칸이면 Y 받침 · 코일 꼭대기까지) · 상단 레일(높이 H 가로 선) · 메쉬(0.05 m ~ H 사각형) ·
/// 철조망 칸이면 코일(코일 아래 ~ 꼭대기 사각형). 담 칸: 담 위(높이 H 가로 선) · 담 앞면(0.05 m ~ H). 어느 칸이든 바닥(땅속 0 ~ −2 m).
/// </remarks>
public static class SensorMountSurfaces
{
    /// <summary>면 위로 보는 허용 오차(m) — 반올림(몫 4자리 · 높이 1 mm) 때문에 생기는 차이.</summary>
    public const double EPS_M = 0.002;

    /// <summary>칸의 모든 물체 면(차례 = 겹칠 때 먼저 고르는 차례: 기둥 → 레일 → 메쉬 → 코일 → 담 → 바닥).</summary>
    public static IReadOnlyList<SensorMountSurface> For(FenceSampleBay bay)
    {
        ArgumentNullException.ThrowIfNull(bay);
        var b = bay.Normalized();
        var w = b.PanelWidthM;
        var h = b.FenceHeightM;
        var min = SensorMountResolver.MIN_ABOVE_GROUND_M;
        var list = new List<SensorMountSurface>(8);
        if (b.IsWall)
        {
            list.Add(new(SensorMountAnchor.WallTop, 0, w, h, h, "담 위"));
            list.Add(new(SensorMountAnchor.WallFace, 0, w, min, h, "담 앞면"));
        }
        else
        {
            list.Add(new(SensorMountAnchor.PostMid, 0, 0, min, b.TopLimitM, "기둥 A"));
            list.Add(new(SensorMountAnchor.PostMid, w, w, min, b.TopLimitM, "기둥 B"));
            list.Add(new(SensorMountAnchor.Rail, 0, w, h, h, "상단 레일"));
            list.Add(new(SensorMountAnchor.Mesh, 0, w, min, h, "메쉬"));
            if (b.HasRazor) list.Add(new(SensorMountAnchor.Razor, 0, w, CoilBottomM(b), b.CoilTopM, "철조망 코일"));
        }
        list.Add(new(SensorMountAnchor.Ground, 0, w, SensorMountResolver.MIN_BURIED_M, 0, "바닥"));
        return list;
    }

    /// <summary>코일 아래 높이(m) — 코일 가운데 − 반지름(Y 받침 위 · 2.5D 그림과 같은 식).</summary>
    public static double CoilBottomM(FenceSampleBay bay)
    {
        var b = bay.Normalized();
        return Math.Max(b.FenceHeightM, b.CoilCenterM - b.CoilRadiusM);
    }

    /// <summary>붙는 곳 <paramref name="anchor"/> 갈래의 면(그 칸에 없으면 빈 목록 — 예: 철조망 없는 칸의 코일 · 담 칸의 기둥).</summary>
    public static IReadOnlyList<SensorMountSurface> OfKind(FenceSampleBay bay, SensorMountAnchor anchor)
    {
        var all = For(bay);
        return anchor switch
        {
            SensorMountAnchor.WallTop => all.Where(s => s.Anchor == SensorMountAnchor.WallTop).ToList(),
            SensorMountAnchor.WallFace => all.Where(s => s.Anchor == SensorMountAnchor.WallFace).ToList(),
            _ => all.Where(s => s.Kind == SensorMountResolver.KindOf(anchor) && s.Anchor is not (SensorMountAnchor.WallTop or SensorMountAnchor.WallFace)).ToList(),
        };
    }

    /// <summary>기둥에 붙은 높이 → 기둥 붙는 곳 이름(꼭대기 · 레일 높이 · 중간 · 아래 중 가장 가까운 것).</summary>
    public static SensorMountAnchor PostAnchorAt(FenceSampleBay bay, double heightM)
    {
        var b = bay.Normalized();
        var h = b.FenceHeightM;
        var marks = new[]
        {
            (SensorMountAnchor.PostTop, b.PostTopM), (SensorMountAnchor.PostRail, h),
            (SensorMountAnchor.PostMid, h * FenceSampleBay.MID_RATIO), (SensorMountAnchor.PostLow, h * FenceSampleBay.LOW_RATIO),
        };
        if (heightM >= b.PostTopM - EPS_M) return SensorMountAnchor.PostTop;
        return marks.OrderBy(m => Math.Abs(m.Item2 - heightM)).First().Item1;
    }

    /// <summary>
    /// (가로 m · 높이 m)에서 가장 가까운 물체 면 — <paramref name="buried"/> 면 바닥만, 아니면 바닥을 뺀 면에서(지중 ↔ 지상은 바꾸지 않는다 · v1.1 결정).
    /// <paramref name="prefer"/> 갈래의 면이 그 자리를 품으면 그 면(같은 자리를 기둥 · 메쉬가 함께 품을 때 지금 갈래를 지킨다).
    /// </summary>
    public static SensorMountSurface Nearest(FenceSampleBay bay, double xM, double heightM, bool buried, SensorMountAnchor? prefer = null)
    {
        var all = For(bay).Where(s => (s.Kind == SensorAnchorKind.Ground) == buried).ToList();
        if (prefer is { } p)
        {
            var own = all.Where(s => SameKind(s.Anchor, p)).FirstOrDefault(s => s.DistanceTo(xM, heightM) <= EPS_M);
            if (own is not null) return own;
        }
        SensorMountSurface best = all[0];
        var d = double.PositiveInfinity;
        foreach (var s in all)
        {
            var sd = s.DistanceTo(xM, heightM);
            if (sd < d - 1e-9) { d = sd; best = s; }
        }
        return best;
    }

    /// <summary>같은 물체 갈래인가(담 위 · 담 앞면은 서로 다른 물체 면).</summary>
    internal static bool SameKind(SensorMountAnchor a, SensorMountAnchor b)
        => a is SensorMountAnchor.WallTop or SensorMountAnchor.WallFace || b is SensorMountAnchor.WallTop or SensorMountAnchor.WallFace
            ? a == b
            : SensorMountResolver.KindOf(a) == SensorMountResolver.KindOf(b);

    /// <summary>
    /// 자세가 물체에서 떨어진 거리(m) — 붙은 갈래 물체 면까지 + (지상이면) 펜스와 거리가 브래킷을 넘는 만큼. 붙어 있으면 0.
    /// 그 갈래 물체가 칸에 없으면 무한대(예: 철조망 없는 칸에 철조망 갈래).
    /// </summary>
    public static double GapToObject(SensorMountPose pose, FenceSampleBay bay)
    {
        ArgumentNullException.ThrowIfNull(pose);
        var b = bay.Normalized();
        var surfaces = OfKind(b, pose.Anchor);
        if (surfaces.Count == 0) return double.PositiveInfinity;
        var x = pose.AlongRatio * b.PanelWidthM;
        var gap = surfaces.Min(s => s.DistanceTo(x, pose.HeightM));
        if (pose.Anchor != SensorMountAnchor.Ground) gap += Math.Max(0, Math.Abs(pose.DepthM) - pose.BracketM);
        return gap <= EPS_M ? 0 : gap;
    }

    /// <summary>물체에 붙어 있나(<see cref="GapToObject"/> = 0).</summary>
    public static bool IsAttached(SensorMountPose pose, FenceSampleBay bay) => GapToObject(pose, bay) == 0;

    internal static string M(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
}
