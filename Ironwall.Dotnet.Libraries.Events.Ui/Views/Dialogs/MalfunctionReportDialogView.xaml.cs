using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Views.Dialogs
{
    /// <summary>
    /// 장애 조치보고 창(B5). 모양 · 키 · 첫 포커스는 커널 틀(<c>ConsoleDialogFrame</c>)이 정한다.
    /// </summary>
    public partial class MalfunctionReportDialogView : UserControl
    {
        public MalfunctionReportDialogView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 틀의 취소(머리 ✕ · ESC)를 [취소] 버튼(<c>x:Name="ClickCancel"</c>)의 Click 으로 넘긴다 — 닫는 길이 그 버튼 하나라
        /// 두 번 닫히지 않고, Caliburn 액션(<c>ClickCancel()</c>)까지 예전과 같은 길을 탄다. 꺼진 버튼은 누르지 않는다. 호출 스레드: UI.
        /// </summary>
        private void OnSecondaryInvoked(object sender, RoutedEventArgs e)
        {
            if (!ClickCancel.IsEnabled || ClickCancel.Visibility != Visibility.Visible) return;
            ClickCancel.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, ClickCancel));
        }
    }
}
