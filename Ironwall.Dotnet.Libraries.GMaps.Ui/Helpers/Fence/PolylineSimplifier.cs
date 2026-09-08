using System;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence
{
    /// <summary>
    /// 드래그 드로잉 스트로크 → 정점 (PRD FR-01, D7). 순수 함수: Douglas-Peucker(ε=지도 px) + 최소 점 간격(m).
    /// 좌표 타입을 (double X, double Y) 튜플로 두어 WPF Point 없이 테스트한다.
    /// </summary>
    public static class PolylineSimplifier
    {
        /// <summary>Douglas-Peucker 단순화. 점 2개 이하면 그대로. ε ≤ 0 이면 중복점만 제거.</summary>
        public static IReadOnlyList<(double X, double Y)> DouglasPeucker(IReadOnlyList<(double X, double Y)> points, double epsilon)
        {
            if (points is null || points.Count == 0) return Array.Empty<(double, double)>();
            var dedup = new List<(double X, double Y)> { points[0] };
            for (int i = 1; i < points.Count; i++)
                if (Math.Abs(points[i].X - dedup[^1].X) > 1e-9 || Math.Abs(points[i].Y - dedup[^1].Y) > 1e-9) dedup.Add(points[i]);
            if (dedup.Count <= 2 || !(epsilon > 0)) return dedup;
            var keep = new bool[dedup.Count]; keep[0] = keep[^1] = true;
            var stack = new Stack<(int, int)>(); stack.Push((0, dedup.Count - 1));
            while (stack.Count > 0)
            {
                var (s, e) = stack.Pop();
                double maxD = -1; int idx = -1;
                for (int i = s + 1; i < e; i++)
                {
                    double d = PerpendicularDistance(dedup[i], dedup[s], dedup[e]);
                    if (d > maxD) { maxD = d; idx = i; }
                }
                if (idx >= 0 && maxD > epsilon) { keep[idx] = true; stack.Push((s, idx)); stack.Push((idx, e)); }
            }
            var result = new List<(double X, double Y)>();
            for (int i = 0; i < dedup.Count; i++) if (keep[i]) result.Add(dedup[i]);
            return result;
        }

        public static double PerpendicularDistance((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len2 = dx * dx + dy * dy;
            if (len2 < 1e-12) return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
            double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2, 0, 1);
            double px = a.X + t * dx, py = a.Y + t * dy;
            return Math.Sqrt((p.X - px) * (p.X - px) + (p.Y - py) * (p.Y - py));
        }

        /// <summary>
        /// 최소 점 간격 강제 — 앞 점과의 거리(distance 델리게이트, m)가 minSpacing 미만인 점을 버린다. 마지막 점은 항상 유지(너무 가까우면 직전 점을 대체).
        /// </summary>
        public static IReadOnlyList<T> EnforceMinSpacing<T>(IReadOnlyList<T> points, double minSpacing, Func<T, T, double> distance)
        {
            if (points is null || points.Count == 0) return Array.Empty<T>();
            if (points.Count == 1 || !(minSpacing > 0)) return points;
            var result = new List<T> { points[0] };
            for (int i = 1; i < points.Count - 1; i++)
                if (distance(result[^1], points[i]) >= minSpacing) result.Add(points[i]);
            var last = points[^1];
            if (result.Count >= 2 && distance(result[^1], last) < minSpacing) result[^1] = last;
            else if (result.Count == 1 && distance(result[0], last) < minSpacing) result.Add(last);   // 점 2개는 유지(라인 성립)
            else result.Add(last);
            return result;
        }
    }
}
