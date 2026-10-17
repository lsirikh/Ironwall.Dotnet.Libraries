using Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;
using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Views.Panels;

/// <summary>
/// 생성 이력 상세 칸의 화면 쪽 배선. 판단은 전부 뷰모델에 있다.
/// </summary>
/// <remarks>
/// 단추는 Caliburn 의 <c>x:Name</c> 규약 대신 <c>Click</c> 으로 묶는다 —
/// <c>x:Name</c> 은 바인딩 지시자라 계측 · 배선에 겸용하면 충돌한다(장비 콘솔 선례).
/// </remarks>
public partial class ReportGenerationDetailView : UserControl
{
    public ReportGenerationDetailView() => InitializeComponent();

    private ReportConsoleViewModel? ViewModel => DataContext as ReportConsoleViewModel;

    private void OnZoomIn(object sender, RoutedEventArgs e) => ViewModel?.PreviewViewModel.ZoomIn();

    private void OnZoomOut(object sender, RoutedEventArgs e) => ViewModel?.PreviewViewModel.ZoomOut();

    private void OnZoomReset(object sender, RoutedEventArgs e) => ViewModel?.PreviewViewModel.ZoomReset();

    private void OnLargeView(object sender, RoutedEventArgs e) => ViewModel?.OpenLargePreview();

    private async void OnDownloadPdf(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.DownloadPdfAsync();
    }

    private async void OnDownloadCsv(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.DownloadCsvAsync();
    }

    private async void OnCancelGeneration(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.CancelGenerationAsync();
    }

    private async void OnDeleteGeneration(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.DeleteGenerationAsync();
    }
}
