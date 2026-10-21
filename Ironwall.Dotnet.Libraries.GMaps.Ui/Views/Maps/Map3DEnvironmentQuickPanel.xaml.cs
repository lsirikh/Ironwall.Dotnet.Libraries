using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;

/// <summary>3D 위 「환경」 빠른 조절 판 — DataContext = <see cref="ViewModels.Maps.Map3DEnvironmentViewModel"/>.</summary>
public partial class Map3DEnvironmentQuickPanel : UserControl
{
    public Map3DEnvironmentQuickPanel()
    {
        InitializeComponent();
        // VM 이 없으면(미등록 호스트) 누를 것 없는 알약을 남기지 않는다
        Visibility = System.Windows.Visibility.Collapsed;
        DataContextChanged += (_, e) => Visibility = e.NewValue is ViewModels.Maps.Map3DEnvironmentViewModel
            ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }
}
