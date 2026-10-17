using System.Windows;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// 콤보 드롭다운 목록 배치 — 순수 함수. 목록이 창(작업 영역과 겹친 부분) 안에 들어오게 위 · 아래를 고르고 높이를 자른다.
/// </summary>
/// <remarks>
/// <para>2026-10-08 실창 점검: 이벤트 창 맨 아래 조치 트레이의 문구 콤보가 아래로만 펼쳐져 목록이 창 밖 · 작업 표시줄까지 내려갔다.
/// WPF 팝업은 모니터 전체(작업 표시줄 포함) 안에만 밀어 넣으므로 창 · 작업 영역 기준은 우리가 정한다.</para>
/// <para>좌표는 모두 같은 좌표계(대상 왼쪽 위 원점)여야 한다. 경계가 비었거나 대상이 경계 밖(화면 밖 시험 창)이면 손대지 않는다.</para>
/// </remarks>
public static class DropDownPlacement
{
    /// <summary>목록 높이를 자를 때 남겨 두는 몫 — 팝업 그림자 · 테두리 · 안쪽 여백(목록 높이 밖).</summary>
    public const double ChromeAllowance = 24;

    /// <summary>아무리 좁아도 목록이 이보다 작아지지는 않는다(두어 줄) — 이보다 좁으면 넘침은 피할 수 없다.</summary>
    public const double MinimumHeight = 56;

    /// <summary>창과 작업 영역이 겹친 부분 — 창을 모르면 작업 영역, 작업 영역을 모르면 창.</summary>
    public static Rect Bounds(Rect window, Rect work)
    {
        if (window.IsEmpty) return work;
        if (work.IsEmpty) return window;
        var inner = window;
        inner.Intersect(work);
        return inner.IsEmpty ? work : inner;
    }

    /// <summary>
    /// 목록 최대 높이 — 대상 위 · 아래 중 넓은 쪽에 들어가는 만큼(원래 값 <paramref name="requested"/> 보다 크게는 안 늘린다).
    /// </summary>
    public static double MaxHeight(Rect target, Rect bounds, double requested)
    {
        if (bounds.IsEmpty || !bounds.IntersectsWith(target)) return requested;
        var room = Math.Max(bounds.Bottom - target.Bottom, target.Top - bounds.Top) - ChromeAllowance;
        return Math.Min(requested, Math.Max(MinimumHeight, room));
    }

    /// <summary>
    /// 목록 왼쪽 위 — 제안(<paramref name="proposed"/>, 원래 템플릿의 배치)이 경계 안에 통째로 들어가면 그대로,
    /// 아니면 대상 아래 → 대상 위 순으로 들어가는 쪽, 둘 다 안 되면 넓은 쪽에 두고 경계 안으로 민다. 가로는 들어가면 경계 안으로 민다.
    /// </summary>
    public static Point Resolve(Rect target, Size popup, Point proposed, Rect bounds)
    {
        if (bounds.IsEmpty || !bounds.IntersectsWith(target)) return proposed;

        bool Fits(double top) => top >= bounds.Top && top + popup.Height <= bounds.Bottom;
        var below = target.Bottom;
        var above = target.Top - popup.Height;

        double y;
        if (Fits(proposed.Y)) y = proposed.Y;
        else if (Fits(below)) y = below;
        else if (Fits(above)) y = above;
        else
        {
            var roomBelow = bounds.Bottom - target.Bottom;
            var roomAbove = target.Top - bounds.Top;
            y = Clamp(roomAbove > roomBelow ? above : below, bounds.Top, bounds.Bottom - popup.Height);
        }

        var x = popup.Width <= bounds.Width ? Clamp(proposed.X, bounds.Left, bounds.Right - popup.Width) : proposed.X;
        return new Point(x, y);
    }

    /// <summary>하한 우선 자르기 — 상자가 경계보다 크면 하한(왼쪽 · 위)에 붙인다.</summary>
    private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(value, max));
}
