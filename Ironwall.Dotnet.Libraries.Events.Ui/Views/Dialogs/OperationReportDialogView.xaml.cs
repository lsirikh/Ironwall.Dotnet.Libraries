using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Views.Dialogs
{
    /// <summary>
    /// 운영 조치보고 창(operation-event-card PRD v1.1 §3). 모양 · 키 · 첫 포커스는 커널 틀(<c>ConsoleDialogFrame</c>)이 정한다.
    /// </summary>
    public partial class OperationReportDialogView : UserControl
    {
        public OperationReportDialogView()
        {
            InitializeComponent();
        }

        /// <summary>틀의 취소(✕ · ESC)를 [취소] 버튼의 Click 으로 넘긴다 — 장애 창과 같다. 호출 스레드: UI.</summary>
        private void OnSecondaryInvoked(object sender, RoutedEventArgs e)
        {
            if (!ClickCancel.IsEnabled || ClickCancel.Visibility != Visibility.Visible) return;
            ClickCancel.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, ClickCancel));
        }
    }
}
