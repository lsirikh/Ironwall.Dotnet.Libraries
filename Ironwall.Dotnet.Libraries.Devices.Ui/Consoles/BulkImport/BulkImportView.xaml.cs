using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.BulkImport;

/// <summary>
/// 미리보기 판의 단추 배선 — 판단은 뷰모델에. 파일 창(결과 받기)은 대시보드 뷰모델이 쥔 고르개를 쓴다(<see cref="ResultSaveRequested"/>).
/// </summary>
public partial class BulkImportView : UserControl
{
    public BulkImportView()
    {
        InitializeComponent();
    }

    private BulkImportViewModel? ViewModel => DataContext as BulkImportViewModel;

    /// <summary>[결과 받기] — 저장 창은 대시보드가 띄운다(가짜 고르개로 바꿔 끼울 수 있게).</summary>
    public static readonly RoutedEvent ResultSaveRequestedEvent =
        EventManager.RegisterRoutedEvent(nameof(ResultSaveRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(BulkImportView));

    public event RoutedEventHandler ResultSaveRequested
    {
        add => AddHandler(ResultSaveRequestedEvent, value);
        remove => RemoveHandler(ResultSaveRequestedEvent, value);
    }

    private async void OnRegister(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.RegisterAsync();
    }

    private async void OnRetry(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.RetryFailedAsync();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => ViewModel?.Cancel();

    private void OnClose(object sender, RoutedEventArgs e) => ViewModel?.Close();

    private void OnSaveResult(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ResultSaveRequestedEvent, this));
}
