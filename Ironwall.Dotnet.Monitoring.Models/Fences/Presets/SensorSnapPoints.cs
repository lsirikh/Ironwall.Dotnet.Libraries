using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Monitoring.Models.Fences.Presets;

/// <summary>
/// 담장 한 칸 샘플 — 기둥 둘 + 상단 레일 + 메쉬(또는 담) · 철조망 유무(sensor-mount-preset FR-04).
/// </summary>
/// <param name="PanelWidthM">칸 폭(m).</param>
/// <param name="FenceHeightM">펜스 높이 = 상단 레일 높이(m).</param>
/// <param name="HasRazor">윤형 철조망(상단 레일 위 코일)이 있는가.</param>
/// <param name="IsWall">담(벽돌 · 시멘트)인가 — 기둥 · 메쉬 대신 담 점.</param>
public sealed record FenceSampleBay(double PanelWidthM = 3.0, double FenceHeightM = 2.4, bool HasRazor = false, bool IsWall = false)
{
    /// <summary>기둥 꼭대기가 상단 레일 위로 솟은 높이(m · 스케치 1 기둥 머리 · 스토리보드 "기둥 상단 2.6 m" = 펜스 2.4 + 0.2).</summary>
    public const double POST_CAP_M = 0.2;

    /// <summary>기둥 중간 점 높이(펜스 높이의 몫 · 스케치 1 실측).</summary>
    public const double MID_RATIO = 0.75;

    /// <summary>기둥 · 메쉬 아래 점 높이(펜스 높이의 몫 · 스케치 1 실측).</summary>
    public const double LOW_RATIO = 0.5;

    /// <summary>지중 점 깊이(m · 땅속) · 펜스에서 떨어진 거리(m).</summary>
    public const double GROUND_DEPTH_M = -0.3;
    public const double GROUND_OFFSET_M = 0.7;

    /// <summary>담 위로 허용하는 여유(m).</summary>
    public const double WALL_CAP_M = 0.3;

    /// <summary>기둥 쪽 끝 고리의 가로 몫(스케치 2 빨간 큰 타원 — 기둥 바로 안쪽).</summary>
    public const double LOOP_RATIO = 0.06;

    /// <summary>값을 범위 안으로(칸 폭 · 높이는 펜스 칸 범위).</summary>
    public FenceSampleBay Normalized()
        => this with
        {
            PanelWidthM = FencePanelSpec.ClampSpan(PanelWidthM),
            FenceHeightM = FencePanelSpec.ClampHeight(FenceHeightM),
            HasRazor = HasRazor && !IsWall,
        };

    /// <summary>윤형 코일 가운데 높이(m) — 2.5D 펜스 그림과 같은 식(<see cref="FenceLayoutMath.RazorCoilCenterM"/>).</summary>
    public double CoilCenterM => FenceLayoutMath.RazorCoilCenterM(FenceHeightM);

    /// <summary>윤형 코일 반지름(m) — 지름 = 펜스 높이 × <see cref="FenceLayoutMath.RAZOR_COIL_DIAMETER_RATIO"/>.</summary>
    public double CoilRadiusM => FenceHeightM * FenceLayoutMath.RAZOR_COIL_DIAMETER_RATIO / 2;

    /// <summary>코일 꼭대기 높이(m).</summary>
    public double CoilTopM => CoilCenterM + CoilRadiusM;

    /// <summary>기둥 꼭대기 높이(m).</summary>
    public double PostTopM => FenceHeightM + POST_CAP_M;

    /// <summary>센서가 오를 수 있는 가장 높은 곳(m) — 철조망이면 코일 꼭대기, 담이면 담 + 여유, 아니면 기둥 꼭대기.</summary>
    public double TopLimitM => IsWall ? FenceHeightM + WALL_CAP_M : HasRazor ? Math.Max(CoilTopM, PostTopM) : PostTopM;

    /// <summary>결선 펜스 칸 하나 → 샘플.</summary>
    public static FenceSampleBay FromPanel(FencePanelSpec panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        var p = panel.Normalized();
        return new FenceSampleBay(p.SpanM, p.HeightM, p.Style == EnumFenceStyle.ChainLinkRazor, p.IsWall);
    }
}

/// <summary>붙는 점 하나 — 이름 · 종류 · 붙는 곳 · 칸 안 가로 몫 · 높이 · 펜스와 거리 · 표시 이름.</summary>
public sealed record SensorSnapPoint(string Name, SensorAnchorKind Kind, SensorMountAnchor Anchor, double AlongRatio, double HeightM, double DepthM, string Label)
{
    /// <summary>"기둥 B · 중간 · 1.8 m 에 붙음".</summary>
    public string AttachText => $"{Label} · {HeightM:0.0#} m 에 붙음";
}

/// <summary>
/// 담장 한 칸의 붙는 점(사용자 스케치 1 · 2 · sensor-mount-preset §4) — <b>순수 함수</b>.
/// 기둥 A · B 각 4점(꼭대기 · 레일 · 중간 · 아래) + 메쉬 가운데 3점(레일 · 중간 · 아래) + 철조망 칸이면 코일 위 · 가운데 · 끝 고리 + 바닥 2점.
/// </summary>
public static class SensorSnapPoints
{
    public const string POST_A = "post_a";
    public const string POST_B = "post_b";

    /// <summary>칸의 모든 붙는 점(철조망 없는 칸엔 razor 점이 없다 · 담이면 기둥 · 메쉬 대신 담 점).</summary>
    public static IReadOnlyList<SensorSnapPoint> For(FenceSampleBay bay)
    {
        ArgumentNullException.ThrowIfNull(bay);
        var b = bay.Normalized();
        var h = b.FenceHeightM;
        var list = new List<SensorSnapPoint>();
        if (b.IsWall)
        {
            list.Add(new("wall.top", SensorAnchorKind.Wall, SensorMountAnchor.WallTop, 0.5, h, 0, "담 위"));
            list.Add(new("wall.face", SensorAnchorKind.Wall, SensorMountAnchor.WallFace, 0.5, h * LOW_OF_WALL, 0, "담 앞면"));
        }
        else
        {
            foreach (var (post, ratio, letter) in new[] { (POST_A, 0.0, "A"), (POST_B, 1.0, "B") })
            {
                list.Add(new($"{post}.top", SensorAnchorKind.Post, SensorMountAnchor.PostTop, ratio, b.PostTopM, 0, $"기둥 {letter} · 꼭대기"));
                list.Add(new($"{post}.rail", SensorAnchorKind.Post, SensorMountAnchor.PostRail, ratio, h, 0, $"기둥 {letter} · 레일 높이"));
                list.Add(new($"{post}.mid", SensorAnchorKind.Post, SensorMountAnchor.PostMid, ratio, h * FenceSampleBay.MID_RATIO, 0, $"기둥 {letter} · 중간"));
                list.Add(new($"{post}.low", SensorAnchorKind.Post, SensorMountAnchor.PostLow, ratio, h * FenceSampleBay.LOW_RATIO, 0, $"기둥 {letter} · 아래"));
            }
            list.Add(new("mesh.rail", SensorAnchorKind.Rail, SensorMountAnchor.Rail, 0.5, h, 0, "상단 레일 · 가운데"));
            list.Add(new("mesh.mid", SensorAnchorKind.Mesh, SensorMountAnchor.Mesh, 0.5, h * FenceSampleBay.MID_RATIO, 0, "메쉬 · 중간"));
            list.Add(new("mesh.low", SensorAnchorKind.Mesh, SensorMountAnchor.Mesh, 0.5, h * FenceSampleBay.LOW_RATIO, 0, "메쉬 · 아래"));
            if (b.HasRazor)
            {
                foreach (var pct in new[] { 25, 50, 75 })
                    list.Add(new($"razor.top.{pct}", SensorAnchorKind.Razor, SensorMountAnchor.Razor, pct / 100.0, b.CoilTopM, 0, $"철조망 위 · {pct}%"));
                list.Add(new("razor.mid", SensorAnchorKind.Razor, SensorMountAnchor.Razor, 0.5, b.CoilCenterM, 0, "철조망 · 가운데"));
                list.Add(new("razor.loop_a", SensorAnchorKind.Razor, SensorMountAnchor.RazorLoop, FenceSampleBay.LOOP_RATIO, b.CoilCenterM, 0, "철조망 고리 · 기둥 A 쪽"));
                list.Add(new("razor.loop_b", SensorAnchorKind.Razor, SensorMountAnchor.RazorLoop, 1 - FenceSampleBay.LOOP_RATIO, b.CoilCenterM, 0, "철조망 고리 · 기둥 B 쪽"));
            }
        }
        list.Add(new("ground.out", SensorAnchorKind.Ground, SensorMountAnchor.Ground, 0.5, FenceSampleBay.GROUND_DEPTH_M, -FenceSampleBay.GROUND_OFFSET_M, "바닥 · 밖"));
        list.Add(new("ground.in", SensorAnchorKind.Ground, SensorMountAnchor.Ground, 0.5, FenceSampleBay.GROUND_DEPTH_M, FenceSampleBay.GROUND_OFFSET_M, "바닥 · 안"));
        return list;
    }

    /// <summary>담 앞면 점 높이(담 높이의 몫).</summary>
    public const double LOW_OF_WALL = 0.5;

    /// <summary>모델이 붙을 수 있는 점만(허용 목록 순서가 아니라 칸 위 차례 그대로).</summary>
    public static IReadOnlyList<SensorSnapPoint> Allowed(FenceSampleBay bay, SensorModel? model)
        => For(bay).Where(p => model is null || model.Allows(p.Kind)).ToList();

    /// <summary>이름으로 찾기(없으면 <c>null</c> — 예: 철조망 없는 칸의 razor 점).</summary>
    public static SensorSnapPoint? Find(FenceSampleBay bay, string? name)
        => string.IsNullOrEmpty(name) ? null : For(bay).FirstOrDefault(p => p.Name == name);

    /// <summary>
    /// 앞에서 본 모습의 (가로 m · 높이 m) 에서 가장 가까운 허용 점 — 끌기 미리보기 · 놓기. 바닥 점은 펜스와 거리도 같이 본다.
    /// </summary>
    public static SensorSnapPoint? Nearest(FenceSampleBay bay, SensorModel? model, double alongM, double heightM, double depthM = 0)
    {
        var b = bay.Normalized();
        SensorSnapPoint? best = null;
        var distance = double.PositiveInfinity;
        foreach (var p in Allowed(b, model))
        {
            var dx = p.AlongRatio * b.PanelWidthM - alongM;
            var dy = p.HeightM - heightM;
            var dz = p.Kind == SensorAnchorKind.Ground ? (p.DepthM - depthM) * 0.5 : 0;
            var d = dx * dx + dy * dy + dz * dz;
            if (d < distance - 1e-12) { distance = d; best = p; }
        }
        return best;
    }

    /// <summary>
    /// Tab · Shift+Tab — 허용 점 사이 다음 · 이전(끝에서 처음으로 돈다). 지금 점이 없거나 목록에 없으면 첫 점(이전이면 끝 점).
    /// </summary>
    public static SensorSnapPoint? Next(FenceSampleBay bay, SensorModel? model, string? current, bool backwards = false)
    {
        var list = Allowed(bay, model);
        if (list.Count == 0) return null;
        var i = current is null ? -1 : list.ToList().FindIndex(p => p.Name == current);
        if (i < 0) return backwards ? list[^1] : list[0];
        var n = list.Count;
        return list[((backwards ? i - 1 : i + 1) % n + n) % n];
    }
}
