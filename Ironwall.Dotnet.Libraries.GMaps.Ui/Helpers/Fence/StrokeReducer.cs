using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence
{
    /// <summary>
    /// 프리핸드 스트로크 → 정점 축약(FR-01/D7): 화면 px 에서 Douglas-Peucker(ε) → 지리 투영 → 최소 간격(m) 강제.
    /// WPF/GMap.NET 무의존 — 투영·거리는 콜백으로 받는다. 기존 마지막 정점(seed)을 주면 그와 너무 가까운 첫 점을 버린다.
    /// </summary>
    public static class StrokeReducer
    {
        public static List<T> Reduce<T>(
            IReadOnlyList<(double X, double Y)> screen, double epsilonPx,
            Func<(double X, double Y), T> project, Func<T, T, double> distance, double minSpacing,
            T? seed = default, bool hasSeed = false)
        {
            if (screen is null || screen.Count == 0) return new List<T>();
            var simplified = PolylineSimplifier.DouglasPeucker(screen, epsilonPx);
            var mapped = simplified.Select(project).ToList();
            if (!hasSeed) return PolylineSimplifier.EnforceMinSpacing(mapped, minSpacing, distance).ToList();
            mapped.Insert(0, seed!);
            var kept = PolylineSimplifier.EnforceMinSpacing(mapped, minSpacing, distance).ToList();
            if (kept.Count > 0 && Equals(kept[0], seed)) kept.RemoveAt(0);
            // seed 와 너무 가까운 단일 잔여점(스트로크가 사실상 제자리)은 버린다
            if (kept.Count == 1 && distance(seed!, kept[0]) < minSpacing) kept.Clear();
            return kept;
        }
    }
}
