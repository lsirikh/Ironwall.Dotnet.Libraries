using System.Windows;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>말풍선이 "?" 의 어느 쪽에 뜨는가 — 꼬리는 그 반대쪽 모서리에서 "?" 를 가리킨다.</summary>
public enum HelpCalloutSide
{
    /// <summary>"?" 아래(기본) — 꼬리는 위 모서리.</summary>
    Below,
    /// <summary>"?" 위 — 꼬리는 아래 모서리.</summary>
    Above,
    /// <summary>"?" 왼쪽 — 꼬리는 오른쪽 모서리.</summary>
    Left,
    /// <summary>"?" 오른쪽 — 꼬리는 왼쪽 모서리.</summary>
    Right,
}

/// <summary>배치 결과 — 말풍선 왼쪽 위(입력과 같은 좌표계) · 뜬 쪽 · 꼬리 중심의 자리(말풍선 안 좌표, 위/아래면 x · 좌/우면 y).</summary>
public readonly record struct HelpCalloutPlacementResult(Point Position, HelpCalloutSide Side, double TailOffset);

/// <summary>
/// 말풍선 배치 — 순수 함수(help-callout PRD FR-01). 아래 → 위 → 왼쪽 → 오른쪽 순으로 <b>통째로 들어가는</b> 쪽을 고른다.
/// </summary>
/// <remarks>
/// <para>말풍선 크기(<c>callout</c>)는 꼬리 자리(<see cref="TailMargin"/>, 네 변 모두)를 포함한 바깥 크기다 — 어느 쪽에 뜨든 크기가 같아야
/// 배치를 정한 뒤 꼬리를 옮겨도 다시 재지 않는다.</para>
/// <para>들어가는 쪽이 없으면 위 · 아래 중 넓은 쪽에 두고 경계 안으로 민다. "?" 가 경계 밖(화면 밖 시험 창 등)이면 밀지 않고 아래에 둔다 —
/// 화면 밖 대상을 위해 말풍선을 화면 안으로 끌어오지 않는다.</para>
/// </remarks>
public static class HelpCalloutPlacement
{
    /// <summary>꼬리 자리 — 말풍선 바깥 크기의 네 변에 비워 둔다(꼬리 길이).</summary>
    public const double TailMargin = 8;

    /// <summary>"?" 와 꼬리 끝 사이.</summary>
    public const double Gap = 2;

    /// <summary>꼬리 중심이 카드 모서리(둥근 귀퉁이)에서 떨어져야 하는 최소 거리.</summary>
    public const double TailEdgeInset = 16;

    /// <summary>"?" 를 왼쪽에 두고 펼칠 때 꼬리 중심의 기본 자리(카드 왼쪽에서) — 스토리보드처럼 말풍선이 "?" 에서 오른쪽으로 펼쳐진다.</summary>
    public const double PreferredTailInset = 20;

    /// <summary>
    /// 창 우선 배치 — 말풍선이 "?" 가 있는 창(<paramref name="window"/>, 작업 영역 <paramref name="screen"/> 과 겹친 부분) 안에
    /// 통째로 들어가는 쪽이 있으면 그 안에 둔다. 창 안 어디에도 안 들어가면(작은 대화창) 작업 영역 기준(<see cref="Resolve(Rect, Size, Rect)"/>)으로 돌아간다.
    /// </summary>
    /// <remarks>
    /// 2026-10-08 실창 점검: 부대 창 오른쪽 끝 "?" 의 말풍선이 작업 영역 기준으로만 밀려 창 오른쪽 밖으로 펼쳐졌고
    /// 창 안에서는 잘려 보였다 — 경계를 창으로 좁혀 창 안쪽으로 펼친다.
    /// </remarks>
    public static HelpCalloutPlacementResult Resolve(Rect target, Size callout, Rect window, Rect screen)
    {
        var inner = window;
        if (!inner.IsEmpty && !screen.IsEmpty) inner.Intersect(screen);
        if (!inner.IsEmpty && inner.IntersectsWith(target) && TryFit(target, callout, inner, out var inWindow)) return inWindow;
        return Resolve(target, callout, screen);
    }

    public static HelpCalloutPlacementResult Resolve(Rect target, Size callout, Rect bounds)
    {
        var below = new Point(0, target.Bottom + Gap);
        var above = new Point(0, target.Top - Gap - callout.Height);

        if (bounds.IsEmpty || !bounds.IntersectsWith(target))
            return Horizontal(HelpCalloutSide.Below, below.Y, target, callout, bounds, clamp: false);

        if (TryFit(target, callout, bounds, out var fitted)) return fitted;

        // 어디에도 통째로 안 들어간다 — 위 · 아래 중 넓은 쪽에 두고 세로로도 경계 안에 민다(겹침은 피할 수 없다).
        var roomBelow = bounds.Bottom - target.Bottom;
        var roomAbove = target.Top - bounds.Top;
        var side = roomBelow >= roomAbove ? HelpCalloutSide.Below : HelpCalloutSide.Above;
        var y = side == HelpCalloutSide.Below ? below.Y : above.Y;
        y = Clamp(y, bounds.Top, bounds.Bottom - callout.Height);
        return Horizontal(side, y, target, callout, bounds, clamp: true);
    }

    /// <summary>아래 → 위 → 왼쪽 → 오른쪽 순으로 경계 안에 통째로(가로 · 세로 모두) 들어가는 쪽.</summary>
    private static bool TryFit(Rect target, Size callout, Rect bounds, out HelpCalloutPlacementResult result)
    {
        var below = target.Bottom + Gap;
        var above = target.Top - Gap - callout.Height;
        var left = target.Left - Gap - callout.Width;
        var right = target.Right + Gap;
        var fitsAcross = callout.Width <= bounds.Width;
        var fitsAlong = callout.Height <= bounds.Height;

        if (fitsAcross && below + callout.Height <= bounds.Bottom) result = Horizontal(HelpCalloutSide.Below, below, target, callout, bounds, clamp: true);
        else if (fitsAcross && above >= bounds.Top) result = Horizontal(HelpCalloutSide.Above, above, target, callout, bounds, clamp: true);
        else if (fitsAlong && left >= bounds.Left) result = Vertical(HelpCalloutSide.Left, left, target, callout, bounds);
        else if (fitsAlong && right + callout.Width <= bounds.Right) result = Vertical(HelpCalloutSide.Right, right, target, callout, bounds);
        else
        {
            result = default;
            return false;
        }
        return true;
    }

    private static HelpCalloutPlacementResult Horizontal(HelpCalloutSide side, double y, Rect target, Size callout, Rect bounds, bool clamp)
    {
        var anchor = target.Left + target.Width / 2;
        var x = anchor - (TailMargin + PreferredTailInset);
        if (clamp) x = Clamp(x, bounds.Left, bounds.Right - callout.Width);
        return new HelpCalloutPlacementResult(new Point(x, y), side, TailFor(anchor - x, callout.Width));
    }

    private static HelpCalloutPlacementResult Vertical(HelpCalloutSide side, double x, Rect target, Size callout, Rect bounds)
    {
        var anchor = target.Top + target.Height / 2;
        var y = Clamp(anchor - (TailMargin + PreferredTailInset), bounds.Top, bounds.Bottom - callout.Height);
        return new HelpCalloutPlacementResult(new Point(x, y), side, TailFor(anchor - y, callout.Height));
    }

    /// <summary>꼬리 중심을 카드의 곧은 모서리 위로 — 귀퉁이(둥근 곳)에 걸리지 않게 자른다.</summary>
    private static double TailFor(double desired, double length)
    {
        var min = TailMargin + TailEdgeInset;
        var max = length - TailMargin - TailEdgeInset;
        return max < min ? length / 2 : Clamp(desired, min, max);
    }

    /// <summary>하한 우선 자르기 — 상자가 경계보다 크면 하한(왼쪽 · 위)에 붙인다.</summary>
    private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(value, max));
}
