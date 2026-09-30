using System.Windows;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;

/// <summary>
/// 조립 카드(L3) 자리 — 아이콘 오른쪽에 붙이고, 지도 밖으로 나가면 왼쪽으로 뒤집고, 위아래는 지도 안으로 가둔다(순수 함수).
/// </summary>
public static class ComponentCardPlacement
{
    /// <summary>아이콘 가장자리와 카드 사이 틈(px).</summary>
    public const double Gap = 12.0;

    /// <summary>지도 가장자리 여백(px).</summary>
    public const double Margin = 8.0;

    /// <summary>
    /// 카드 왼쪽 위 — <paramref name="anchor"/>(아이콘 화면 중심) 오른쪽에 두고 세로 가운데를 맞춘다.
    /// 오른쪽에 자리가 없으면 왼쪽, 거기도 없으면 지도 안으로 민다.
    /// </summary>
    /// <param name="anchor">아이콘 화면 중심.</param>
    /// <param name="markerHalf">아이콘 반 폭(px).</param>
    /// <param name="card">카드 크기.</param>
    /// <param name="viewport">지도 크기.</param>
    public static Point Place(Point anchor, double markerHalf, Size card, Size viewport)
    {
        double half = double.IsFinite(markerHalf) && markerHalf > 0 ? markerHalf : 0;
        double x = anchor.X + half + Gap;
        if (x + card.Width > viewport.Width - Margin)
        {
            var left = anchor.X - half - Gap - card.Width;
            x = left >= Margin ? left : Math.Max(Margin, viewport.Width - Margin - card.Width);
        }

        double y = anchor.Y - card.Height / 2;
        y = Math.Min(y, viewport.Height - Margin - card.Height);
        y = Math.Max(y, Margin);
        return new Point(x, y);
    }
}
