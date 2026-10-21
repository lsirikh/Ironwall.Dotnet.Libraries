using System.Windows;
using System.Windows.Controls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;

/// <summary>
/// 3D 환경 설정 묶음 — DataContext = <see cref="Map3DEnvironmentViewModel"/>.
/// 인증키는 가림 입력(PasswordBox)이라 바인딩 대신 여기서 넣고 뺀다(포커스를 잃을 때 · [연결 시험] 직전).
/// </summary>
public partial class Map3DEnvironmentSettingsBlock : UserControl
{
    public Map3DEnvironmentSettingsBlock()
    {
        InitializeComponent();
    }

    private Map3DEnvironmentViewModel? Vm => DataContext as Map3DEnvironmentViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm) KeyBox.Password = vm.KmaServiceKey;
    }

    private void OnKeyLostFocus(object sender, RoutedEventArgs e) => CommitKey();

    private void OnTestClick(object sender, RoutedEventArgs e) => CommitKey();

    private void CommitKey()
    {
        if (Vm is { } vm && vm.KmaServiceKey != KeyBox.Password.Trim()) vm.KmaServiceKey = KeyBox.Password;
    }
}
