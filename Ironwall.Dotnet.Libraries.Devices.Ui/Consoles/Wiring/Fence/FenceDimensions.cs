using Ironwall.Dotnet.Monitoring.Models.Fences;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;

/// <summary>
/// 거리(m) 표시(2026-10-01 · 사용자: "개념도나 3D 화면에는 센서간 거리 m 표시, 펜스나 담의 m 표시 해줘, on/off 되게") — <b>순수 함수</b>.
/// 2.5D 펜스 뷰는 땅 위 치수 두 줄: ① 망(담) 길이 줄 · ② 같은 줄 이웃 센서 사이 줄(아래 줄 · 위 줄 따로). 땅 번호 줄 아래에 두어 번호판 · 번호와 겹치지 않는다.
/// 작은 배율에서는 화면에서 이웃 글자와 겹치는 글자를 뺀다(처음 · 끝은 남긴다).
/// </summary>
public static class FenceDimensions
{
    /// <summary>망(담) 길이 줄의 깊이(세계 단위) — 땅 번호 줄(깊이 20) 아래.</summary>
    public const double PANEL_ROW_Z = 50;

    /// <summary>센서 사이 줄의 깊이 — 아래 줄 · 위 줄.</summary>
    public const double LOWER_GAP_ROW_Z = 80;
    public const double UPPER_GAP_ROW_Z = 108;

    /// <summary>치수 글자 크기(세계 단위) · 끝 눈금 반 길이 · 선과 글자 사이.</summary>
    public const double TEXT_SIZE = 9.5;
    public const double TICK = 3;
    public const double TEXT_DROP = 11;

    /// <summary>글자 사이 화면 틈(px).</summary>
    public const double GAP_PX = 6;

    /// <summary>
    /// 겹치는 글자 솎기 — 가운데 · 폭(같은 단위)을 받아 남길 것을 돌려준다(가운데 차례). 처음은 늘 남기고, 앞에 남긴 글자와 <paramref name="gap"/> 이상
    /// 떨어진 것만 남긴다. 끝도 남기되, 앞에 남긴 글자와 겹치면 그 글자를 뺀다(처음과 겹치면 처음만).
    /// </summary>
    public static bool[] Thin(IReadOnlyList<(double Center, double Width)> labels, double gap)
    {
        var n = labels?.Count ?? 0;
        var keep = new bool[n];
        if (n == 0) return keep;
        var order = Enumerable.Range(0, n).OrderBy(i => labels![i].Center).ToList();
        double Left(int i) => labels![i].Center - labels[i].Width / 2;
        double Right(int i) => labels![i].Center + labels[i].Width / 2;

        var kept = new List<int> { order[0] };
        keep[order[0]] = true;
        for (var j = 1; j < order.Count; j++)
        {
            var i = order[j];
            if (Left(i) >= Right(kept[^1]) + gap) { keep[i] = true; kept.Add(i); }
        }
        var last = order[^1];
        if (!keep[last] && n > 1)
        {
            while (kept.Count > 1 && Left(last) < Right(kept[^1]) + gap) { keep[kept[^1]] = false; kept.RemoveAt(kept.Count - 1); }
            if (Left(last) >= Right(kept[^1]) + gap) keep[last] = true;
        }
        return keep;
    }

    /// <summary>지금 배율에서 치수 글자(주 글자 — 늘 읽힌다)가 그려지는 크기(세계 단위) — <see cref="FenceScene.Legible"/> 와 같은 규칙.</summary>
    public static double RenderedSize(double size, double zoom)
    {
        var z = zoom > 0.05 ? zoom : 0.05;
        return size * z >= FenceScene.MIN_TEXT ? size : FenceScene.MIN_TEXT / z;
    }

    /// <summary>
    /// 펜스 뷰 치수 — 망 길이 줄 + 같은 줄 이웃 센서 사이 줄(선 · 끝 눈금 · 가운데 글자). 센서 x 는 그림 자리(<paramref name="sensorX"/>), 글자는 실제 m.
    /// </summary>
    public static IReadOnlyList<FenceShape> Build(FenceGeometry geometry, IReadOnlyList<FenceLaneGap> gaps, IReadOnlyDictionary<int, double> sensorX,
                                                  double upm, FenceProjector p, double zoom)
    {
        var o = new List<FenceShape>();
        if (geometry is null) return o;
        var size = RenderedSize(TEXT_SIZE, zoom);
        var gap = GAP_PX / (zoom > 0.05 ? zoom : 0.05);

        Row(geometry.Panels.Select(panel => (panel.StartM * upm, panel.EndM * upm, FenceLayoutMath.MetresText(panel.SpanM))).ToList(), PANEL_ROW_Z);
        foreach (var lane in new[] { FenceLane.Lower, FenceLane.Upper })
        {
            var row = (gaps ?? Array.Empty<FenceLaneGap>())
                .Where(g => g.Lane == lane && sensorX.ContainsKey(g.LeftKey) && sensorX.ContainsKey(g.RightKey))
                .Select(g => (sensorX[g.LeftKey], sensorX[g.RightKey], FenceLayoutMath.MetresText(g.Metres))).ToList();
            Row(row, lane == FenceLane.Lower ? LOWER_GAP_ROW_Z : UPPER_GAP_ROW_Z);
        }
        return o;

        void Row(IReadOnlyList<(double A, double B, string Text)> spans, double z)
        {
            var labels = spans.Select(s => ((s.A + s.B) / 2, FenceScene.EstimateWidth(s.Text, size))).ToList();
            var keep = Thin(labels, gap);
            for (var i = 0; i < spans.Count; i++)
            {
                var (a, b, text) = spans[i];
                var pa = p.P(a, 0, z);
                var pb = p.P(b, 0, z);
                o.Add(new FenceShape(FenceShapeKind.Line, FenceInk.DimLine, new[] { pa, pb }));
                o.Add(new FenceShape(FenceShapeKind.Line, FenceInk.DimLine, new[] { new Point(pa.X, pa.Y - TICK), new Point(pa.X, pa.Y + TICK) }));
                o.Add(new FenceShape(FenceShapeKind.Line, FenceInk.DimLine, new[] { new Point(pb.X, pb.Y - TICK), new Point(pb.X, pb.Y + TICK) }));
                if (keep[i])
                    o.Add(new FenceShape(FenceShapeKind.Text, FenceInk.DimText, new[] { new Point((pa.X + pb.X) / 2, (pa.Y + pb.Y) / 2 + TEXT_DROP) }, Text: text, FontSize: TEXT_SIZE));
            }
        }
    }
}
