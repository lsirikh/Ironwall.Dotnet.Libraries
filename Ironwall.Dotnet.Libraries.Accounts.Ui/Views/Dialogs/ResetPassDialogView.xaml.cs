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
/// Interaction logic for ResetPassDialogView.xaml
/// </summary>
public partial class ResetPassDialogView : UserControl
{
    public ResetPassDialogView()
    {
        InitializeComponent();
        // 창이 뜨면 첫 칸(현재 비밀번호)에서 바로 입력한다 — 틀의 첫 포커스(✕)보다 뒤(Input 우선순위)에 준다.
        Loaded += (_, _) => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() => InputPassword.Focus()));
    }

    /// <summary>틀의 취소(ESC · 머리 ✕)를 이 창의 [취소] 버튼(x:Name="ClickCancel")으로 넘긴다 — <see cref="DialogCancelRoute"/>.</summary>
    private void OnSecondaryInvoked(object sender, RoutedEventArgs e) => DialogCancelRoute.Invoke(ClickCancel);
}

