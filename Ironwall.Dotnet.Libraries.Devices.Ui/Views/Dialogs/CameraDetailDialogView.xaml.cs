using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Views.Dialogs;

/// <summary>
/// 카메라 상세 탭 몸통. 제목 · 닫기 · 저장은 이 뷰를 담는 호스트 창의 커널 틀이 맡는다(B4 — 틀이 두 겹이 되지 않게).
/// </summary>
public partial class CameraDetailDialogView : UserControl
{
    public CameraDetailDialogView()
    {
        InitializeComponent();
    }
}
