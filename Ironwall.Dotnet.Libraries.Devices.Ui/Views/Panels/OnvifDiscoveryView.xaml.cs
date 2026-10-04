using System.Windows;
using System.Windows.Controls;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Views.Panels;

/// <summary>[ONVIF 검색] 펼침 영역. PasswordBox 는 바인딩이 안 되어 여기서 넘긴다.</summary>
public partial class OnvifDiscoveryView : UserControl
{
    public OnvifDiscoveryView()
    {
        InitializeComponent();
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is OnvifDiscoveryViewModel vm && sender is PasswordBox box)
            vm.Password = box.Password;
    }
}
