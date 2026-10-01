using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Concept;

/// <summary>
/// 두 줄 개념도 그림(fence-wiring-editor v0.3 FR-20 · 참고 그림 4장) — <b>순수</b> 그림 목록(<see cref="FenceShape"/>). 렌더러가 잉크를 토큰으로 풀어 그린다.
/// </summary>
/// <remarks>
/// <para>펜스 격자(테두리 · 망 무늬 · 기둥) 위에 두 줄. Ch1 = 실선(정보 계열 색) — 제어기에서 아래 줄을 따라 먼 끝까지. 먼 끝에서 위로 꺾여
/// 위 줄을 제어기 쪽으로 돌아오고, 센서가 없는 구간 · 제어기까지는 Ch2 = 점선(앰버 계열 · <c>{6,4}</c> — 러버밴드 <c>{5,3}</c> · 그룹 <c>{4,3}</c> 과 겹치지 않는 어휘).
/// 선은 색만이 아니라 <b>실선/점선</b>으로 가른다.</para>
/// </remarks>
public static class ConceptScene
{
    /// <summary>바탕 — 땅선 · 펜스 격자 · 기둥 · Ch1 · 꺾임선 · 위 줄/리턴선 · 칩 위 번호 · 아래 눈금 · Ch1/Ch2 글자.</summary>
    public static IReadOnlyList<FenceShape> Background(ConceptGeometry g, IReadOnlyList<ConceptNodeInfo> nodes)
    {
        var o = new List<FenceShape>(256);
        var info = (nodes ?? Array.Empty<ConceptNodeInfo>()).ToDictionary(n => n.Key);

        // 땅선(그림 전체 폭) · 펜스 격자
        o.Add(Line(FenceInk.ConceptGround, new Point(0, g.FenceBottom), new Point(g.Extent.Width, g.FenceBottom)));
        o.Add(new FenceShape(FenceShapeKind.Rect, FenceInk.ConceptFence, new[] { new Point(g.FenceLeft, g.FenceTop), new Point(g.FenceRight, g.FenceBottom) }));
        var meshStep = Math.Max(8, Math.Min(14, (g.FenceRight - g.FenceLeft) / 120));
        for (var x = g.FenceLeft + meshStep; x < g.FenceRight - 1; x += meshStep)
            o.Add(Line(FenceInk.ConceptMesh, new Point(x, g.FenceTop), new Point(x, g.FenceBottom)));
        for (var y = g.FenceTop + meshStep; y < g.FenceBottom - 1; y += meshStep)
            o.Add(Line(FenceInk.ConceptMesh, new Point(g.FenceLeft, y), new Point(g.FenceRight, y)));
        foreach (var x in g.Posts)
            o.Add(Line(FenceInk.ConceptPost, new Point(x, g.FenceTop - 4), new Point(x, g.FenceBottom)));

        // Ch1 — 제어기 아래 포트 → 아래 줄 → 먼 끝(실선)
        var cx = g.Port1.X;
        o.Add(new FenceShape(FenceShapeKind.Polyline, FenceInk.ConceptCh1, new[] { g.Port1, new Point(cx, g.LowerY), new Point(g.FarX, g.LowerY) }));

        // 위 줄 — 센서가 있으면 먼 끝 → 제어기에 가장 가까운 위 줄 칩까지 실선, 그 뒤 제어기까지 점선. 없으면 처음부터 점선(리턴선 · 그림 ②)
        var upper = g.Nodes.Where(n => n.Lane == FenceLane.Upper).ToList();
        var hasUpper = upper.Count > 0;
        o.Add(Line(hasUpper ? FenceInk.ConceptCh2 : FenceInk.ConceptCh2Dash, new Point(g.FarX, g.LowerY), new Point(g.FarX, g.UpperY)));   // 꺾임선
        var near = hasUpper
            ? (g.End == FenceControllerEnd.Left ? upper.Min(n => n.Center.X) : upper.Max(n => n.Center.X))
            : g.FarX;
        if (hasUpper) o.Add(Line(FenceInk.ConceptCh2, new Point(g.FarX, g.UpperY), new Point(near, g.UpperY)));
        o.Add(new FenceShape(FenceShapeKind.Polyline, FenceInk.ConceptCh2Dash, new[] { new Point(near, g.UpperY), new Point(cx, g.UpperY), g.Port2 }));

        // 포트 글자 — 제어기 옆(색이 아니라 글자로도)
        var side = g.End == FenceControllerEnd.Left ? FenceTextAnchor.Start : FenceTextAnchor.End;
        var dx = g.End == FenceControllerEnd.Left ? 6 : -6;
        o.Add(Text(FenceInk.ConceptPortText, new Point(cx + dx, g.LowerY - 4), "Ch1", 9.5, side));
        o.Add(Text(FenceInk.ConceptPortText, new Point(cx + dx, g.UpperY - 4), "Ch2", 9.5, side));

        // 칩 위 번호(솎은 것만)
        if (g.Mode != ConceptChipMode.Dot)
            foreach (var node in g.Nodes.Where(n => n.ShowLabel && info.ContainsKey(n.Key)))
                o.Add(Text(node.Lane == FenceLane.Upper ? FenceInk.ConceptLabelUpper : FenceInk.ConceptLabelLower,
                           new Point(node.Center.X, node.Center.Y - g.Chip.Height / 2 - 4), info[node.Key].Number.ToString(CultureInfo.InvariantCulture), 11));

        // 아래 눈금 — 펜스 위 위치(왼쪽부터)
        foreach (var tick in g.Ticks)
        {
            o.Add(Line(FenceInk.ConceptTick, new Point(tick.X, g.FenceBottom), new Point(tick.X, g.FenceBottom + 4)));
            o.Add(Text(FenceInk.ConceptTickText, new Point(tick.X, g.TickY), tick.Position.ToString(CultureInfo.InvariantCulture), 10.5));
        }
        return o;
    }

    /// <summary>센서 칩 — 원점 = 칩 가운데. 줄마다 칠(아래 = 정보 계열 · 위 = 앰버 계열) + 번호(자세히일 때) · 저장 대기 표지 · 선택 고리.</summary>
    public static FenceChipPicture Node(ConceptNodeInfo node, FenceLane lane, ConceptChipMode mode, Size chip, bool selected)
    {
        var w = chip.Width;
        var h = chip.Height;
        var hw = Math.Max(w / 2 + 3, 7);
        var hh = Math.Max(h / 2 + 3, 7);
        var hit = new Rect(-hw, -hh, 2 * hw, 2 * hh);
        var o = new List<FenceShape>(6) { new(FenceShapeKind.Rect, FenceInk.Hit, new[] { hit.TopLeft, hit.BottomRight }) };
        var radius = Math.Min(4, h / 2);
        o.Add(new FenceShape(FenceShapeKind.Rect, lane == FenceLane.Upper ? FenceInk.ConceptChipUpper : FenceInk.ConceptChipLower,
                             new[] { new Point(-w / 2, -h / 2), new Point(w / 2, h / 2) }, radius, radius));
        if (mode == ConceptChipMode.Full)
        {
            var number = node.Number.ToString(CultureInfo.InvariantCulture);
            o.Add(Text(lane == FenceLane.Upper ? FenceInk.ConceptChipTextUpper : FenceInk.ConceptChipTextLower, new Point(0, 3.6), number, number.Length > 3 ? 8.5 : 9.5));
        }
        if (node.IsNumberChanged || node.IsDraft)
            o.Add(new FenceShape(FenceShapeKind.Polygon, FenceInk.Draft, new[] { new Point(w / 2 - 6, -h / 2 - 1), new Point(w / 2 + 1, -h / 2 - 1), new Point(w / 2 + 1, -h / 2 + 6) }));
        if (selected) o.Add(new FenceShape(FenceShapeKind.Rect, FenceInk.Select, new[] { new Point(-w / 2 - 3, -h / 2 - 3), new Point(w / 2 + 3, h / 2 + 3) }, radius + 2, radius + 2));
        return new FenceChipPicture(o, hit);
    }

    /// <summary>제어기 칩 <c>C</c> — 원점 = 사각형 왼쪽 위.</summary>
    public static FenceChipPicture Controller(ConceptGeometry g, bool selected)
    {
        var w = g.Controller.Width;
        var h = g.Controller.Height;
        var hit = new Rect(-5, -5, w + 10, h + 10);
        var o = new List<FenceShape>(4)
        {
            new(FenceShapeKind.Rect, FenceInk.Hit, new[] { hit.TopLeft, hit.BottomRight }),
            new(FenceShapeKind.Rect, FenceInk.ConceptController, new[] { new Point(0, 0), new Point(w, h) }, 4, 4),
            Text(FenceInk.ConceptControllerText, new Point(w / 2, h / 2 + 4.2), "C", 12),
        };
        if (selected) o.Add(new FenceShape(FenceShapeKind.Rect, FenceInk.Select, new[] { new Point(-3, -3), new Point(w + 3, h + 3) }, 6, 6));
        return new FenceChipPicture(o, hit);
    }

    /// <summary>VBus 표지 칩(FR-21 · 표시 전용) — 원점 = 가운데.</summary>
    public static FenceChipPicture Vbus(bool selected)
    {
        const double W = 34, H = 14;
        var hit = new Rect(-W / 2 - 3, -H / 2 - 3, W + 6, H + 6);
        var o = new List<FenceShape>(4)
        {
            new(FenceShapeKind.Rect, FenceInk.Hit, new[] { hit.TopLeft, hit.BottomRight }),
            new(FenceShapeKind.Rect, FenceInk.ConceptVbus, new[] { new Point(-W / 2, -H / 2), new Point(W / 2, H / 2) }, 3, 3),
            Text(FenceInk.ConceptVbusText, new Point(0, 3.4), "VBus", 9),
        };
        if (selected) o.Add(new FenceShape(FenceShapeKind.Rect, FenceInk.Select, new[] { new Point(-W / 2 - 3, -H / 2 - 3), new Point(W / 2 + 3, H / 2 + 3) }, 5, 5));
        return new FenceChipPicture(o, hit);
    }

    /// <summary>끄는 동안의 삽입 표지 — 세로 막대 + 위 화살(펜스 뷰와 같은 어휘).</summary>
    public static IReadOnlyList<FenceShape> Insertion(Point at)
        => new[]
        {
            new FenceShape(FenceShapeKind.Line, FenceInk.ConceptInsert, new[] { new Point(at.X, at.Y - 13), new Point(at.X, at.Y + 13) }),
            new FenceShape(FenceShapeKind.Polygon, FenceInk.ConceptInsert, new[] { new Point(at.X - 5, at.Y - 21), new Point(at.X + 5, at.Y - 21), new Point(at.X, at.Y - 14) }),
        };

    /// <summary>제어기를 끄는 동안 — 놓을 쪽 끝의 윤곽(점선 아님 · 굵은 테두리).</summary>
    public static IReadOnlyList<FenceShape> ControllerTarget(Rect at)
        => new[] { new FenceShape(FenceShapeKind.Rect, FenceInk.ConceptTarget, new[] { at.TopLeft, at.BottomRight }, 4, 4) };

    private static FenceShape Line(FenceInk ink, Point a, Point b) => new(FenceShapeKind.Line, ink, new[] { a, b });

    private static FenceShape Text(FenceInk ink, Point at, string text, double size, FenceTextAnchor anchor = FenceTextAnchor.Middle)
        => new(FenceShapeKind.Text, ink, new[] { at }, Text: text, FontSize: size, Anchor: anchor);
}
