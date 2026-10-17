namespace Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;

/// <summary>렌더가 끝내 실패했을 때, 그 까닭을 어떻게 읽을 것인가.</summary>
public enum PreviewRenderFailure
{
    /// <summary>실패가 아니다.</summary>
    None,

    /// <summary>
    /// 우리가 그 사이에 내려놓았다(다른 줄을 골랐다 · 레일을 바꿨다 · 폭이 바뀌어 자리표시자가 됐다).
    /// <b>런타임 잘못이 아니다</b> — 아무것도 표시하지 않는다.
    /// </summary>
    Superseded,

    /// <summary>지금 살아 있어야 할 브라우저가 실패했다 — 런타임을 못 쓴다고 알린다.</summary>
    Runtime,
}

/// <summary>
/// ★ "미리보기를 못 그렸다"의 까닭을 가르는 <b>순수</b> 규칙.
/// </summary>
/// <remarks>
/// <para><b>왜 필요한가</b>: <c>EnsureCoreWebView2Async</c> 를 기다리는 동안 우리가 스스로 브라우저를 내려놓는
/// 일이 <b>흔하다</b> — 줄을 고를 때마다 HTML 을 비우고, 레일을 바꾸고, 경계를 끌어 1280 을 넘나들면 매번 일어난다.
/// 그 뒤 이어지는 코드가 던지는 예외를 <b>런타임 탓으로 읽으면</b> "이 PC 에 WebView2 런타임이 없습니다" 가
/// 앱을 껐다 켤 때까지 굳는다(<c>IsRuntimeReady</c> 는 싱글턴 뷰모델의 한 방향 걸쇠였다).</para>
/// <para>그래서 실패를 두 가지로 가른다: <b>우리가 치운 것</b>(무시)과 <b>지금 것이 깨진 것</b>(알린다).</para>
/// </remarks>
public static class PreviewRenderRules
{
    /// <summary>
    /// 렌더 도중 예외가 났다 — 무엇 탓인가.
    /// </summary>
    /// <param name="isCurrentBrowser">기다리기 전에 쥔 브라우저가 <b>아직도</b> 이 뷰의 브라우저인가.</param>
    /// <param name="isStillLive">뷰모델의 판정이 <b>아직도</b> "살아 있는 미리보기"인가.</param>
    public static PreviewRenderFailure Classify(bool isCurrentBrowser, bool isStillLive)
        => isCurrentBrowser && isStillLive ? PreviewRenderFailure.Runtime : PreviewRenderFailure.Superseded;

    /// <summary>런타임을 못 쓴다고 표시할 것인가 — <b>지금 것이 깨졌을 때만</b> 참.</summary>
    public static bool ShouldMarkRuntimeUnavailable(bool isCurrentBrowser, bool isStillLive)
        => Classify(isCurrentBrowser, isStillLive) == PreviewRenderFailure.Runtime;
}
