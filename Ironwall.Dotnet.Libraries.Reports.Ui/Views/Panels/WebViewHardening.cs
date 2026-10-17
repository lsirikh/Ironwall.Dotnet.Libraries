using Microsoft.Web.WebView2.Core;
using System;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Views.Panels;

/// <summary>
/// 미리보기 WebView2 의 잠금 정책 — <b>두 인스턴스(상세 칸 · 크게 보기 창)가 같은 정책</b>을 쓰도록 한 곳에 둔다.
/// </summary>
/// <remarks>
/// <para>미리보기는 <b>서버가 만든 자립형 HTML</b> 하나를 <c>NavigateToString</c> 으로 그리는 것이 전부다.
/// 스크립트는 켜 둔다(인라인 Chart.js 가 그림을 그린다). 그 밖에 이 표면이 할 일은 없다:
/// 바깥으로 나가지 않고, 새 창을 열지 않고, 파일을 내려받지 않고, 개발자 도구 · 기본 컨텍스트 메뉴도 없다.</para>
/// <para>내려받기는 앱의 [PDF 내려받기] · [상세 CSV] 가 인가된 경로로 한다 — 브라우저가 직접 받게 두지 않는다.</para>
/// </remarks>
internal static class WebViewHardening
{
    /// <summary>초기화가 끝난 <see cref="CoreWebView2"/> 에 정책을 건다.</summary>
    public static void Apply(CoreWebView2? core)
    {
        if (core is null) return;

        try
        {
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsZoomControlEnabled = true;    // 확대 · 축소는 우리가 ZoomFactor 로 준다
            // IsScriptEnabled 는 건드리지 않는다 — 서버 HTML 의 인라인 Chart.js 가 꺼지면 그림이 사라진다.

            core.NavigationStarting += OnNavigationStarting;
            core.NewWindowRequested += OnNewWindowRequested;
            core.DownloadStarting += OnDownloadStarting;
        }
        catch (Exception)
        {
            // 판본에 따라 없는 설정이 있을 수 있다 — 잠금에 실패해도 미리보기 자체는 살려 둔다.
        }
    }

    /// <summary>
    /// 처음 <c>NavigateToString</c> 이 만드는 문서(약속상 <c>about:blank</c> 계열)만 통과시킨다.
    /// HTML 안에서 바깥으로 나가려는 이동은 전부 막는다.
    /// </summary>
    private static void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        var uri = e.Uri ?? string.Empty;
        var allowed = uri.Length == 0
                      || uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
                      || uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
        if (!allowed) e.Cancel = true;
    }

    private static void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        => e.Handled = true;     // 새 창을 열지 않는다(앱 밖으로 나가는 유일한 길을 막는다)

    private static void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
        => e.Cancel = true;      // 내려받기는 앱의 인가된 경로로만 한다
}
