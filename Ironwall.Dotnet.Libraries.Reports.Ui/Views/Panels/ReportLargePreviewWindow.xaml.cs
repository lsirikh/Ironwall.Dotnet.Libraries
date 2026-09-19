using Microsoft.Web.WebView2.Wpf;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Views.Panels;

/// <summary>
/// [크게 보기] 창 — <b>자체 HWND</b>. 공역(airspace) 문제의 근본 회피책이다.
/// </summary>
/// <remarks>
/// 이 창 안에서는 WebView2 위에 WPF 를 얹지 않으므로 공역을 따질 일이 없다. 콘솔 쪽 상세 칸 미리보기는
/// 이 창이 열려 있는 동안 자리표시자로 내려간다(뷰모델의 <c>IsLargeViewOpen</c>).
/// </remarks>
public partial class ReportLargePreviewWindow : Window
{
    private readonly string? _html;
    private WebView2? _browser;

    public ReportLargePreviewWindow(string? html, string title)
    {
        InitializeComponent();
        _html = html;
        Title = string.IsNullOrWhiteSpace(title) ? "미리보기" : title;
    }

    private async void OnHostLoaded(object sender, RoutedEventArgs e)
    {
        var host = (Border)sender;
        if (string.IsNullOrEmpty(_html))
        {
            host.Child = Placeholder("보여 줄 미리보기가 없습니다.");
            return;
        }

        try
        {
            _browser = new WebView2();
            System.Windows.Automation.AutomationProperties.SetAutomationId(_browser, "Reports.LargePreview.Browser");
            host.Child = _browser;
            await _browser.EnsureCoreWebView2Async();
            _browser.NavigateToString(_html);
        }
        catch (Exception)
        {
            // 런타임 미설치 등 — 빈 칸을 내지 않고 까닭을 적는다.
            _browser = null;
            host.Child = Placeholder("이 PC 에 WebView2 런타임이 없어 미리보기를 그릴 수 없습니다.\n[PDF 내려받기] 로 내용을 확인하세요.");
        }
    }

    private static TextBlock Placeholder(string text) => new()
    {
        Text = text,
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(24),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Foreground = System.Windows.Media.Brushes.DimGray,
    };

    private void OnZoomIn(object sender, RoutedEventArgs e) => Zoom(+0.1);

    private void OnZoomOut(object sender, RoutedEventArgs e) => Zoom(-0.1);

    private void OnZoomReset(object sender, RoutedEventArgs e)
    {
        if (_browser != null) _browser.ZoomFactor = 1.0;
    }

    private void Zoom(double delta)
    {
        if (_browser is null) return;
        _browser.ZoomFactor = Math.Max(0.5, Math.Min(3.0, Math.Round(_browser.ZoomFactor + delta, 2)));
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        try { _browser?.Dispose(); } catch { /* 이미 내려갔다 */ }
        _browser = null;
        base.OnClosed(e);
    }
}
