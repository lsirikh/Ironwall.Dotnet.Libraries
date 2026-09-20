using Microsoft.Web.WebView2.Core;
using System;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;

/// <summary>
/// 이 PC 에 WebView2 런타임이 설치돼 있는가 — <b>화면과 무관하게</b> 한 번 물어본다.
/// </summary>
/// <remarks>
/// <para><b>왜 따로 묻는가</b>: 종전에는 "살아 있는 WebView2 를 만들어 보다가 실패하면" 런타임이 없다고 판정했다.
/// 그런데 살아 있는 시도는 <b>도킹에서만</b> 일어난다 — 서랍 · 접힘에서는 한 번도 만들지 않으므로
/// 런타임이 없는 PC 에서도 "[크게 보기] 를 누르면 큰 창으로 봅니다" 가 켜진 채 남고, 눌러도 빈 창이 뜬다.</para>
/// <para>그래서 판정을 <b>표면 모드와 분리</b>한다. 런타임 유무는 이 프로브가 정하고,
/// 렌더 실패는 <b>지금 살아 있는 브라우저가 깨졌을 때만</b> 그 판정을 뒤집는다.</para>
/// </remarks>
public interface IWebViewRuntimeProbe
{
    /// <summary>런타임을 쓸 수 있는가. 값을 캐시해도 되지만 예외를 밖으로 던지지 않는다.</summary>
    bool IsAvailable();
}

/// <summary>실제 프로브 — 설치된 브라우저 판본 문자열을 한 번 묻고 결과를 기억한다.</summary>
public sealed class WebViewRuntimeProbe : IWebViewRuntimeProbe
{
    private bool? _available;

    public static IWebViewRuntimeProbe Instance { get; } = new WebViewRuntimeProbe();

    public bool IsAvailable()
    {
        if (_available.HasValue) return _available.Value;

        try
        {
            // 런타임이 없으면 WebView2RuntimeNotFoundException, 판본이 있으면 "120.0.…" 같은 문자열.
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            _available = !string.IsNullOrWhiteSpace(version);
        }
        catch (Exception)
        {
            _available = false;
        }
        return _available.Value;
    }
}

/// <summary>시험 · 미리보기 도구가 값을 정하는 프로브.</summary>
public sealed class FixedWebViewRuntimeProbe : IWebViewRuntimeProbe
{
    public FixedWebViewRuntimeProbe(bool available) => Available = available;

    public bool Available { get; set; }

    public bool IsAvailable() => Available;
}
