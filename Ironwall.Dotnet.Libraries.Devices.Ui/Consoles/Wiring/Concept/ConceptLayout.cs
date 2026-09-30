using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Concept;

/// <summary>개념도의 모양(FR-12) — 가로 띠(기본) · 원형.</summary>
public enum ConceptShape
{
    Strip = 0,
    Ring = 1,
}

/// <summary>개념도 노드 한 개의 자리.</summary>
/// <param name="Param">경로 위 위치(0…1) — 끌어 놓을 틈을 셀 때 쓴다(Ch1(A) 쪽이 0).</param>
public sealed record ConceptPoint(int Key, Point Center, double Param);

/// <summary>개념도 한 장의 배치 — 노드 · 제어기 · 두 포트(Ch1(A) · Ch2(B)) · 그림 전체 크기.</summary>
public sealed record ConceptGeometry(
    ConceptShape Shape,
    IReadOnlyList<ConceptPoint> Nodes,
    Rect Controller,
    Point Port1,
    Point Port2,
    Size Extent,
    Point Center,
    double Radius);

/// <summary>
/// 개념도 배치 — <b>순수 함수</b>(fence-wiring-editor FR-12 · NFR-01). 링을 <b>Ch1(A) → #1 … #N → Ch2(B)</b> 로 그린다(모든 제어기가 같은 그림 · §1-0).
/// </summary>
/// <remarks>
/// <para><b>가로 띠</b> — 노드를 한 줄로(간격 ≥ <see cref="STRIP_MIN_STEP"/>, 넓으면 칸에 맞춰 벌린다), 제어기는 아래 가운데, 두 포트에서 리턴케이블이 양 끝 노드로 간다.
/// 노드가 많으면 그림이 칸보다 넓어진다(가로로 이동).</para>
/// <para><b>원형</b> — 제어기는 아래 가운데, #1 은 Ch1 쪽(왼쪽 아래)에서 시작해 시계 방향으로 위를 돌아 #N 이 Ch2 쪽(오른쪽 아래)에 온다.</para>
/// </remarks>
public static class ConceptLayout
{
    public const double NODE_R = 11;
    public const double STRIP_MIN_STEP = 40;
    public const double STRIP_MARGIN = 48;
    public const double STRIP_NODE_Y = 44;
    public const double CONTROLLER_W = 108;
    public const double CONTROLLER_H = 30;

    /// <summary>원형에서 제어기 쪽에 비워 두는 각(도, 한쪽) — #1 과 #N 이 제어기 양옆에 온다.</summary>
    public const double RING_GAP_DEGREES = 26;

    public static ConceptGeometry Build(ConceptShape shape, IReadOnlyList<int> keys, Size available)
        => shape == ConceptShape.Ring ? Ring(keys, available) : Strip(keys, available);

    /// <summary>가로 띠.</summary>
    public static ConceptGeometry Strip(IReadOnlyList<int> keys, Size available)
    {
        var list = keys ?? Array.Empty<int>();
        var n = list.Count;
        var width = Math.Max(available.Width, 2 * STRIP_MARGIN + CONTROLLER_W);
        var step = n <= 1 ? 0 : Math.Max(STRIP_MIN_STEP, (width - 2 * STRIP_MARGIN) / (n - 1));
        var extentWidth = n <= 1 ? width : Math.Max(width, 2 * STRIP_MARGIN + step * (n - 1));
        var height = Math.Max(available.Height, 120);
        var nodes = new List<ConceptPoint>(n);
        var start = n <= 1 ? extentWidth / 2 : STRIP_MARGIN;
        for (var i = 0; i < n; i++)
            nodes.Add(new ConceptPoint(list[i], new Point(start + step * i, STRIP_NODE_Y), n <= 1 ? 0.5 : i / (double)(n - 1)));

        var controller = new Rect(extentWidth / 2 - CONTROLLER_W / 2, height - CONTROLLER_H - 12, CONTROLLER_W, CONTROLLER_H);
        var port1 = new Point(controller.Left + 14, controller.Top);
        var port2 = new Point(controller.Right - 14, controller.Top);
        return new ConceptGeometry(ConceptShape.Strip, nodes, controller, port1, port2, new Size(extentWidth, height), new Point(extentWidth / 2, height / 2), 0);
    }

    /// <summary>원형.</summary>
    public static ConceptGeometry Ring(IReadOnlyList<int> keys, Size available)
    {
        var list = keys ?? Array.Empty<int>();
        var n = list.Count;
        var width = Math.Max(available.Width, 200);
        var height = Math.Max(available.Height, 200);
        var radius = Math.Max(40, Math.Min(width, height - CONTROLLER_H - 20) / 2 - 30);
        var center = new Point(width / 2, radius + 26);
        var nodes = new List<ConceptPoint>(n);
        var sweep = 360 - 2 * RING_GAP_DEGREES;
        for (var i = 0; i < n; i++)
        {
            var t = n <= 1 ? 0.5 : i / (double)(n - 1);
            var degrees = 90 + RING_GAP_DEGREES + sweep * t;             // 화면 좌표(y 아래)에서 90° = 아래 · 시계 방향으로 증가
            var rad = degrees * Math.PI / 180;
            nodes.Add(new ConceptPoint(list[i], new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad)), t));
        }
        var controller = new Rect(center.X - CONTROLLER_W / 2, center.Y + radius + 8, CONTROLLER_W, CONTROLLER_H);
        var port1 = new Point(controller.Left + 14, controller.Top);
        var port2 = new Point(controller.Right - 14, controller.Top);
        var extent = new Size(width, Math.Max(height, controller.Bottom + 10));
        return new ConceptGeometry(ConceptShape.Ring, nodes, controller, port1, port2, extent, center, radius);
    }

    /// <summary>
    /// 포인터를 놓으면 들어갈 체인 틈(옮기기 <b>전</b> 목록 기준 0…N · <c>WiringChain.PlaceInBranch</c> 규칙) — 포인터보다 앞(경로 위)에 있는 노드 수.
    /// </summary>
    public static int GapAt(ConceptGeometry geometry, Point pointer)
    {
        var p = ParamAt(geometry, pointer);
        return geometry.Nodes.Count(node => node.Param < p);
    }

    /// <summary>포인터의 경로 위 위치(0…1) — 띠는 x, 원형은 각.</summary>
    public static double ParamAt(ConceptGeometry geometry, Point pointer)
    {
        var nodes = geometry.Nodes;
        if (nodes.Count <= 1) return nodes.Count == 1 && pointer.X > nodes[0].Center.X ? 1 : 0;
        if (geometry.Shape == ConceptShape.Strip)
        {
            var first = nodes[0].Center.X;
            var last = nodes[^1].Center.X;
            return (pointer.X - first) / Math.Max(1e-6, last - first);
        }
        var degrees = Math.Atan2(pointer.Y - geometry.Center.Y, pointer.X - geometry.Center.X) * 180 / Math.PI;   // −180…180, 90 = 아래
        var sweep = 360 - 2 * RING_GAP_DEGREES;
        var fromStart = ((degrees - (90 + RING_GAP_DEGREES)) % 360 + 360) % 360;
        // 제어기 쪽 빈 부채꼴 — 가까운 끝으로(왼쪽 절반은 #1 앞, 오른쪽 절반은 #N 뒤)
        if (fromStart > sweep) return fromStart > sweep + RING_GAP_DEGREES ? -0.01 : 1.01;
        return fromStart / sweep;
    }

    /// <summary>틈 <paramref name="gap"/> 의 삽입 표지 자리(두 이웃 노드의 가운데 · 양 끝은 한 칸 바깥).</summary>
    public static Point InsertionPoint(ConceptGeometry geometry, int gap)
    {
        var nodes = geometry.Nodes;
        if (nodes.Count == 0) return geometry.Center;
        var g = Math.Clamp(gap, 0, nodes.Count);
        if (g == 0) return nodes.Count > 1 ? Lerp(nodes[0].Center, nodes[1].Center, -0.5) : new Point(nodes[0].Center.X - 20, nodes[0].Center.Y);
        if (g == nodes.Count) return nodes.Count > 1 ? Lerp(nodes[^1].Center, nodes[^2].Center, -0.5) : new Point(nodes[^1].Center.X + 20, nodes[^1].Center.Y);
        var a = nodes[g - 1].Center;
        var b = nodes[g].Center;
        return new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
    }

    private static Point Lerp(Point a, Point b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
}
