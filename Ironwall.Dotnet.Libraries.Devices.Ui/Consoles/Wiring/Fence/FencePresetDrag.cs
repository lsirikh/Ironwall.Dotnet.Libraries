using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;

/// <summary>
/// 결선 펜스 보기의 위치 프리셋 끌기 계산(sensor-mount-preset W4) — <b>순수 함수</b>(캔버스는 결과를 그리기만 한다 · 헤드리스 시험 대상).
/// 붙는 점의 그림 좌표 · 포인터에 가장 가까운 허용 점 · Alt 자유 위치 · 끄는 동안 그릴 점 모습.
/// </summary>
public static class FencePresetDrag
{
    /// <summary>붙는 점의 그림 좌표 — 가로 = 펜스 m × <paramref name="upm"/> · 높이 = m × <paramref name="vpm"/> · 깊이 = 펜스와 거리 m × <paramref name="upm"/>(안 +).</summary>
    public static Point PointAt(FencePresetSnapPoint point, FenceProjector p, double upm, double vpm)
    {
        ArgumentNullException.ThrowIfNull(point);
        return p.P(point.XM * upm, point.Point.HeightM * vpm, point.Point.DepthM * upm);
    }

    /// <summary>
    /// 포인터(그림 좌표)에 가장 가까운 <b>허용</b> 점 — 반경 없이 늘 하나에 붙는다(W2 "가장 가까운 점에 붙음"). 허용 점이 없으면 <c>null</c>.
    /// </summary>
    public static FencePresetSnapPoint? Nearest(IReadOnlyList<(FencePresetSnapPoint Point, Point At)> points, Point pointer)
    {
        FencePresetSnapPoint? best = null;
        var distance = double.PositiveInfinity;
        foreach (var (point, at) in points ?? Array.Empty<(FencePresetSnapPoint, Point)>())
        {
            if (!point.Allowed) continue;
            var d = (at - pointer).LengthSquared;
            if (d < distance - 1e-9) { distance = d; best = point; }
        }
        return best;
    }

    /// <summary>
    /// Alt 자유 위치 — 포인터(그림 좌표 · 펜스 면 깊이 0)를 (칸 안 가로 m, 높이 m)로. 1 cm 단위로 맞춘다(칸 범위 안 맞춤은 계산 쪽이 한다).
    /// </summary>
    public static (double AlongM, double HeightM) FreeAt(Point pointer, FenceProjector p, double upm, double vpm, double bayStartM)
    {
        var x = (pointer.X / (upm > 0 ? upm : 1)) - bayStartM;
        var y = p.Cy > 0 ? -pointer.Y / p.Cy - p.YLift : 0;
        return (Math.Round(x * 100) / 100, Math.Round(y / (vpm > 0 ? vpm : 1) * 100) / 100);
    }

    /// <summary>끄는 동안 그릴 점 — 붙을 점(빨강 + 고리) · 붙을 수 있는 점(회색 · 다크 흰) · 붙지 않는 점(더 흐림). 자유 위치면 붙을 점이 없다.</summary>
    public static IReadOnlyList<FenceSnapDot> Dots(IReadOnlyList<(FencePresetSnapPoint Point, Point At)> points, FencePresetSnapPoint? target)
        => (points ?? Array.Empty<(FencePresetSnapPoint, Point)>())
            .Select(t => new FenceSnapDot(t.At, target is not null && t.Point == target ? FenceSnapState.MountHot : t.Point.Allowed ? FenceSnapState.Mount : FenceSnapState.MountDim))
            .ToList();

    /// <summary>
    /// 끄는 중 Tab · Shift+Tab — 지금 붙을 점에서 다음 · 이전 허용 점(칸 안 차례 · 끝에서 처음으로 돈다). 허용 점이 없으면 <c>null</c>.
    /// </summary>
    public static FencePresetSnapPoint? Cycle(IReadOnlyList<(FencePresetSnapPoint Point, Point At)> points, FencePresetSnapPoint? current, bool backwards)
    {
        var allowed = (points ?? Array.Empty<(FencePresetSnapPoint, Point)>()).Select(t => t.Point).Where(p => p.Allowed).ToList();
        if (allowed.Count == 0) return null;
        var i = current is null ? -1 : allowed.IndexOf(current);
        if (i < 0) return backwards ? allowed[^1] : allowed[0];
        var n = allowed.Count;
        return allowed[((backwards ? i - 1 : i + 1) % n + n) % n];
    }
}
