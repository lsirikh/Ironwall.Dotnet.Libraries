using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Views.Dialogs;
/****************************************************************************
   Purpose      : 탐지 신호 이력 다이얼로그 뷰 (Detection_Signal_History FR-09)
                  코드비하인드는 뷰 관심사만 — 차트 포인트 클릭으로 선택된 행을
                  그리드 가시 영역으로 스크롤(FR-13 차트↔그리드 동기).
   Created By   : GHLee
   Created On   : 2026-07-23
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public partial class DetectionHistoryDialogView : UserControl
{
    public DetectionHistoryDialogView()
    {
        InitializeComponent();
        HistoryGrid.SelectionChanged += OnHistorySelectionChanged;
    }

    /// <summary>
    /// 틀(<c>ConsoleDialogFrame</c>)의 취소 — 머리 ✕ · ESC — 를 [닫기] 버튼(<c>x:Name="CloseDialog"</c>)의 Click 으로 넘긴다(B4).
    /// 닫는 길이 그 버튼 하나라 두 번 닫히지 않고, Caliburn 액션(<c>CloseDialog()</c>)까지 예전과 같은 길을 탄다. 꺼진 버튼은 누르지 않는다.
    /// </summary>
    private void OnSecondaryInvoked(object sender, RoutedEventArgs e)
    {
        if (!CloseDialog.IsEnabled || CloseDialog.Visibility != Visibility.Visible) return;
        CloseDialog.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, CloseDialog));
    }

    private void OnHistorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryGrid.SelectedItem != null)
            HistoryGrid.ScrollIntoView(HistoryGrid.SelectedItem);
    }
}
