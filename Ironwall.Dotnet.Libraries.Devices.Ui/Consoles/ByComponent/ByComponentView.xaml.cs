using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.ByComponent;
/****************************************************************************
   Purpose      : "부품으로 찾기" 콘솔 탭 뷰 코드비하인드 (FR-17)
   Created By   : GHLee
   Created On   : 9/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// "부품으로 찾기" 탭 뷰. VM 의 필터 프로퍼티 세터는 값만 바꾸고 스스로 서버를 부르지 않는다
/// (<see cref="ByComponentViewModel"/> remarks 참조) — 콤보 · 칩 어느 쪽이든 선택이 바뀌면
/// 이 한 핸들러가 <see cref="ByComponentViewModel.SearchAsync"/> 를 명시적으로 부른다.
/// </summary>
public partial class ByComponentView : UserControl
{
    public ByComponentView()
    {
        InitializeComponent();
    }

    private void OnFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ByComponentViewModel vm) _ = vm.SearchAsync();
    }
}
