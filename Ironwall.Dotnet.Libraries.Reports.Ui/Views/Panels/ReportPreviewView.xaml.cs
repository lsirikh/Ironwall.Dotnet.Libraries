using Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Views.Panels;

/// <summary>
/// 미리보기 — 서버 자립형 HTML 을 WebView2 로 그린다. <b>살아 있어도 되는 동안에만</b> 만든다.
/// </summary>
/// <remarks>
/// <para>★ 공역: WebView2 는 네이티브 창이라 같은 최상위 창의 WPF 위에 그려진다. 그래서
/// <c>Visibility="Collapsed"</c> 로 숨기는 것만으로는 부족한 상황(서랍 · 스크림 · 팝업)이 있고,
/// 무엇보다 <b>만들지 않는 것</b>이 확실하다. 판정은 뷰모델의 순수 함수
/// (<c>ReportPreviewSurfaceRules.Resolve</c>)가 하고, 이 코드는 그 결과에 따라 <b>붙였다 뗀다</b>.</para>
/// <para>런타임이 없거나 초기화에 실패하면 뷰모델의 <c>IsRuntimeReady</c> 를 내려 자리표시자가
/// 까닭을 적게 한다 — 조용히 삼켜 빈 칸을 내지 않는다.</para>
/// </remarks>
public partial class ReportPreviewView : UserControl
{
    private ReportPreviewViewModel? _viewModel;
    private Border? _host;
    private WebView2? _browser;
    private string? _renderedHtml;

    public ReportPreviewView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void OnBrowserHostLoaded(object sender, RoutedEventArgs e)
    {
        _host = (Border)sender;
        Sync();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = e.NewValue as ReportPreviewViewModel;
        if (_viewModel != null) _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _renderedHtml = null;
        Sync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Teardown();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ReportPreviewViewModel.Surface)
            or nameof(ReportPreviewViewModel.IsSurfaceLive)
            or nameof(ReportPreviewViewModel.Html))
            Sync();
    }

    /// <summary>판정에 맞춰 브라우저를 붙이거나 뗀다.</summary>
    private void Sync()
    {
        if (_host is null || _viewModel is null) return;

        if (!_viewModel.IsSurfaceLive) { Teardown(); return; }

        if (_browser is null)
        {
            try
            {
                _browser = new WebView2 { MinHeight = 120 };
                _browser.SetBinding(WebView2.ZoomFactorProperty,
                    new Binding(nameof(ReportPreviewViewModel.ZoomFactor)) { Source = _viewModel, Mode = BindingMode.TwoWay });
                System.Windows.Automation.AutomationProperties.SetAutomationId(_browser, "Reports.Preview.Browser");
                _host.Child = _browser;
            }
            catch (Exception)
            {
                // 런타임 미설치 등 — 콘솔은 정상이고 자리표시자가 까닭을 적는다.
                _browser = null;
                _host.Child = null;
                _viewModel.IsRuntimeReady = false;
                return;
            }
        }

        _ = RenderAsync(_viewModel.Html);
    }

    private async Task RenderAsync(string? html)
    {
        var browser = _browser;
        if (browser is null || string.IsNullOrEmpty(html)) return;
        if (string.Equals(_renderedHtml, html, StringComparison.Ordinal)) return;   // 같은 HTML 을 두 번 네비게이트하지 않는다

        try
        {
            await browser.EnsureCoreWebView2Async();
            browser.NavigateToString(html);       // 자립형 HTML(약 280KB) < NavigateToString 2MB 한도
            _renderedHtml = html;
        }
        catch (Exception)
        {
            _renderedHtml = null;
            Teardown();
            if (_viewModel != null) _viewModel.IsRuntimeReady = false;
        }
    }

    private void Teardown()
    {
        _renderedHtml = null;
        if (_host != null) _host.Child = null;
        if (_browser is null) return;

        try { _browser.Dispose(); } catch { /* 이미 내려갔다 */ }
        _browser = null;
    }
}
