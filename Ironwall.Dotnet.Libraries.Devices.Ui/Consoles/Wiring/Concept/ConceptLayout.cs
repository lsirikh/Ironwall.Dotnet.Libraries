using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Concept;

/// <summary>칩을 얼마나 자세히 그리나 — 칸이 넓으면 번호까지, 좁으면 작은 칩, 아주 좁으면 점(센서 수백 대).</summary>
public enum ConceptChipMode
{
    /// <summary>칩 안 번호 + 칩 위 번호(그림 ①).</summary>
    Full = 0,
    /// <summary>작은 칩 · 칩 위 번호는 솎는다(그림 ② · ③).</summary>
    Compact = 1,
    /// <summary>점 칩만(번호 · 신호등 없음 · 200대 이상).</summary>
    Dot = 2,
}

/// <summary>개념도에 놓을 센서 한 대 — 줄 · 펜스 위 가로 위치(m) · 칩 위 번호 글자(간격 · 솎기 판단 · 없으면 세 자리로 본다).</summary>
public sealed record ConceptLaneItem(int Key, FenceLane Lane, double XM, string? Label = null);

/// <summary>개념도 노드 한 개의 자리.</summary>
/// <param name="ShowLabel">칩 위 번호를 그리는가(솎은 결과).</param>
public sealed record ConceptPoint(int Key, FenceLane Lane, Point Center, bool ShowLabel);

/// <summary>아래 눈금 하나 — 펜스 위 위치(왼쪽부터 1).</summary>
public sealed record ConceptTick(double X, int Position);

/// <summary>
/// 두 줄 개념도 한 장의 배치(fence-wiring-editor v0.3 FR-20 · 참고 그림 4장) — 펜스 격자 · 두 줄 · 칩 · 제어기 <c>C</c> · 눈금.
/// </summary>
public sealed record ConceptGeometry(
    FenceControllerEnd End,
    IReadOnlyList<ConceptPoint> Nodes,
    Rect Controller,
    Point Port1,
    Point Port2,
    double FenceLeft,
    double FenceRight,
    double UpperY,
    double LowerY,
    double FenceTop,
    double FenceBottom,
    double TickY,
    IReadOnlyList<double> Posts,
    IReadOnlyList<ConceptTick> Ticks,
    ConceptChipMode Mode,
    Size Chip,
    Size Extent)
{
    /// <summary>먼 끝(꺾임선) x — 제어기 반대쪽 펜스 끝.</summary>
    public double FarX => End == FenceControllerEnd.Left ? FenceRight : FenceLeft;

    /// <summary>제어기 쪽 펜스 끝 x.</summary>
    public double NearX => End == FenceControllerEnd.Left ? FenceLeft : FenceRight;

    /// <summary>줄의 y.</summary>
    public double LaneY(FenceLane lane) => lane == FenceLane.Upper ? UpperY : LowerY;
}

/// <summary>
/// 두 줄 개념도 배치 — <b>순수 함수</b>(fence-wiring-editor v0.3 FR-20 · NFR-01). 참고 그림처럼 펜스(격자 · 기둥)를 가로로 펴고 위 · 아래 두 줄에 칩을 놓는다.
/// 제어기 <c>C</c> 는 왼쪽 끝 또는 오른쪽 끝 — Ch1(실선)은 아래 줄로, Ch2(점선)는 위 줄로(또는 리턴선) 들어간다.
/// </summary>
/// <remarks>
/// <para><b>칸에 맞춘다</b> — 펜스 전체 길이를 보이는 폭에 눌러 담는다(가로로 잘리지 않는다). 칸이 좁아지면 칩을 줄이고(<see cref="ConceptChipMode"/>)
/// 칩 위 번호 · 아래 눈금을 솎는다(그림 ② 65대: 눈금 1 · 11 · 21 …).</para>
/// <para>눈금은 펜스 위 위치를 <b>왼쪽부터</b> 센다(제어기가 오른쪽이어도 · 그림 ④).</para>
/// </remarks>
public static class ConceptLayout
{
    public const double CONTROLLER_W = 30;
    public const double CONTROLLER_H = 20;
    /// <summary>제어기와 펜스 사이.</summary>
    public const double CONTROLLER_GAP = 26;
    public const double SIDE_MARGIN = 10;
    /// <summary>먼 끝 바깥 여백(칩 반 폭 · 꺾임선).</summary>
    public const double FAR_MARGIN = 22;

    public const double UPPER_Y = 44;
    public const double LOWER_Y = 96;
    public const double MIN_HEIGHT = 140;

    /// <summary>두 줄 사이의 가장 좁은 · 가장 넓은 간격(px) — 칸이 높아지면 줄 간격이 자란다(칩 · 글자 크기는 그대로).</summary>
    public const double MIN_LANE_GAP = LOWER_Y - UPPER_Y;
    public const double MAX_LANE_GAP = 220;

    /// <summary>위 줄 위 · 아래 줄 아래에 남기는 높이(px) — 칩 위 번호 · 펜스 테두리 · 눈금 글자 자리.</summary>
    public const double TOP_ROOM = UPPER_Y;
    public const double BOTTOM_ROOM = MIN_HEIGHT - LOWER_Y;

    /// <summary>
    /// 높이 <paramref name="height"/> 에서 두 줄의 y — 기본(140)은 44 · 96 그대로, 높아지면 줄 간격이 <see cref="MAX_LANE_GAP"/> 까지 자라고
    /// 남는 높이는 위아래로 나눠 가운데에 둔다(렌더 검토: 개념도가 커져도 칩만 작게 남지 않게).
    /// </summary>
    public static (double Upper, double Lower) LaneYs(double height)
    {
        var h = double.IsFinite(height) ? Math.Max(height, MIN_HEIGHT) : MIN_HEIGHT;
        var gap = Math.Clamp(h - TOP_ROOM - BOTTOM_ROOM, MIN_LANE_GAP, MAX_LANE_GAP);
        var upper = TOP_ROOM + Math.Max(0, h - TOP_ROOM - BOTTOM_ROOM - gap) / 2;
        return (upper, upper + gap);
    }

    public const double FULL_STEP = 40;
    public const double COMPACT_STEP = 18;
    public static readonly Size FULL_CHIP = new(34, 16);

    /// <summary>칩 위 번호를 솎을 때 번호 사이 최소 간격(px).</summary>
    public const double LABEL_SPACING = 36;

    /// <summary>칩 위 번호 글자 크기(px).</summary>
    public const double LABEL_SIZE = 11;

    /// <summary>
    /// 칩 위 번호의 폭(px · 여백 4 포함) — 숫자는 어림(0.58em)보다 넓게 그려져 1.15 배로 본다(헤디드 r21: 다섯 자리 번호 "70066" 끼리 36px 간격에서 겹쳤다).
    /// </summary>
    public static double LabelWidth(string? label) => FenceScene.EstimateWidth(string.IsNullOrEmpty(label) ? "000" : label, LABEL_SIZE) * 1.15 + 4;

    /// <summary>눈금 글자 사이 최소 간격(px) — 이보다 좁으면 5 · 10 · 20 … 칸마다.</summary>
    public const double TICK_SPACING = 120;

    /// <summary>눈금을 다 보이는 최대 칸 수(그림 ①: 6칸 모두).</summary>
    public const int TICK_ALL_MAX = 12;

    /// <param name="items">센서(줄 · 펜스 위 x m).</param>
    /// <param name="postsM">기둥 x(m) — 없으면 격자만.</param>
    /// <param name="lengthM">펜스 전체 길이(m) — 0 이면 센서 x 의 최댓값.</param>
    public static ConceptGeometry Build(IReadOnlyList<ConceptLaneItem> items, IReadOnlyList<double> postsM, double lengthM, FenceControllerEnd end, Size available)
    {
        var list = items ?? Array.Empty<ConceptLaneItem>();
        var posts = postsM ?? Array.Empty<double>();
        var width = Math.Max(available.Width, 240);
        var height = Math.Max(available.Height, MIN_HEIGHT);
        var length = lengthM > 0 ? lengthM : Math.Max(1, list.Count == 0 ? 1 : list.Max(i => i.XM));
        var (upperY, lowerY) = LaneYs(height);

        double fenceLeft, fenceRight;
        Rect controller;
        var cTop = (upperY + lowerY) / 2 - CONTROLLER_H / 2;
        if (end == FenceControllerEnd.Left)
        {
            controller = new Rect(SIDE_MARGIN, cTop, CONTROLLER_W, CONTROLLER_H);
            fenceLeft = controller.Right + CONTROLLER_GAP;
            fenceRight = width - FAR_MARGIN;
        }
        else
        {
            controller = new Rect(width - SIDE_MARGIN - CONTROLLER_W, cTop, CONTROLLER_W, CONTROLLER_H);
            fenceLeft = FAR_MARGIN;
            fenceRight = controller.Left - CONTROLLER_GAP;
        }
        var scale = Math.Max(1e-6, fenceRight - fenceLeft) / length;
        double X(double m) => fenceLeft + Math.Clamp(m, 0, length) * scale;

        // 칸 폭 — 줄마다 이웃 칩 사이(같은 자리는 뺀다)의 가장 좁은 값
        var step = double.PositiveInfinity;
        foreach (var lane in new[] { FenceLane.Lower, FenceLane.Upper })
        {
            var xs = list.Where(i => i.Lane == lane).Select(i => X(i.XM)).OrderBy(x => x).ToList();
            for (var i = 1; i < xs.Count; i++)
                if (xs[i] - xs[i - 1] > 0.5) step = Math.Min(step, xs[i] - xs[i - 1]);
        }
        var mode = step >= FULL_STEP ? ConceptChipMode.Full : step >= COMPACT_STEP ? ConceptChipMode.Compact : ConceptChipMode.Dot;
        var chip = mode switch
        {
            ConceptChipMode.Full => FULL_CHIP,
            ConceptChipMode.Compact => new Size(Math.Min(24, step - 6), 10),
            _ => new Size(Math.Max(2, Math.Min(8, step - 1)), 8),
        };

        // 같은 자리 칩은 옆으로 벌린다(줄마다 · 순서 유지) — 큰 칩은 번호가 서로 닿지 않을 만큼(가장 넓은 번호 폭)
        var widestLabel = list.Count == 0 ? LabelWidth(null) : list.Max(i => LabelWidth(i.Label));
        var nodes = new List<ConceptPoint>(list.Count);
        foreach (var lane in new[] { FenceLane.Lower, FenceLane.Upper })
        {
            var row = list.Where(i => i.Lane == lane).Select((item, index) => (item, index)).OrderBy(t => t.item.XM).ThenBy(t => t.index).Select(t => t.item).ToList();
            // 같은 자리만 벌린다 — 칸보다 넓게 벌리면 펜스 밖으로 밀려난다(점 칩 · 수백 대)
            var gap = mode == ConceptChipMode.Full
                ? Math.Max(chip.Width + 2, widestLabel)
                : double.IsInfinity(step) ? chip.Width + 2 : Math.Min(chip.Width + 2, step);
            var spread = FenceWorld.Separate(row.Select(i => X(i.XM)).ToList(), gap);
            var every = mode == ConceptChipMode.Full ? 1 : mode == ConceptChipMode.Compact ? Math.Max(1, (int)Math.Ceiling(LABEL_SPACING / Math.Max(1, step))) : int.MaxValue;
            // 번호는 실제 간격으로 한 번 더 솎는다 — 앞에 그린 번호와 반 폭씩 닿으면 뺀다(글자끼리 겹치지 않게)
            double? lastX = null;
            var lastW = 0.0;
            for (var i = 0; i < row.Count; i++)
            {
                var w = LabelWidth(row[i].Label);
                var show = i % every == 0 && (lastX is not { } lx || spread[i] - lx >= (lastW + w) / 2);
                if (show) { lastX = spread[i]; lastW = w; }
                nodes.Add(new ConceptPoint(row[i].Key, lane, new Point(spread[i], lane == FenceLane.Upper ? upperY : lowerY), show));
            }
        }

        // 눈금 — 펜스 위 위치(센서가 선 가로 자리 · 왼쪽부터 1). 칸이 많으면 5 · 10 · 20 … 칸마다.
        var columns = nodes.Select(n => Math.Round(n.Center.X * 2) / 2).Distinct().OrderBy(x => x).ToList();
        var ticks = new List<ConceptTick>();
        if (columns.Count > 0)
        {
            var spacing = columns.Count > 1 ? (columns[^1] - columns[0]) / (columns.Count - 1) : double.PositiveInfinity;
            var every = 1;
            if (columns.Count > TICK_ALL_MAX)
                foreach (var k in new[] { 5, 10, 20, 50, 100, 200, 500 })
                {
                    every = k;
                    if (k * spacing >= TICK_SPACING) break;
                }
            for (var i = 0; i < columns.Count; i += every) ticks.Add(new ConceptTick(columns[i], i + 1));
        }

        var port1 = new Point(controller.X + controller.Width / 2, controller.Bottom);
        var port2 = new Point(controller.X + controller.Width / 2, controller.Top);
        var fenceTop = upperY - 14;
        var fenceBottom = lowerY + 16;
        return new ConceptGeometry(end, nodes, controller, port1, port2, fenceLeft, fenceRight, upperY, lowerY, fenceTop, fenceBottom, fenceBottom + 16,
                                   posts.Select(X).ToList(), ticks, mode, chip, new Size(width, height));
    }

    /// <summary>포인터가 가리키는 줄 — 두 줄 가운데보다 위면 위 줄.</summary>
    public static FenceLane LaneAt(ConceptGeometry geometry, Point pointer)
        => pointer.Y < (geometry.UpperY + geometry.LowerY) / 2 ? FenceLane.Upper : FenceLane.Lower;

    /// <summary>놓을 틈 — 그 줄에서 끄는 것을 뺀 칩 중 포인터보다 왼쪽인 수(왼쪽부터 0…).</summary>
    public static int IndexAt(ConceptGeometry geometry, FenceLane lane, Point pointer, IReadOnlyCollection<int>? excluding = null)
        => geometry.Nodes.Count(n => n.Lane == lane && (excluding is null || !excluding.Contains(n.Key)) && n.Center.X < pointer.X);

    /// <summary>틈 표지 자리 — 그 줄에서 이웃 둘의 가운데(양 끝은 칩 한 개 바깥).</summary>
    public static Point InsertionPoint(ConceptGeometry geometry, FenceLane lane, int index, IReadOnlyCollection<int>? excluding = null)
    {
        var row = geometry.Nodes.Where(n => n.Lane == lane && (excluding is null || !excluding.Contains(n.Key))).OrderBy(n => n.Center.X).ToList();
        var y = geometry.LaneY(lane);
        if (row.Count == 0) return new Point((geometry.FenceLeft + geometry.FenceRight) / 2, y);
        var i = Math.Clamp(index, 0, row.Count);
        var half = geometry.Chip.Width / 2 + 4;
        if (i == 0) return new Point(Math.Max(geometry.FenceLeft - half, row[0].Center.X - half), y);
        if (i == row.Count) return new Point(row[^1].Center.X + half, y);
        return new Point((row[i - 1].Center.X + row[i].Center.X) / 2, y);
    }

    /// <summary>
    /// VBus 표지 자리(FR-21) — 사슬 틈 <paramref name="gap"/>(k = k번째 센서 뒤)의 두 칩 가운데. 꺾이는 곳(줄이 다르면)은 먼 끝 꺾임선 가운데.
    /// </summary>
    public static Point VbusPoint(ConceptGeometry geometry, IReadOnlyList<int> chain, int gap)
    {
        if (chain is null || chain.Count < 2) return new Point(geometry.FarX, (geometry.UpperY + geometry.LowerY) / 2);
        var g = Math.Clamp(gap, 1, chain.Count - 1);
        var a = geometry.Nodes.FirstOrDefault(n => n.Key == chain[g - 1]);
        var b = geometry.Nodes.FirstOrDefault(n => n.Key == chain[g]);
        if (a is null || b is null) return new Point(geometry.FarX, (geometry.UpperY + geometry.LowerY) / 2);
        if (a.Lane != b.Lane) return new Point(geometry.FarX, (geometry.UpperY + geometry.LowerY) / 2);
        return new Point((a.Center.X + b.Center.X) / 2, a.Center.Y);
    }

    /// <summary>VBus 를 놓을 사슬 틈 — 포인터에 가장 가까운 틈 표지(1…N−1).</summary>
    public static int VbusGapAt(ConceptGeometry geometry, IReadOnlyList<int> chain, Point pointer)
    {
        if (chain is null || chain.Count < 2) return 0;
        var best = 1;
        var distance = double.PositiveInfinity;
        for (var g = 1; g < chain.Count; g++)
        {
            var p = VbusPoint(geometry, chain, g);
            var d = (p - pointer).LengthSquared;
            if (d < distance) { distance = d; best = g; }
        }
        return best;
    }
}
