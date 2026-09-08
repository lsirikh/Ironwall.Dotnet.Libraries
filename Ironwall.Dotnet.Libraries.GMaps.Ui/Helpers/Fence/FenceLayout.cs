using System;
using System.Collections.Generic;
using System.Linq;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence
{
    /// <summary>
    /// 로컬 평면 좌표(X=동, Z=북). 단위는 입력이 정한다 — <see cref="FenceLayout.ToLocalMeters"/> 로 만들면 m(첫 정점 기준 등거리 근사),
    /// 3D 철망 컨트롤(GMapMarkerPidsGroup3DControl.ComputeFrame)은 컨트롤 로컬 <b>px</b> 프레임을 그대로 넣는다(2D 선 정합). 레이아웃 자체는 단위 무관.
    /// </summary>
    public readonly record struct FencePoint(double X, double Z)
    {
        public double DistanceTo(FencePoint o) => Math.Sqrt((o.X - X) * (o.X - X) + (o.Z - Z) * (o.Z - Z));
        public static FencePoint Lerp(FencePoint a, FencePoint b, double t) => new(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t);
    }

    /// <summary>통문 절개 요청 — 통문 중심(로컬 m)과 폭(m). 변에서 <see cref="FenceDefaults.GateSnapM"/> 이내일 때만 절개된다.</summary>
    public readonly record struct GateCut(FencePoint Center, double WidthM);

    /// <summary>기둥 — 코너 여부·굵기(내각 &lt; 100° 코너)·소속 변.</summary>
    public sealed record FencePost(FencePoint Position, bool IsCorner, bool IsThick, int EdgeIndex);

    /// <summary>철망 패널(변 위 구간). 통문 절개로 잘린 부분은 아예 생성되지 않는다.</summary>
    public sealed record FencePanel(int EdgeIndex, int Index, FencePoint A, FencePoint B)
    {
        public double LengthM => A.DistanceTo(B);
    }

    /// <summary>펜스 센서 노드(센서 장착 모드). GlobalIndex 는 라인 시작부터의 순번(0-base) — 그룹 센서 순번 매핑 키.</summary>
    public sealed record FenceNode(int EdgeIndex, int Index, int GlobalIndex, FencePoint Position);

    /// <summary>
    /// 배치 결과. <paramref name="TotalLengthM"/>·<paramref name="RemainderM"/> 은 <b>입력 좌표 단위</b>다 — px 프레임에서 계산했으면 px 이므로
    /// m 가 필요한 소비자(삼각형 예산 <c>FenceMath.ExceedsBudget</c>)는 metersPerPixel 을 곱한다(C17, FenceRunVisual.MetersPerPixel).
    /// </summary>
    public sealed record FenceLayoutResult(
        IReadOnlyList<FencePost> Posts,
        IReadOnlyList<FencePanel> Panels,
        IReadOnlyList<FenceNode> Nodes,
        double TotalLengthM,
        double RemainderM,
        int EdgeCount)
    {
        public static readonly FenceLayoutResult Empty = new(Array.Empty<FencePost>(), Array.Empty<FencePanel>(), Array.Empty<FenceNode>(), 0, 0, 0);
    }

    /// <summary>
    /// 펜스 라인 → 기둥·패널·센서 노드 배치 순수 함수 (PRD FR-02, 스토리보드 §A-4). WPF/GMap.NET 무의존.
    /// <list type="bullet">
    /// <item>기둥 모드: 변마다 n = max(1, round(L/s)) 패널, 실간격 L/n(균등 분배). 꺾임점엔 코너 기둥, 내각 &lt; 100° 면 굵게.</item>
    /// <item>센서 모드: 노드 floor(L/s) 개를 정확 간격 s 로, 잔여(&lt; s)는 마지막 패널. 코너 기둥은 유지.</item>
    /// <item>닫힌 경로: 마지막 → 첫 정점 변 포함(정점 3개 이상).</item>
    /// <item>통문: 중심이 변에서 GateSnapM 이내면 변 위로 투영, 폭 w 구간의 패널을 절개(겹치는 절개는 병합).</item>
    /// <item>고도는 1차 무시.</item>
    /// </list>
    /// </summary>
    public static class FenceLayout
    {
        private const double Eps = 1e-6;

        public static FenceLayoutResult Compute(IReadOnlyList<FencePoint> points, double spacingM, EnumFenceMode mode, bool isClosed, IReadOnlyList<GateCut>? gates = null)
        {
            if (points is null || points.Count < 2) return FenceLayoutResult.Empty;
            if (!(spacingM > 0) || double.IsNaN(spacingM) || double.IsInfinity(spacingM)) spacingM = FenceDefaults.PostSpacingM;

            // 변 목록(영길이 변 제거)
            var edges = new List<(FencePoint A, FencePoint B, int SrcIndex)>();
            for (int i = 0; i < points.Count - 1; i++)
                if (points[i].DistanceTo(points[i + 1]) > Eps) edges.Add((points[i], points[i + 1], i));
            bool closed = isClosed && points.Count >= 3;
            if (closed && points[^1].DistanceTo(points[0]) > Eps) edges.Add((points[^1], points[0], points.Count - 1));
            if (edges.Count == 0) return FenceLayoutResult.Empty;

            var posts = new List<FencePost>();
            var panels = new List<FencePanel>();
            var nodes = new List<FenceNode>();
            double total = 0, remainder = 0; int globalNode = 0;

            for (int e = 0; e < edges.Count; e++)
            {
                var (a, b, _) = edges[e];
                double L = a.DistanceTo(b); total += L;
                var cuts = CutIntervals(a, b, L, gates);

                // 코너 기둥: 변의 시작점(닫힘이 아니면 마지막 변의 끝점도)
                bool thick = InteriorAngleDeg(edges, e, closed) < FenceDefaults.ThickCornerAngleDeg;
                posts.Add(new FencePost(a, IsCorner: true, IsThick: thick, EdgeIndex: e));
                if (e == edges.Count - 1 && !closed) posts.Add(new FencePost(b, IsCorner: true, IsThick: false, EdgeIndex: e));

                if (mode == EnumFenceMode.Posts)
                {
                    int n = Math.Max(1, (int)Math.Round(L / spacingM, MidpointRounding.AwayFromZero));
                    double step = L / n;
                    for (int k = 1; k < n; k++) posts.Add(new FencePost(FencePoint.Lerp(a, b, k * step / L), IsCorner: false, IsThick: false, EdgeIndex: e));
                    for (int k = 0; k < n; k++) AddPanelClipped(panels, e, k, a, b, L, k * step, (k + 1) * step, cuts);
                }
                else
                {
                    int nFull = (int)Math.Floor(L / spacingM + Eps);          // 정확 간격 패널 수
                    double rem = L - nFull * spacingM; if (rem < Eps) rem = 0;  // 잔여(< s)
                    remainder += rem;
                    // 노드: 마지막 노드가 코너 위(=L)에 놓이면 코너 기둥이 대신하므로 제외
                    int nNodes = (nFull > 0 && rem == 0) ? nFull - 1 : nFull;
                    for (int k = 1; k <= nNodes; k++)
                        nodes.Add(new FenceNode(e, k - 1, globalNode++, FencePoint.Lerp(a, b, k * spacingM / L)));
                    int panelIdx = 0;
                    for (int k = 0; k < nFull; k++) AddPanelClipped(panels, e, panelIdx++, a, b, L, k * spacingM, (k + 1) * spacingM, cuts);
                    if (rem > 0) AddPanelClipped(panels, e, panelIdx, a, b, L, nFull * spacingM, L, cuts);
                }
            }
            return new FenceLayoutResult(posts, panels, nodes, total, remainder, edges.Count);
        }

        /// <summary>정점 e 의 시작점에서의 내각(도). 첫 정점(열린 경로)은 180 으로 취급.</summary>
        public static double InteriorAngleDeg(IReadOnlyList<(FencePoint A, FencePoint B, int SrcIndex)> edges, int e, bool closed)
        {
            int prev = e - 1;
            if (prev < 0) { if (!closed) return 180; prev = edges.Count - 1; }
            var (pa, pb, _) = edges[prev]; var (a, b, _) = edges[e];
            double ux = pa.X - pb.X, uz = pa.Z - pb.Z;   // 이전 변을 거꾸로(꼭짓점 → 이전 점)
            double vx = b.X - a.X, vz = b.Z - a.Z;        // 다음 변
            double lu = Math.Sqrt(ux * ux + uz * uz), lv = Math.Sqrt(vx * vx + vz * vz);
            if (lu < Eps || lv < Eps) return 180;
            double cos = Math.Clamp((ux * vx + uz * vz) / (lu * lv), -1, 1);
            return Math.Acos(cos) * 180.0 / Math.PI;
        }

        /// <summary>변 위 절개 구간 [t0,t1](m, 변 시작 기준) 목록 — 겹치는 구간은 병합.</summary>
        public static IReadOnlyList<(double T0, double T1)> CutIntervals(FencePoint a, FencePoint b, double L, IReadOnlyList<GateCut>? gates)
        {
            if (gates is null || gates.Count == 0 || L < Eps) return Array.Empty<(double, double)>();
            var list = new List<(double T0, double T1)>();
            double dx = (b.X - a.X) / L, dz = (b.Z - a.Z) / L;
            foreach (var g in gates)
            {
                if (!(g.WidthM > 0)) continue;
                double t = (g.Center.X - a.X) * dx + (g.Center.Z - a.Z) * dz;   // 변 위 투영 파라미터(m)
                if (t < -Eps || t > L + Eps) continue;
                var foot = new FencePoint(a.X + dx * t, a.Z + dz * t);
                if (foot.DistanceTo(g.Center) > FenceDefaults.GateSnapM) continue;
                list.Add((Math.Max(0, t - g.WidthM / 2), Math.Min(L, t + g.WidthM / 2)));
            }
            if (list.Count == 0) return Array.Empty<(double, double)>();
            list.Sort((x, y) => x.T0.CompareTo(y.T0));
            var merged = new List<(double T0, double T1)> { list[0] };
            for (int i = 1; i < list.Count; i++)
            {
                var last = merged[^1];
                if (list[i].T0 <= last.T1 + Eps) merged[^1] = (last.T0, Math.Max(last.T1, list[i].T1));
                else merged.Add(list[i]);
            }
            return merged;
        }

        private static void AddPanelClipped(List<FencePanel> panels, int edge, int index, FencePoint a, FencePoint b, double L, double t0, double t1, IReadOnlyList<(double T0, double T1)> cuts)
        {
            if (t1 - t0 < Eps) return;
            // 절개 구간을 빼고 남는 부분만 패널로
            var pieces = new List<(double, double)> { (t0, t1) };
            foreach (var (c0, c1) in cuts)
            {
                var next = new List<(double, double)>();
                foreach (var (p0, p1) in pieces)
                {
                    if (c1 <= p0 + Eps || c0 >= p1 - Eps) { next.Add((p0, p1)); continue; }
                    if (c0 > p0 + Eps) next.Add((p0, c0));
                    if (c1 < p1 - Eps) next.Add((c1, p1));
                }
                pieces = next;
            }
            foreach (var (p0, p1) in pieces)
                if (p1 - p0 > Eps) panels.Add(new FencePanel(edge, index, FencePoint.Lerp(a, b, p0 / L), FencePoint.Lerp(a, b, p1 / L)));
        }

        /// <summary>위경도 점열 → 첫 점 기준 로컬 미터 평면(X=동, Z=북). 하버사인 대신 등거리 근사(수백 m 라인에서 오차 &lt; 0.1%).</summary>
        public static IReadOnlyList<FencePoint> ToLocalMeters(IReadOnlyList<(double Lat, double Lng)> geo)
        {
            if (geo is null || geo.Count == 0) return Array.Empty<FencePoint>();
            const double R = 6371000.0;
            double lat0 = geo[0].Lat * Math.PI / 180.0, lng0 = geo[0].Lng * Math.PI / 180.0, cos0 = Math.Cos(lat0);
            var result = new FencePoint[geo.Count];
            for (int i = 0; i < geo.Count; i++)
            {
                double lat = geo[i].Lat * Math.PI / 180.0, lng = geo[i].Lng * Math.PI / 180.0;
                result[i] = new FencePoint((lng - lng0) * cos0 * R, (lat - lat0) * R);
            }
            return result;
        }

        /// <summary>속성창 요약 라벨 — "기둥 N개 · 실간격 x.xx m" / "센서 N개 · 간격 s m · 잔여 x.x m"</summary>
        public static string Summary(FenceLayoutResult r, double spacingM, EnumFenceMode mode)
        {
            if (r.EdgeCount == 0) return "라인 없음";
            if (mode == EnumFenceMode.Posts)
            {
                double avg = r.Panels.Count > 0 ? r.Panels.Average(p => p.LengthM) : 0;
                return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"기둥 {r.Posts.Count}개 · 실간격 {avg:F2} m");
            }
            return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"센서 {r.Nodes.Count}개 · 간격 {spacingM:F1} m · 잔여 {r.RemainderM:F1} m");
        }
    }
}
