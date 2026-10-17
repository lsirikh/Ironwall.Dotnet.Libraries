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

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Views.Dialogs;
/// <summary>
/// Interaction logic for RegisterDialogView.xaml
/// </summary>
public partial class RegisterDialogView : UserControl
{
    public RegisterDialogView()
    {
        InitializeComponent();
        // 첫 포커스는 아이디 칸(옛 창과 같다) — 커널 틀이 뜰 때 머리 ✕ 에 준 첫 포커스보다 뒤(Input 우선순위)에 돌려 둔다.
        Loaded += (_, _) => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() => Username.Focus()));
    }

    /// <summary>틀의 취소(ESC · 머리 ✕)를 이 창의 [취소] 버튼(x:Name="ClickCancel")으로 넘긴다 — <see cref="DialogCancelRoute"/>.</summary>
    private void OnSecondaryInvoked(object sender, RoutedEventArgs e) => DialogCancelRoute.Invoke(ClickCancel);
}

