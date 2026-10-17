using System;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;

/// <summary>
/// ★ 상세 칸 미리보기의 <b>폭 맞춤</b> — 순수 함수. 칸 폭(DIU)만 받아 배율과 "살아 있는 미리보기를 실어도 되는가"를 정한다.
/// </summary>
/// <remarks>
/// <para><b>근거(실측)</b></para>
/// <list type="bullet">
///   <item>서버 보고서 HTML 은 반응형이 아니다 — <c>report.css</c> 의 <c>.page { width: 210mm }</c>(A4, 96 dpi 에서 793.7px),
///         <c>&lt;meta name=viewport&gt;</c> 없음, <c>@media</c> 없음(api-test-server app/templates/reports/assets/report.css:12).
///         그래서 글을 다시 흐르게 할 수 없고, 쪽 전체를 칸 폭에 맞춰 <b>축소</b>해야 한다.</item>
///   <item>[크게 보기] 창(100%)에서 쪽 폭 793px 을 캡처로 확인했다(gis-r7-gallery 013) — CSS px 1 = DIU 1.</item>
///   <item>칸 폭: 도킹 380 → 미리보기 347 · 도킹 S 300 → 267 · 서랍(콘솔 1100 · 900) 360 → 326(서랍 왼쪽 굵은 선 2 포함) — 뷰 시험 실측(ReportConsolePreviewViewTests).</item>
/// </list>
/// <para>세로 스크롤 막대(최대 17px)만큼 빼고 쪽 폭으로 나눈다. 1 보다 크게 키우지 않는다(넓은 칸에서 흐려지지 않게).</para>
/// <para><b>하한 0.30</b>: 이보다 작으면 쪽 머리 · 표 머리(12~13px)가 4px 아래로 떨어져 무엇인지조차 가리기 어렵다.
/// 가장 좁은 실제 칸(도킹 S 267 → 0.31)이 하한 위에 들도록 정했다 — 그 아래(256 미만)는 실제 콘솔 폭에서 생기지 않고
/// (서랍은 min(360, 콘솔×0.86) 이고 칸은 서랍 − 34 라, 콘솔이 약 337 아래로 내려가야 256 밑이 된다),
/// 생기면 자리표시자가 [크게 보기]로 이끈다.
/// WebView2 자체 하한(0.25)보다도 위다.</para>
/// </remarks>
public static class ReportPreviewFit
{
    /// <summary>보고서 한 쪽의 폭 — A4 210mm @ 96 dpi.</summary>
    public const double PageWidth = 794;

    /// <summary>세로 스크롤 막대 몫(고전 막대 17px — 겹침 막대여도 넉넉히 뺀다).</summary>
    public const double ScrollbarAllowance = 17;

    /// <summary>읽을 수 있는 최소 배율.</summary>
    public const double MinZoom = 0.30;

    /// <summary>맞춤이 키우는 최대 배율(원본 크기).</summary>
    public const double MaxFitZoom = 1.0;

    /// <summary>수동 확대 상한.</summary>
    public const double MaxZoom = 3.0;

    /// <summary>살아 있는 미리보기를 싣는 최소 칸 폭 — <see cref="PageWidth"/> × <see cref="MinZoom"/> + 막대 = 255.2 → 256.</summary>
    public static readonly double MinLiveWidth = Math.Ceiling(PageWidth * MinZoom + ScrollbarAllowance);

    /// <summary>아직 재지 못한 폭(0 · 음수 · NaN)인가 — 재기 전에는 막지 않는다.</summary>
    public static bool IsUnmeasured(double paneWidth) => double.IsNaN(paneWidth) || double.IsInfinity(paneWidth) || paneWidth <= 0;

    /// <summary>이 칸 폭에 살아 있는 미리보기를 실어도 되는가.</summary>
    public static bool CanShowLive(double paneWidth) => IsUnmeasured(paneWidth) || paneWidth >= MinLiveWidth;

    /// <summary>쪽 전체가 칸 폭에 들어가는 배율(0.01 단위로 내림 — 올리면 가로 스크롤이 생긴다).</summary>
    public static double FitZoom(double paneWidth)
    {
        if (IsUnmeasured(paneWidth)) return MaxFitZoom;
        var raw = (paneWidth - ScrollbarAllowance) / PageWidth;
        var floored = Math.Floor(raw * 100) / 100;
        return Math.Max(MinZoom, Math.Min(MaxFitZoom, floored));
    }

    /// <summary>수동 확대 · 축소 한 걸음(10%) 뒤의 배율 — 하한은 <see cref="MinZoom"/>(맞춤 배율보다 커지는 '축소'가 없게).</summary>
    public static double Step(double zoom, int direction)
        => Math.Max(MinZoom, Math.Min(MaxZoom, Math.Round(zoom + 0.1 * Math.Sign(direction), 2)));
}
