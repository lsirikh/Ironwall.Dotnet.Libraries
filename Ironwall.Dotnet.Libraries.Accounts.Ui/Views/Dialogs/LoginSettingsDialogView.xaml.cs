using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;
using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Views.Dialogs;

/// <summary>
/// 로그인 설정 창 — 틀의 주 · 보조 동작(버튼 · Enter · ESC · 머리 ✕)을 뷰모델로 넘긴다.
/// <para>틀은 로그인 패널 칸 위 덮개에 얹혀 아래 로그인 틀의 조상이 아니다 — 그래도 사건이 위로 번지지 않게 여기서 끝낸다.</para>
/// </summary>
public partial class LoginSettingsDialogView : UserControl
{
    public LoginSettingsDialogView() => InitializeComponent();

    private void OnPrimaryInvoked(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        (DataContext as LoginSettingsDialogViewModel)?.Primary();
    }

    private void OnSecondaryInvoked(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        (DataContext as LoginSettingsDialogViewModel)?.Secondary();
    }
}
