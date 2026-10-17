using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;

/****************************************************************************
   Purpose      : 부대 기호 기하 — APP-6(D) 아군 지상 틀 + 제대 표지의 경로 데이터 (FR-20 · FR-22)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>제대 표지의 모양.</summary>
public enum UnitMarkKind
{
    None = 0,

    /// <summary>X — 사단 XX(여단 이상).</summary>
    Crosses = 1,

    /// <summary>세로선 — 연대 ||| · 대대 || · 중대 |.</summary>
    Bars = 2,

    /// <summary>점 — 소대(소초) ●●●.</summary>
    Dots = 3,

    /// <summary>서버가 모르는 제대 — "?"(끌 수 없다, 부모 FR-19).</summary>
    Unknown = 4,
}

/// <summary>제대 표지 — 모양과 개수.</summary>
public sealed record UnitEchelonMark(UnitMarkKind Kind, int Count)
{
    /// <summary>글자로 쓴 표지(툴팁 · 대조용). 그림은 경로로 그린다.</summary>
    public string Glyph => Kind switch
    {
        UnitMarkKind.Crosses => new string('X', Count),
        UnitMarkKind.Bars => new string('|', Count),
        UnitMarkKind.Dots => new string('●', Count),
        UnitMarkKind.Unknown => "?",
        _ => string.Empty,
    };
}

/// <summary>
/// 한 단계 · 한 제대의 기호 한 벌. 좌표는 <b>노드 요소 기준</b>(원점 = 노드 요소 왼쪽 위).
/// </summary>
/// <param name="Level">단계.</param>
/// <param name="Box">노드 요소 크기.</param>
/// <param name="Center">노드 요소 안의 "부대 위치" 점 — 캔버스는 이 점을 월드 위치 × 배율에 맞춘다.</param>
/// <param name="Frame">아군 지상 틀(반 픽셀 정렬).</param>
/// <param name="FramePathData">틀 경로 — 닫힌 사각 하나.</param>
/// <param name="Mark">제대 표지.</param>
/// <param name="MarkStrokePathData">표지 중 선(X · |). L0 이거나 모르는 제대면 빈 문자열.</param>
/// <param name="MarkDotPathData">표지 중 채운 점(●). 없으면 빈 문자열.</param>
/// <param name="MarkAnchor">표지 자리의 밑변 가운데 — 모르는 제대의 "?" 를 호출부가 이 자리에 쓴다.</param>
/// <param name="BranchPathData">틀 안의 병과 기호. <b>언제나 빈 문자열</b>(API 에 병과 필드 없음 — 결정 #5 · S-3).</param>
public sealed record UnitSymbolShape(
    UnitMapLevel Level,
    Size Box,
    Point Center,
    Rect Frame,
    string FramePathData,
    UnitEchelonMark Mark,
    string MarkStrokePathData,
    string MarkDotPathData,
    Point MarkAnchor,
    string BranchPathData);

/// <summary>
/// 부대 기호의 기하. 경로 <b>문자열</b>만 만든다 — <c>StreamGeometry</c> 는 호출부(노드)가 <c>Geometry.Parse</c> 로 만든다.
/// </summary>
/// <remarks>
/// <para>지도의 APP-6 마커(<c>GMaps.Ui</c>)는 이 프로젝트가 참조하지 않아 가져올 수 없다 — 모양만 맞추고 벡터로 새로 그린다(SB S10).
/// 표지 대응은 시험이 지도 변환기 소스를 <b>문자열로</b> 대조한다(참조 없이).</para>
/// <para>도형 크기는 단계마다 <b>화면에서 고정</b>이다(FR-21) — 배율은 간격만 바꾼다. 그래서 여기에는 배율이 없다.</para>
/// </remarks>
public static class UnitSymbolGeometry
{
    #region - 상수 -
    /// <summary>L1 틀 30×20.</summary>
    public static readonly Size L1Frame = new(30, 20);

    /// <summary>L2 틀 36×24.</summary>
    public static readonly Size L2Frame = new(36, 24);

    /// <summary>L2 카드 132×56.</summary>
    public static readonly Size L2Card = new(132, 56);

    /// <summary>L1 노드 상자 — 틀 위 표지(7) · 아래 짧은 이름을 담는다. 중심 (22, 22).</summary>
    public static readonly Size L1Box = new(44, 50);

    /// <summary>L0 틀 둘레 여백(한쪽).</summary>
    public const double L0_MARGIN = 3.0;

    /// <summary>표지 높이 · 표지 간격 · 틀과의 틈(SB <c>marks()</c> 의 h 7 · g 4 · 3).</summary>
    public const double MARK_HEIGHT = 7.0;
    public const double MARK_GAP = 4.0;
    public const double MARK_CLEARANCE = 3.0;

    /// <summary>X 하나의 반폭 · 두 X 사이 반거리 · 점 반지름.</summary>
    public const double CROSS_HALF_WIDTH = 3.0;
    public const double CROSS_OFFSET = 4.5;
    public const double DOT_RADIUS = 1.6;

    /// <summary>모르는 제대 — L0 틀(중대 크기로 둔다: 가운데 크기라 어느 제대로도 오해되지 않는다).</summary>
    private static readonly Size L0Unknown = new(12, 8);

    private static readonly Dictionary<EnumUnitEchelon, Size> L0Frames = new()
    {
        [EnumUnitEchelon.Division] = new Size(20, 13),
        [EnumUnitEchelon.Regiment] = new Size(17, 11),
        [EnumUnitEchelon.Battalion] = new Size(14, 9),
        [EnumUnitEchelon.Company] = new Size(12, 8),
        [EnumUnitEchelon.Outpost] = new Size(8, 6),
    };
    #endregion

    /// <summary>그 단계 · 제대의 틀 크기. L0 는 크기가 제대를 말한다(SB S2).</summary>
    public static Size FrameSize(UnitMapLevel level, EnumUnitEchelon? echelon) => level switch
    {
        UnitMapLevel.L2 => L2Frame,
        UnitMapLevel.L1 => L1Frame,
        _ => echelon is EnumUnitEchelon e && L0Frames.TryGetValue(e, out var size) ? size : L0Unknown,
    };

    /// <summary>제대 → 표지. 사단 XX · 연대 ||| · 대대 || · 중대 | · 소초(소대) ●●● · 모름 ?.</summary>
    public static UnitEchelonMark MarkOf(EnumUnitEchelon? echelon) => echelon switch
    {
        EnumUnitEchelon.Division => new(UnitMarkKind.Crosses, 2),
        EnumUnitEchelon.Regiment => new(UnitMarkKind.Bars, 3),
        EnumUnitEchelon.Battalion => new(UnitMarkKind.Bars, 2),
        EnumUnitEchelon.Company => new(UnitMarkKind.Bars, 1),
        EnumUnitEchelon.Outpost => new(UnitMarkKind.Dots, 3),
        _ => new(UnitMarkKind.Unknown, 1),
    };

    /// <summary>노드 요소 상자와 그 안의 부대 위치 점.</summary>
    public static (Size Box, Point Center) NodeBox(UnitMapLevel level, EnumUnitEchelon? echelon)
    {
        switch (level)
        {
            case UnitMapLevel.L2:
                return (L2Card, new Point(L2Card.Width / 2, L2Card.Height / 2));
            case UnitMapLevel.L1:
                return (L1Box, new Point(22, 22));
            default:
                var frame = FrameSize(level, echelon);
                var box = new Size(frame.Width + L0_MARGIN * 2, frame.Height + L0_MARGIN * 2);
                return (box, new Point(box.Width / 2, box.Height / 2));
        }
    }

    /// <summary>그 단계 · 제대의 기호 한 벌.</summary>
    public static UnitSymbolShape Build(UnitMapLevel level, EnumUnitEchelon? echelon)
    {
        var (box, center) = NodeBox(level, echelon);
        var size = FrameSize(level, echelon);

        // 틀 왼쪽 위 — 반 픽셀(x.5)에 맞춘다(NFR-08: 1px 선이 두 픽셀에 번지지 않게).
        var origin = level switch
        {
            UnitMapLevel.L2 => new Point(10, 18),                                  // 카드 안 왼쪽(SB: x − 56, y − 10)
            UnitMapLevel.L1 => new Point(center.X - size.Width / 2, center.Y - size.Height / 2),
            _ => new Point(L0_MARGIN, L0_MARGIN),
        };
        var frame = new Rect(HalfPixel(origin.X), HalfPixel(origin.Y), size.Width, size.Height);

        var mark = MarkOf(echelon);
        var anchor = new Point(frame.Left + frame.Width / 2, frame.Top - MARK_CLEARANCE);
        var (strokes, dots) = level == UnitMapLevel.L0 ? (string.Empty, string.Empty) : MarkPaths(mark, anchor);

        return new UnitSymbolShape(level, box, center, frame, RectPath(frame), mark, strokes, dots, anchor, string.Empty);
    }

    #region - 상태 표지 경로(노드 스타일이 쓴다 — FR-23) -
    /// <summary>★ 내 부대 — 다섯 꼭짓점 별(안쪽 반지름 0.45).</summary>
    public static string StarPathData(Point center, double radius)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < 10; i++)
        {
            var angle = -Math.PI / 2 + i * Math.PI / 5;
            var r = i % 2 == 1 ? radius * 0.45 : radius;
            sb.Append(i == 0 ? "M" : " L").Append(P(center.X + r * Math.Cos(angle), center.Y + r * Math.Sin(angle)));
        }
        return sb.Append(" Z").ToString();
    }

    /// <summary>▲ 장비 오류 — 밑변 왼쪽 (x, y + size) 에서 시작하는 정삼각형 근사.</summary>
    public static string TrianglePathData(double x, double y, double size)
        => $"M{P(x, y + size)} L{P(x + size / 2, y)} L{P(x + size, y + size)} Z";

    /// <summary>선택 — 모서리 괄호 넷(L0 · L1).</summary>
    public static string BracketsPathData(Rect r, double length)
        => $"M{P(r.Left, r.Top + length)} V{N(r.Top)} H{N(r.Left + length)} "
         + $"M{P(r.Right - length, r.Top)} H{N(r.Right)} V{N(r.Top + length)} "
         + $"M{P(r.Right, r.Bottom - length)} V{N(r.Bottom)} H{N(r.Right - length)} "
         + $"M{P(r.Left + length, r.Bottom)} H{N(r.Left)} V{N(r.Bottom - length)}";

    /// <summary>운용 중지(L0) — 틀 왼쪽 아래에서 오른쪽 위로 사선 하나.</summary>
    public static string SuspendedSlashPathData(Rect frame)
        => $"M{P(frame.Left, frame.Bottom)} L{P(frame.Right, frame.Top)}";

    /// <summary>옮긴 부대 핀 — 머리 원(반지름 3) + 바늘.</summary>
    public static string PinPathData(Point tip)
        => $"M{P(tip.X - 3, tip.Y - 3)} A3,3 0 1 1 {P(tip.X + 3, tip.Y - 3)} A3,3 0 1 1 {P(tip.X - 3, tip.Y - 3)} Z "
         + $"M{P(tip.X, tip.Y)} L{P(tip.X, tip.Y + 5)}";
    #endregion

    #region - 내부 -
    private static (string Strokes, string Dots) MarkPaths(UnitEchelonMark mark, Point anchor)
    {
        var cx = anchor.X;
        var bottom = anchor.Y;
        var top = bottom - MARK_HEIGHT;
        var strokes = new StringBuilder();
        var dots = new StringBuilder();

        switch (mark.Kind)
        {
            case UnitMarkKind.Crosses:
                for (var i = 0; i < mark.Count; i++)
                {
                    var x = cx + (i - (mark.Count - 1) / 2.0) * CROSS_OFFSET * 2;
                    Append(strokes, $"M{P(x - CROSS_HALF_WIDTH, top)} L{P(x + CROSS_HALF_WIDTH, bottom)}");
                    Append(strokes, $"M{P(x + CROSS_HALF_WIDTH, top)} L{P(x - CROSS_HALF_WIDTH, bottom)}");
                }
                break;

            case UnitMarkKind.Bars:
                for (var i = 0; i < mark.Count; i++)
                {
                    var x = cx + (i - (mark.Count - 1) / 2.0) * MARK_GAP;
                    Append(strokes, $"M{P(x, top)} L{P(x, bottom)}");
                }
                break;

            case UnitMarkKind.Dots:
                var cy = bottom - MARK_HEIGHT / 2;
                for (var i = 0; i < mark.Count; i++)
                {
                    var x = cx + (i - (mark.Count - 1) / 2.0) * MARK_GAP;
                    Append(dots, $"M{P(x - DOT_RADIUS, cy)} A{N(DOT_RADIUS)},{N(DOT_RADIUS)} 0 1 1 {P(x + DOT_RADIUS, cy)} "
                               + $"A{N(DOT_RADIUS)},{N(DOT_RADIUS)} 0 1 1 {P(x - DOT_RADIUS, cy)} Z");
                }
                break;
        }

        return (strokes.ToString(), dots.ToString());
    }

    private static void Append(StringBuilder sb, string figure)
    {
        if (sb.Length > 0) sb.Append(' ');
        sb.Append(figure);
    }

    private static string RectPath(Rect r)
        => $"M{P(r.Left, r.Top)} L{P(r.Right, r.Top)} L{P(r.Right, r.Bottom)} L{P(r.Left, r.Bottom)} Z";

    /// <summary>정수 · 반 정수 어디에 있든 가장 가까운 x.5 로.</summary>
    private static double HalfPixel(double value) => Math.Floor(value) + 0.5;

    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string P(double x, double y) => N(x) + "," + N(y);
    #endregion
}
