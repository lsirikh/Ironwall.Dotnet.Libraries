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
/// Interaction logic for LogoutPanelView.xaml
/// </summary>
public partial class LogoutPanelView : UserControl
{
    public LogoutPanelView()
    {
        InitializeComponent();
    }

    /// <summary>틀의 취소(ESC · 머리 ✕)를 이 창의 [취소] 버튼(x:Name="ClickCancel")으로 넘긴다 — <see cref="DialogCancelRoute"/>.</summary>
    private void OnSecondaryInvoked(object sender, RoutedEventArgs e) => DialogCancelRoute.Invoke(ClickCancel);
}
