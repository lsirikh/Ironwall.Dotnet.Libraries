using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;
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
/// <para>★ <b>우리가 치운 것을 런타임 탓으로 읽지 않는다.</b> 기다리는 동안 브라우저를 내려놓는 일은 흔하다
/// (줄을 고를 때마다 · 레일 전환 · 폭 변화). 세대 번호와 인스턴스 동일성으로 "아직 이것이 그것인가"를 확인하고,
/// <see cref="PreviewRenderRules"/> 가 런타임 탓인지 가른다 — 그러지 않으면 "런타임이 없습니다" 가
/// 앱을 껐다 켤 때까지 굳는다.</para>
/// </remarks>
public partial class ReportPreviewView : UserControl
{
    private ReportPreviewViewModel? _viewModel;
    private Border? _host;
    private WebView2? _browser;
    private string? _renderedHtml;

    /// <summary>브라우저를 만들고 없앨 때마다 오른다 — 기다림 뒤에 "그 사이 치웠는가"를 이것으로 안다.</summary>
    private int _generation;

    public ReportPreviewView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnBrowserHostLoaded(object sender, RoutedEventArgs e)
    {
        _host = (Border)sender;
        Sync();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();
        _viewModel = e.NewValue as ReportPreviewViewModel;
        Attach();
        _renderedHtml = null;
        Sync();
    }

    /// <summary>뗐다가 같은 뷰가 다시 붙는 경우(패널 재표시 · 탭) — 구독을 되살린다.</summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null && DataContext is ReportPreviewViewModel vm) _viewModel = vm;
        Attach();
        Sync();
    }

    /// <summary>
    /// ★ 구독을 반드시 뗀다 — 싱글턴 뷰모델이 죽은 뷰를 붙잡고 있으면, 그 좀비 뷰가 죽은 Border 에
    /// WebView2 를 계속 만들고 <c>IsRuntimeReady</c> 까지 뒤집는다.
    /// </summary>
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Detach();
        Teardown();
    }

    private void Attach()
    {
        if (_viewModel is null || _isAttached) return;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _isAttached = true;
    }

    private void Detach()
    {
        if (_viewModel is null || !_isAttached) return;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _isAttached = false;
    }

    private bool _isAttached;

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
                _generation++;
                _browser = new WebView2 { MinHeight = 120 };
                _browser.CoreWebView2InitializationCompleted += OnCoreInitialized;
                _browser.SetBinding(WebView2.ZoomFactorProperty,
                    new Binding(nameof(ReportPreviewViewModel.ZoomFactor)) { Source = _viewModel, Mode = BindingMode.TwoWay });
                System.Windows.Automation.AutomationProperties.SetAutomationId(_browser, "Reports.Preview.Browser");
                _host.Child = _browser;
            }
            catch (Exception)
            {
                // 만들지도 못했다 — 이것은 진짜 런타임 문제다.
                _browser = null;
                _host.Child = null;
                _viewModel.IsRuntimeReady = false;
                return;
            }
        }

        _ = RenderAsync(_viewModel.Html, _browser, _generation);
    }

    private void OnCoreInitialized(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2InitializationCompletedEventArgs e)
    {
        if (sender is WebView2 view) WebViewHardening.Apply(view.CoreWebView2);
    }

    private async Task RenderAsync(string? html, WebView2 browser, int generation)
    {
        if (string.IsNullOrEmpty(html)) return;
        if (string.Equals(_renderedHtml, html, StringComparison.Ordinal)) return;   // 같은 HTML 을 두 번 네비게이트하지 않는다

        try
        {
            await browser.EnsureCoreWebView2Async();

            // ★ 기다리는 동안 우리가 치웠을 수 있다 — 그렇다면 여기서 조용히 끝낸다.
            if (!IsStillCurrent(browser, generation)) return;

            browser.NavigateToString(html);       // 자립형 HTML(약 280KB) < NavigateToString 2MB 한도
            _renderedHtml = html;
        }
        catch (Exception)
        {
            var isCurrent = IsStillCurrent(browser, generation);
            var isLive = _viewModel?.IsSurfaceLive ?? false;

            // 우리가 치운 뒤의 예외는 런타임 잘못이 아니다(그렇게 읽으면 걸쇠가 영영 내려간다).
            if (!PreviewRenderRules.ShouldMarkRuntimeUnavailable(isCurrent, isLive)) return;

            _renderedHtml = null;
            Teardown();
            if (_viewModel != null) _viewModel.IsRuntimeReady = false;
        }
    }

    private bool IsStillCurrent(WebView2 browser, int generation)
        => generation == _generation && ReferenceEquals(browser, _browser);

    private void Teardown()
    {
        _renderedHtml = null;
        if (_host != null) _host.Child = null;
        if (_browser is null) return;

        _generation++;                 // 기다리던 렌더가 "나는 이제 그것이 아니다"를 알 수 있게
        var browser = _browser;
        _browser = null;
        try
        {
            browser.CoreWebView2InitializationCompleted -= OnCoreInitialized;
            BindingOperations.ClearBinding(browser, WebView2.ZoomFactorProperty);
            browser.Dispose();
        }
        catch { /* 이미 내려갔다 */ }
    }
}
