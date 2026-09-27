using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Views.Panels;
/// <summary>
/// Interaction logic for LoginPanelView.xaml
/// </summary>
public partial class LoginPanelView : UserControl
{
    public LoginPanelView()
    {
        InitializeComponent();
        // 첫 포커스는 아이디 칸(옛 창과 같다) — 커널 틀은 뜰 때 첫 포커스를 머리 ✕ 에 준다(틀 버튼을 비웠으므로).
        // 그보다 뒤(Input 우선순위)에 아이디 칸으로 돌려 둔다 — 칸의 TextBoxFocusAndCaretBehavior 가 캐럿을 끝으로 보낸다.
        Loaded += (_, _) => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() => TextBoxID.Focus()));
    }

    /// <summary>틀의 취소(ESC · 머리 ✕)를 이 창의 [취소] 버튼(x:Name="ClickCancel")으로 넘긴다 — <see cref="DialogCancelRoute"/>.</summary>
    private void OnSecondaryInvoked(object sender, RoutedEventArgs e) => DialogCancelRoute.Invoke(ClickCancel);
}

