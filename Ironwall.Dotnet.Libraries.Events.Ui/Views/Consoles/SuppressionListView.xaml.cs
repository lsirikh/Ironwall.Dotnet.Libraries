using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Views.Consoles;

/// <summary>
/// 억제 스케줄 T1 목록. 뷰는 <b>배선만</b> 한다 — 판정은 전부 뷰모델과 순수 함수에 있다.
/// </summary>
public partial class SuppressionListView : UserControl
{
    public SuppressionListView()
    {
        InitializeComponent();
    }

    private SuppressionConsoleViewModel? Model => DataContext as SuppressionConsoleViewModel;

    /// <summary>행의 [취소 예약] — 확인 팝업을 먼저 띄운다.</summary>
    private void OnCancelRow(object sender, RoutedEventArgs e)
    {
        if (Model is null) return;
        if (sender is FrameworkElement { DataContext: SuppressionConsoleRow row }) Model.Selected = row;
        _ = Model.CancelSelectedAsync();
    }
}
