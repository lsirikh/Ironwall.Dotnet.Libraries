using System.Windows.Controls;

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

    private void OnHistorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryGrid.SelectedItem != null)
            HistoryGrid.ScrollIntoView(HistoryGrid.SelectedItem);
    }
}
