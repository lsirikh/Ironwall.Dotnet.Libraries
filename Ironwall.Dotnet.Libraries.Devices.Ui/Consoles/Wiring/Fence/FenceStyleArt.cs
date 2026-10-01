using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;

/// <summary>
/// 펜스 모양 5종의 그림 재료(fence-style-art · 사용자 참고 사진) — <b>순수 함수</b>. 세계 좌표(가로 <c>x</c> · 높이 <c>y</c> · 깊이 <c>z</c>)를
/// 2.5D 투영(<see cref="FenceProjector"/>)에 태워 그림 목록(<see cref="FenceShape"/>)을 낸다. 입체 · 평면 모두 같은 함수다.
/// </summary>
/// <remarks>
/// <para><b>참고 사진은 모양만 본다</b> — 사진 · 비트맵을 제품에 싣지 않는다. 모든 무늬는 선 · 면을 계산해 그린다.</para>
/// <para><b>성능(망 200칸)</b> — 한 칸의 많은 선 · 조각은 그림 하나(<see cref="FenceShapeKind.Strokes"/> · <see cref="FenceShapeKind.Patches"/>)에 묶어
/// 렌더러가 기하를 한 번 만들어 붙잡아 둔다(장면이 다시 세워질 때 — 망 · 줌이 바뀔 때만). 색은 그릴 때마다 토큰에서 다시 푼다.
/// 무늬가 화면에서 너무 작아지면 그리지 않는다(LOD).</para>
/// <para><b>결정적</b> — 벽돌 색 흔들림 · 시멘트 반점은 (망 번호 · 줄 · 칸) 해시로 정한다. 같은 입력이면 늘 같은 그림(프레임마다 바뀌지 않는다).</para>
/// </remarks>
public static class FenceStyleArt
{
    #region - Constants · LOD -
    /// <summary>코일 지름 = 펜스 높이 × 이 비(참고 사진 35~45%).</summary>
    public const double COIL_DIAMETER_RATIO = 0.4;
    public const double COIL_R_MIN = 12;
    public const double COIL_R_MAX = 34;
    /// <summary>고리 가로 = 세로 × 이 비(0.45~0.6).</summary>
    public const double COIL_ASPECT = 0.52;
    /// <summary>고리 가운데 사이 = 지름 × 이 비(0.35~0.45).</summary>
    public const double COIL_SPACING_RATIO = 0.4;
    /// <summary>고리가 번갈아 기우는 각(라디안 · 약 10°).</summary>
    public const double COIL_TILT = 0.17;
    /// <summary>코일 가운데 높이 = 받침 위 반지름 × 이 비(Y 안에 앉는다).</summary>
    public const double COIL_SEAT_RATIO = 0.95;
    /// <summary>Y 받침 팔 — 수직에서 30° · 길이 = 코일 반지름 × 1.2.</summary>
    public const double ARM_ANGLE = Math.PI / 6;
    public const double ARM_LENGTH_RATIO = 1.2;
    /// <summary>코일 고리 하나의 점 수.</summary>
    public const int COIL_LOOP_POINTS = 20;
    /// <summary>망 한 칸의 코일 고리 상한.</summary>
    public const int COIL_LOOP_CAP = 40;

    /// <summary>디자인펜스 세로살 간격(세계 단위).</summary>
    public const double DESIGN_WIRE_STEP = 4;
    /// <summary>V 접힘이 앞으로 튀어나오는 깊이(입체 · 세계 단위).</summary>
    public const double DESIGN_FOLD_DEPTH = 3.5;

    /// <summary>벽돌 줄 수(망 높이를 이만큼 나눈다) · 벽돌 길이 = 줄 높이 × 이 비.</summary>
    public const int BRICK_ROWS = 16;
    public const double BRICK_LENGTH_RATIO = 2.3;
    /// <summary>벽돌 줄이 화면에서 이보다 낮으면(px) 벽돌을 그리지 않고 바탕 무늬로 둔다.</summary>
    public const double BRICK_MIN_ROW_PX = 3;
    /// <summary>줄눈 폭(세계 단위).</summary>
    public const double MORTAR = 0.9;

    /// <summary>시멘트 반점 크기(세계 단위) · 화면에서 이보다 작으면(px) 반점을 그리지 않는다 · 망 한 칸의 반점 상한.</summary>
    public const double SPECKLE_SIZE = 1.6;
    public const double SPECKLE_MIN_PX = 0.8;
    public const int SPECKLE_CAP = 140;
    /// <summary>반점 하나가 차지하는 면적(세계 단위²) — 밀도.</summary>
    public const double SPECKLE_AREA = 60;
    #endregion

    #region - Razor (윤형철조망) -
    /// <summary>
    /// 코일 반지름(세계 높이 단위) — 지름이 펜스 높이의 약 40%(참고 사진 · 재검토 렌더: 코일 지름이 또렷이 보여야 한다). 아주 낮거나 높은 펜스는 범위 안으로.
    /// </summary>
    public static double CoilRadius(double fenceHeight) => Math.Clamp(fenceHeight * COIL_DIAMETER_RATIO / 2, COIL_R_MIN, COIL_R_MAX);

    /// <summary>
    /// Y 받침(기둥 위) — 두 팔이 수직에서 30° 바깥으로(앞 모습에서 "V" 로 또렷이 · 참고 사진 ③), 길이 = 코일 반지름 × 1.2. 입체에서는 깊이로도 조금 벌린다.
    /// 팔 끝 높이를 함께 돌려준다(철선이 걸리는 자리).
    /// </summary>
    public static (IReadOnlyList<FenceShape> Shapes, double TipY, double TipZ) YArms(FenceProjector p, double x, double postTop, double fenceHeight)
    {
        var length = CoilRadius(fenceHeight) * ARM_LENGTH_RATIO;
        var dx = length * Math.Sin(ARM_ANGLE);
        var tipY = postTop + length * Math.Cos(ARM_ANGLE);
        var tipZ = length * 0.35 * p.K;
        var arms = new List<Point[]>
        {
            new[] { p.P(x, postTop, 0), p.P(x - dx, tipY, -tipZ) },
            new[] { p.P(x, postTop, 0), p.P(x + dx, tipY, tipZ) },
        };
        return (new[] { Figures(FenceShapeKind.Strokes, FenceInk.RazorArm, arms, closed: false, null) }, tipY, tipZ);
    }

    /// <summary>
    /// 콘서티나 코일(망 한 칸 · <paramref name="xa"/>~<paramref name="xb"/>) — <b>앞에서 보이는 고리</b>: 겹친 타원(가로 = 세로 × <see cref="COIL_ASPECT"/>)이
    /// 지름의 <see cref="COIL_SPACING_RATIO"/> 간격으로 줄지어, 번갈아 조금씩 기울어(용수철처럼) 겹친다. 고리마다 작은 가시, 팔 끝 사이 철선 둘 + 꼭대기 철선 하나.
    /// 고리는 화면에서 타원으로 그린다 — 펜스와 직각인 원을 투영하면 앞 모습에서 선으로 접혀 "막대 줄"로 보였다(재검토 렌더).
    /// </summary>
    /// <param name="postTop">기둥 꼭대기(받침 시작) 높이(세계 단위).</param>
    /// <param name="fenceHeight">펜스 높이(세계 단위) — 코일 지름을 정한다.</param>
    public static IReadOnlyList<FenceShape> RazorCoil(FenceProjector p, double xa, double xb, double postTop, double fenceHeight, string? color = null)
    {
        var o = new List<FenceShape>(3);
        if (xb - xa < 1) return o;
        var geometry = CoilLayout(p, xa, xb, postTop, fenceHeight);
        var loops = new List<Point[]>(geometry.Centers.Count);
        var barbs = new List<Point[]>();
        for (var n = 0; n < geometry.Centers.Count; n++)
        {
            var c = geometry.Centers[n];
            var tilt = (n % 2 == 0 ? 1 : -1) * COIL_TILT;
            var (sin, cos) = Math.SinCos(tilt);
            var loop = new Point[COIL_LOOP_POINTS];
            for (var i = 0; i < COIL_LOOP_POINTS; i++)
            {
                var t = 2 * Math.PI * i / COIL_LOOP_POINTS;
                var ex = geometry.Rx * Math.Cos(t);
                var ey = geometry.Ry * Math.Sin(t);
                loop[i] = new Point(c.X + ex * cos - ey * sin, c.Y + ex * sin + ey * cos);
            }
            loops.Add(loop);
            // 가시 — 고리 둘레에 짧은 틱(바깥으로)
            for (var i = n % 3; i < COIL_LOOP_POINTS; i += COIL_LOOP_POINTS / 6)
            {
                var q = loop[i];
                var v = q - c;
                if (v.Length < 1e-6) continue;
                v.Normalize();
                barbs.Add(new[] { q - v * 1.2 + new Vector(-v.Y, v.X) * 1.3, q + v * 1.8 - new Vector(-v.Y, v.X) * 1.3 });
            }
        }
        if (loops.Count > 0)
        {
            o.Add(Figures(FenceShapeKind.Strokes, FenceInk.RazorCoil, loops, closed: true, color));
            o.Add(Figures(FenceShapeKind.Strokes, FenceInk.RazorBarb, barbs, closed: false, color));
        }
        // 철선 — 받침 팔 끝 높이에 둘(앞 · 뒤), 기둥 꼭대기 바로 위에 하나
        var length = geometry.Radius * ARM_LENGTH_RATIO;
        var tipY = postTop + length * Math.Cos(ARM_ANGLE);
        var tipZ = length * 0.35 * p.K;
        var strands = new List<Point[]>
        {
            new[] { p.P(xa, tipY, -tipZ), p.P(xb, tipY, -tipZ) },
            new[] { p.P(xa, tipY - length * 0.12, tipZ), p.P(xb, tipY - length * 0.12, tipZ) },
            new[] { p.P(xa, postTop + 2, 0), p.P(xb, postTop + 2, 0) },
        };
        o.Add(Figures(FenceShapeKind.Strokes, FenceInk.RazorStrand, strands, closed: false, null));
        return o;
    }

    /// <summary>코일 배치(시험 대상) — 고리 가운데(화면 좌표) · 화면 반지름(가로 · 세로) · 세계 반지름 · 꼭대기/바닥(화면 y).</summary>
    public sealed record CoilGeometry(IReadOnlyList<Point> Centers, double Rx, double Ry, double Radius, double TopY, double BottomY);

    /// <summary>
    /// 코일 배치 — 가운데 높이 = 받침 위 반지름 × <see cref="COIL_SEAT_RATIO"/>(Y 안에 앉는다), 고리 간격 = 지름(화면) × <see cref="COIL_SPACING_RATIO"/>,
    /// 망 한 칸의 고리는 <see cref="COIL_LOOP_CAP"/> 이하.
    /// </summary>
    public static CoilGeometry CoilLayout(FenceProjector p, double xa, double xb, double postTop, double fenceHeight)
    {
        var r = CoilRadius(fenceHeight);
        var cy = postTop + r * COIL_SEAT_RATIO;
        var ry = r * p.Cy;
        var rx = ry * COIL_ASPECT;
        var step = Math.Max(2 * ry * COIL_SPACING_RATIO, (xb - xa) / COIL_LOOP_CAP);
        var centers = new List<Point>();
        for (var x0 = xa + rx * 0.6; x0 <= xb - rx * 0.6 + 1e-6; x0 += step) centers.Add(p.P(x0, cy, 0));
        if (centers.Count == 0 && xb - xa >= 1) centers.Add(p.P((xa + xb) / 2, cy, 0));
        var mid = p.P(xa, cy, 0).Y;
        return new CoilGeometry(centers, rx, ry, r, mid - ry * 1.02, mid + ry * 1.02);
    }
    #endregion

    #region - Design fence (디자인펜스 · 3D 용접망) -
    /// <summary>
    /// 디자인펜스 망 — 세로 철선(간격 <see cref="DESIGN_WIRE_STEP"/>) · 가로 철선, 그리고 가로로 앞으로 꺾인 V 접힘 2~3줄(입체에서 앞으로 튀어나오고 밝은 선이 얹힌다).
    /// </summary>
    public static IReadOnlyList<FenceShape> DesignMesh(FenceProjector p, double a, double b, double h, string? color = null)
    {
        var o = new List<FenceShape>(3);
        if (b - a < 1 || h < 10) return o;
        var folds = FoldHeights(h);
        var bulge = DESIGN_FOLD_DEPTH * Math.Max(0.35, p.K);
        const double D = 3.5;
        var bottom = 5.0;
        var top = h - 3;

        var verticals = new List<Point[]>();
        var count = Math.Max(2, (int)Math.Round((b - a) / DESIGN_WIRE_STEP));
        for (var i = 0; i <= count; i++)
        {
            var x = a + (b - a) * i / count;
            var pts = new List<Point> { p.P(x, bottom, 0) };
            foreach (var f in folds) { pts.Add(p.P(x, f - D, 0)); pts.Add(p.P(x, f, bulge)); pts.Add(p.P(x, f + D, 0)); }
            pts.Add(p.P(x, top, 0));
            verticals.Add(pts.ToArray());
        }
        var horizontals = new List<Point[]> { new[] { p.P(a, bottom, 0), p.P(b, bottom, 0) }, new[] { p.P(a, top, 0), p.P(b, top, 0) } };
        // 접힘 사이 가로 철선(사진: 200mm 안팎 간격)
        var levels = new List<double>();
        for (var y = bottom + 12; y < top - 4; y += 12)
            if (folds.All(f => Math.Abs(f - y) > D + 2)) levels.Add(y);
        horizontals.AddRange(levels.Select(y => new[] { p.P(a, y, 0), p.P(b, y, 0) }));

        o.Add(Figures(FenceShapeKind.Strokes, FenceInk.DesignWire, verticals.Concat(horizontals).ToList(), closed: false, color));
        o.Add(Figures(FenceShapeKind.Strokes, FenceInk.DesignFold, folds.Select(f => new[] { p.P(a, f, bulge), p.P(b, f, bulge) }).ToList(), closed: false, color));
        return o;
    }

    /// <summary>V 접힘 높이 — 낮은 펜스는 2줄, 1.6m(세계 약 86) 이상은 3줄.</summary>
    public static IReadOnlyList<double> FoldHeights(double h)
        => h >= 86 ? new[] { h * 0.22, h * 0.5, h * 0.78 } : new[] { h * 0.3, h * 0.7 };

    /// <summary>디자인펜스 기둥의 고정 클램프 — 접힘 높이마다 작은 받침(앞면).</summary>
    public static IReadOnlyList<FenceShape> DesignClamps(FenceProjector p, double x, double h, double postWidth, string? color = null)
    {
        var w = postWidth + 3;
        var z = p.De / 2 + 1;
        return FoldHeights(h).Select(f => new FenceShape(FenceShapeKind.Polygon, FenceInk.DesignClamp,
            new[] { p.P(x - w / 2, f - 2.2, z), p.P(x + w / 2, f - 2.2, z), p.P(x + w / 2, f + 2.2, z), p.P(x - w / 2, f + 2.2, z) }, Color: color)).ToList();
    }
    #endregion

    #region - Brick wall (벽돌담) -
    /// <summary>
    /// 벽돌 쌓기(엇갈림 · running bond) — 담 앞면(깊이 <paramref name="zFront"/>)에 줄눈 바탕 + 벽돌 조각을 색 셋으로 나눠(해시로 정한 흔들림) 얹는다.
    /// 줄이 화면에서 <see cref="BRICK_MIN_ROW_PX"/> 보다 낮으면 빈 목록(바탕 무늬만 남는다).
    /// </summary>
    public static IReadOnlyList<FenceShape> BrickCourses(FenceProjector p, double xa, double xb, double h, double zFront, double zoom, int panelIndex, string? color = null)
    {
        var o = new List<FenceShape>(4);
        var row = h / BRICK_ROWS;
        if (xb - xa < 1 || row * Math.Max(zoom, 0) < BRICK_MIN_ROW_PX) return o;
        var length = row * BRICK_LENGTH_RATIO;
        var tones = new[] { new List<Point[]>(), new List<Point[]>(), new List<Point[]>() };
        for (var r = 0; r < BRICK_ROWS; r++)
        {
            var y0 = r * row + MORTAR / 2;
            var y1 = (r + 1) * row - MORTAR / 2;
            var start = xa - (r % 2 == 1 ? length / 2 : 0);
            var c = 0;
            for (var x = start; x < xb - 0.5; x += length, c++)
            {
                var b0 = Math.Max(xa, x) + MORTAR / 2;
                var b1 = Math.Min(xb, x + length) - MORTAR / 2;
                if (b1 - b0 < 0.6) continue;
                var tone = (int)(Hash(panelIndex, r, c) % 7) switch { 0 or 1 => 1, 2 => 2, _ => 0 };
                tones[tone].Add(new[] { p.P(b0, y0, zFront), p.P(b1, y0, zFront), p.P(b1, y1, zFront), p.P(b0, y1, zFront) });
            }
        }
        o.Add(new FenceShape(FenceShapeKind.Polygon, FenceInk.BrickMortarFace,
            new[] { p.P(xa, 0, zFront), p.P(xb, 0, zFront), p.P(xb, h, zFront), p.P(xa, h, zFront) }, Color: null));
        o.Add(Figures(FenceShapeKind.Patches, FenceInk.BrickTone0, tones[0], closed: true, color));
        o.Add(Figures(FenceShapeKind.Patches, FenceInk.BrickTone1, tones[1], closed: true, color));
        o.Add(Figures(FenceShapeKind.Patches, FenceInk.BrickTone2, tones[2], closed: true, color));
        return o;
    }
    #endregion

    #region - Concrete (시멘트담 · 미장) -
    /// <summary>
    /// 미장 결 — 담 앞면에 (망 번호로 씨를 둔) 결정적 반점 두 색. 개수는 면적에 비례하되 <see cref="SPECKLE_CAP"/> 를 넘지 않고,
    /// 반점이 화면에서 <see cref="SPECKLE_MIN_PX"/> 보다 작으면 빈 목록.
    /// </summary>
    public static IReadOnlyList<FenceShape> ConcreteStucco(FenceProjector p, double xa, double xb, double h, double zFront, double zoom, int panelIndex, string? color = null)
    {
        var o = new List<FenceShape>(2);
        if (xb - xa < 1 || SPECKLE_SIZE * Math.Max(zoom, 0) < SPECKLE_MIN_PX) return o;
        var count = Math.Min(SPECKLE_CAP, (int)((xb - xa) * h / SPECKLE_AREA));
        var dark = new List<Point[]>();
        var light = new List<Point[]>();
        for (var i = 0; i < count; i++)
        {
            var hx = Hash(panelIndex, i, 1);
            var hy = Hash(panelIndex, i, 2);
            var x = xa + 1 + (xb - xa - 2) * (hx % 10000) / 10000.0;
            var y = 2 + (h - 4) * (hy % 10000) / 10000.0;
            var s = SPECKLE_SIZE * (0.6 + (hx >> 16) % 5 * 0.15);
            var dot = new[] { p.P(x, y, zFront), p.P(x + s, y + s * 0.3, zFront), p.P(x + s * 0.7, y + s, zFront), p.P(x - s * 0.2, y + s * 0.6, zFront) };
            ((hy >> 16) % 3 == 0 ? light : dark).Add(dot);
        }
        o.Add(Figures(FenceShapeKind.Patches, FenceInk.ConcreteSpeckleDark, dark, closed: true, color));
        o.Add(Figures(FenceShapeKind.Patches, FenceInk.ConcreteSpeckleLight, light, closed: true, color));
        return o;
    }
    #endregion

    #region - Helpers -
    /// <summary>결정적 해시(FNV-1a 섞기) — 같은 (a, b, c) 면 늘 같은 값.</summary>
    public static uint Hash(int a, int b, int c)
    {
        unchecked
        {
            var h = 2166136261u;
            foreach (var v in new[] { a, b, c })
            {
                h ^= (uint)v;
                h *= 16777619u;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
            }
            return h;
        }
    }

    /// <summary>여러 도형(점 배열)을 그림 하나로 — 점을 이어 붙이고 도형마다 점 수를 적는다.</summary>
    private static FenceShape Figures(FenceShapeKind kind, FenceInk ink, IReadOnlyList<Point[]> figures, bool closed, string? color)
        => new(kind, ink, figures.SelectMany(f => f).ToArray(), Color: color, Figures: figures.Select(f => f.Length).ToArray(), Closed: closed);

    #endregion
}
