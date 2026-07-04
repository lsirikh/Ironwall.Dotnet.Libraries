using Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Views.Panels;

/// <summary>
/// 미리보기 뷰 — VM(Html) 변경 시 WebView2에 서버 자립형 HTML을 NavigateToString 렌더.
/// EnsureCoreWebView2Async 는 Loaded(창에 붙은 뒤)에만 호출. 실제 HTML 단일 네비게이트(로딩페이지 없음)로 레이스 제거.
/// </summary>
public partial class ReportPreviewView : UserControl
{
    private ReportPreviewViewModel? _vm;
    private bool _loaded;

    public ReportPreviewView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null) _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = e.NewValue as ReportPreviewViewModel;
        if (_vm != null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            _ = RenderAsync(_vm.Html);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;
        _ = RenderAsync(_vm?.Html);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => _loaded = false;

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReportPreviewViewModel.Html))
            _ = RenderAsync(_vm?.Html);
    }

    private async Task RenderAsync(string? html)
    {
        if (!_loaded || string.IsNullOrEmpty(html)) return;   // 창에 붙은 뒤 + HTML 준비된 경우만
        try
        {
            await Browser.EnsureCoreWebView2Async();
            Browser.NavigateToString(html);   // 서버 자립형 HTML(≈280KB) < NavigateToString 2MB 한도
        }
        catch
        {
            // WebView2 Runtime 미설치/초기화 실패 등 — 무시(미리보기 미표시, 앱은 정상)
        }
    }
}
