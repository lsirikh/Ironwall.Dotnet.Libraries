using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Concept;

/// <summary>
/// 개념도 그림(fence-wiring-editor FR-12) — <b>순수</b> 그림 목록(<see cref="FenceShape"/>). 렌더러가 잉크를 토큰으로 풀어 그린다.
/// 링: Ch1(A) 포트 → 리턴케이블 → #1 → … → #N → 리턴케이블 → Ch2(B) 포트, 흐름 방향 화살표.
/// </summary>
public static class ConceptScene
{
    /// <summary>바탕 — 내부망 띠(IP 센서가 있으면) · 체인 선 · 흐름 화살표 · 리턴케이블 · 포트 이름.</summary>
    public static IReadOnlyList<FenceShape> Background(ConceptGeometry g, IReadOnlyList<ConceptNodeInfo> nodes)
    {
        var o = new List<FenceShape>(64);
        var points = g.Nodes;
        var r = ConceptLayout.NODE_R;

        if (points.Count > 0 && nodes.Any(n => n.IsIp))
        {
            var box = Bounds(points.Select(p => p.Center));
            box.Inflate(24, 22);
            box.Height += 12;                                     // 신호등 자리
            o.Add(new FenceShape(FenceShapeKind.Rect, FenceInk.ConceptInternalNet, new[] { box.TopLeft, box.BottomRight }, 8, 8));
        }

        // 체인 선 + 흐름 화살표(Ch1 → Ch2)
        for (var i = 1; i < points.Count; i++)
        {
            var a = points[i - 1].Center;
            var b = points[i].Center;
            o.Add(new FenceShape(FenceShapeKind.Line, FenceInk.ConceptWire, new[] { Toward(a, b, r), Toward(b, a, r) }));
            var d = b - a;
            if (d.Length < 3 * r) continue;
            d.Normalize();
            var m = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
            var n = new Vector(-d.Y, d.X);
            o.Add(new FenceShape(FenceShapeKind.Polygon, FenceInk.ConceptArrow, new[] { m + d * 5, m - d * 4 + n * 4.5, m - d * 4 - n * 4.5 }));
        }

        // 리턴케이블 — 두 포트에서 양 끝 노드로(센서 없는 선)
        if (points.Count > 0)
        {
            var first = points[0].Center;
            var last = points[^1].Center;
            if (g.Shape == ConceptShape.Strip)
            {
                var y = (first.Y + r + 14 + g.Controller.Top) / 2;
                o.Add(new FenceShape(FenceShapeKind.Polyline, FenceInk.ConceptReturn, new[] { g.Port1, new Point(g.Port1.X, y), new Point(first.X, y), new Point(first.X, first.Y + r) }));
                o.Add(new FenceShape(FenceShapeKind.Polyline, FenceInk.ConceptReturn, new[] { g.Port2, new Point(g.Port2.X, y), new Point(last.X, y), new Point(last.X, last.Y + r) }));
            }
            else
            {
                o.Add(new FenceShape(FenceShapeKind.Line, FenceInk.ConceptReturn, new[] { g.Port1, Toward(first, g.Port1, r) }));
                o.Add(new FenceShape(FenceShapeKind.Line, FenceInk.ConceptReturn, new[] { g.Port2, Toward(last, g.Port2, r) }));
            }
        }

        o.Add(Text(FenceInk.ConceptPortText, new Point(g.Controller.Left - 6, g.Controller.Top + 13), WiringValidation.PORT_1, 11, FenceTextAnchor.End));
        o.Add(Text(FenceInk.ConceptPortText, new Point(g.Controller.Right + 6, g.Controller.Top + 13), WiringValidation.PORT_2, 11, FenceTextAnchor.Start));
        return o;
    }

    /// <summary>노드 칩 — 원점 = 노드 가운데. 번호(장비 번호) · IP 테두리 · 선택 고리 · 저장 대기 표지.</summary>
    public static FenceChipPicture Node(ConceptNodeInfo node, bool selected)
    {
        var r = ConceptLayout.NODE_R;
        var hit = new Rect(-r - 5, -r - 12, 2 * r + 10, 2 * r + 17);
        var o = new List<FenceShape>(8) { new(FenceShapeKind.Rect, FenceInk.Hit, new[] { hit.TopLeft, hit.BottomRight }) };
        if (node.IsIp)
        {
            o.Add(new FenceShape(FenceShapeKind.Ellipse, FenceInk.ConceptNodeIp, new[] { new Point(0, 0) }, r + 3.5, r + 3.5));
            o.Add(Text(FenceInk.ConceptNodeSub, new Point(0, -r - 5), "IP", 8.5));
        }
        o.Add(new FenceShape(FenceShapeKind.Ellipse, FenceInk.ConceptNode, new[] { new Point(0, 0) }, r, r));
        var number = node.Number.ToString(CultureInfo.InvariantCulture);
        o.Add(Text(FenceInk.ConceptNodeText, new Point(0, 3.8), number, number.Length > 3 ? 8.5 : 10.5));
        if (node.IsNumberChanged || node.IsDraft)
            o.Add(new FenceShape(FenceShapeKind.Polygon, FenceInk.Draft, new[] { new Point(r - 1, -r - 1), new Point(r - 8, -r - 1), new Point(r - 1, -r + 6) }));
        if (selected) o.Add(new FenceShape(FenceShapeKind.Ellipse, FenceInk.Select, new[] { new Point(0, 0) }, r + 5.5, r + 5.5));
        return new FenceChipPicture(o, hit);
    }

    /// <summary>제어기 칩 — 원점 = 제어기 사각형 왼쪽 위. 포트 두 개(Ch1 · Ch2).</summary>
    public static FenceChipPicture Controller(ConceptGeometry g, bool selected)
    {
        var w = g.Controller.Width;
        var h = g.Controller.Height;
        var hit = new Rect(-6, -6, w + 12, h + 12);
        var o = new List<FenceShape>(8)
        {
            new(FenceShapeKind.Rect, FenceInk.Hit, new[] { hit.TopLeft, hit.BottomRight }),
            new(FenceShapeKind.Rect, FenceInk.ConceptController, new[] { new Point(0, 0), new Point(w, h) }, 5, 5),
            Text(FenceInk.ConceptControllerText, new Point(w / 2, h / 2 + 4.5), "제어기", 12.5),
            new(FenceShapeKind.Ellipse, FenceInk.ConceptPort, new[] { new Point(g.Port1.X - g.Controller.Left, 0) }, 4.5, 4.5),
            new(FenceShapeKind.Ellipse, FenceInk.ConceptPort, new[] { new Point(g.Port2.X - g.Controller.Left, 0) }, 4.5, 4.5),
        };
        if (selected) o.Add(new FenceShape(FenceShapeKind.Rect, FenceInk.Select, new[] { hit.TopLeft, hit.BottomRight }, 7, 7));
        return new FenceChipPicture(o, hit);
    }

    /// <summary>끄는 동안의 삽입 표지 — 세로 막대 + 위 화살(펜스 뷰와 같은 어휘).</summary>
    public static IReadOnlyList<FenceShape> Insertion(Point at)
        => new[]
        {
            new FenceShape(FenceShapeKind.Line, FenceInk.ConceptInsert, new[] { new Point(at.X, at.Y - 20), new Point(at.X, at.Y + 20) }),
            new FenceShape(FenceShapeKind.Polygon, FenceInk.ConceptInsert, new[] { new Point(at.X - 6, at.Y - 29), new Point(at.X + 6, at.Y - 29), new Point(at.X, at.Y - 21) }),
        };

    private static Point Toward(Point from, Point to, double distance)
    {
        var d = to - from;
        if (d.Length < 1e-6) return from;
        d.Normalize();
        return from + d * distance;
    }

    private static Rect Bounds(IEnumerable<Point> points)
    {
        var list = points.ToList();
        return new Rect(new Point(list.Min(p => p.X), list.Min(p => p.Y)), new Point(list.Max(p => p.X), list.Max(p => p.Y)));
    }

    private static FenceShape Text(FenceInk ink, Point at, string text, double size, FenceTextAnchor anchor = FenceTextAnchor.Middle)
        => new(FenceShapeKind.Text, ink, new[] { at }, Text: text, FontSize: size, Anchor: anchor);
}
