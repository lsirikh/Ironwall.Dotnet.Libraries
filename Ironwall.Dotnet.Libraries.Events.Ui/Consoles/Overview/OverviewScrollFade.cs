namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Overview;

/// <summary>
/// 개요 본문 아래 가장자리 흐림 — 판정은 순수 함수(뷰는 결과만 그린다).
/// </summary>
/// <remarks>
/// 개요는 카드가 세로로 쌓여 표면(1340×800)보다 길다. 스크롤 칸의 아랫변이 다음 카드("장비별 이벤트")의 제목을
/// 글자 한가운데에서 자르면 "잘린 칸" 결함처럼 보였다(GIS 실창 육안 검토 #20). 아래에 더 있을 때만 끝을 흐려
/// "이어진다" 를 형태로 알리고, 끝까지 내리면 흐림을 걷는다(마지막 카드를 흐린 채로 두지 않는다).
/// </remarks>
public static class OverviewScrollFade
{
    /// <summary>흐림 띠 높이(DIU).</summary>
    public const double FadeHeight = 28;

    /// <summary>아래에 더 볼 것이 남았는가 — 1 DIU 미만의 반올림 찌꺼기는 끝으로 본다.</summary>
    public static bool HasMoreBelow(double scrollableHeight, double verticalOffset)
        => scrollableHeight - verticalOffset >= 1;

    /// <summary>
    /// 흐림이 시작되는 자리(0~1, 요소 높이에 대한 비율). 요소가 흐림 띠보다 낮으면 흐리지 않는다(1).
    /// </summary>
    public static double FadeStart(double elementHeight, double fadeHeight = FadeHeight)
    {
        if (double.IsNaN(elementHeight) || elementHeight <= fadeHeight * 2) return 1;
        return 1 - fadeHeight / elementHeight;
    }
}
