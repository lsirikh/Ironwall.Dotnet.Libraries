using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 워크벤치 뷰 코드비하인드 — 선택 동기화 · 키보드 폴백
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 이벤트 맵핑 워크벤치 뷰.
/// </summary>
/// <remarks>
/// 여기에는 <b>시각 배선만</b> 둔다 — 판정은 전부 뷰모델과 순수 함수에 있다.
/// <c>ListBox.SelectedItems</c> 는 바인딩할 수 없어서 선택만 여기서 뷰모델 컬렉션으로 옮긴다.
/// </remarks>
public partial class MappingWorkbenchView : UserControl
{
    /// <summary>생성자.</summary>
    public MappingWorkbenchView()
    {
        InitializeComponent();
    }

    private MappingWorkbenchViewModel? Model => DataContext as MappingWorkbenchViewModel;

    #region - 툴바 · 막대 -
    private void OnAdd(object sender, RoutedEventArgs e) => _ = Model?.CreateMappingAsync();

    private void OnReload(object sender, RoutedEventArgs e) => _ = Model?.ReloadAsync();

    private void OnApply(object sender, RoutedEventArgs e) => _ = Model?.ApplyAsync();

    private void OnRevert(object sender, RoutedEventArgs e) => _ = Model?.RevertAsync();

    private void OnSaveMapping(object sender, RoutedEventArgs e) => _ = Model?.SaveMappingAsync();

    private void OnCancelCreate(object sender, RoutedEventArgs e) => Model?.CancelCreateMapping();

    /// <summary>
    /// 툴바의 [삭제] 를 숨긴다 — 이 창은 맵핑 삭제를 제공하지 않는다(PRD 범위 밖).
    /// </summary>
    /// <remarks>
    /// 늘 꺼진 채 "제공하지 않습니다" 를 말하는 버튼은 동작하는 척하는 자리표시다(감사 E-10 #3).
    /// 커널 <c>ConsoleToolbar</c> 에는 아직 삭제 버튼을 끄는 속성이 없어, 템플릿 부품(<c>PART_Delete</c>)을
    /// 여기서 접는다. Loaded 는 창을 다시 붙일 때마다 오므로 템플릿이 다시 입혀져도 다시 접힌다.
    /// </remarks>
    private void OnToolbarLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Control toolbar) return;
        toolbar.ApplyTemplate();
        if (toolbar.Template?.FindName("PART_Delete", toolbar) is UIElement delete)
            delete.Visibility = Visibility.Collapsed;
    }
    #endregion

    #region - 보드 버튼 (드래그의 짝) -
    private void OnMoveUp(object sender, RoutedEventArgs e) => Model?.MoveUp();

    private void OnMoveDown(object sender, RoutedEventArgs e) => Model?.MoveDown();

    private void OnRelease(object sender, RoutedEventArgs e) => Model?.ReleaseSelected();

    private void OnAddSelected(object sender, RoutedEventArgs e) => Model?.AddSelected();
    #endregion

    #region - 선택 동기화 -
    private void OnBoardSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Model is null || sender is not ListBox list) return;

        Model.SelectedBoardRows.Clear();
        foreach (var item in list.SelectedItems.OfType<MappingRowViewModel>())
            Model.SelectedBoardRows.Add(item);
        Model.OnSelectionChanged();
    }

    private void OnPaletteSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Model is null || sender is not ListBox list) return;

        Model.SelectedPaletteItems.Clear();
        foreach (var item in list.SelectedItems.OfType<MappingPaletteItemViewModel>())
            Model.SelectedPaletteItems.Add(item);
        Model.OnSelectionChanged();
    }
    #endregion

    #region - 키보드 폴백 -
    /// <summary>
    /// 보드에서 <c>Delete</c> 는 해제 — 드래그백과 <b>같은 경로</b>를 부른다.
    /// </summary>
    /// <remarks>
    /// <c>Alt+↑↓</c> 는 커널의 <c>ReorderKeyboardBehavior</c> 가 잡는다(터널 + <c>Key.System</c>).
    /// 여기서 중복으로 처리하면 한 번 눌러 두 칸이 움직인다.
    /// </remarks>
    private void OnBoardPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Model is null) return;
        if (e.OriginalSource is TextBoxBase) return;        // 칸 안에서 지우는 중이면 건드리지 않는다

        if (e.Key is Key.Delete or Key.Left)
        {
            Model.ReleaseSelected();
            e.Handled = true;
        }
    }

    /// <summary>팔레트에서 <c>Enter</c>/<c>→</c> 는 투입 — [＋ 추가 ▶] 와 같은 경로다.</summary>
    private void OnPalettePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Model is null) return;
        if (e.OriginalSource is TextBoxBase) return;

        if (e.Key is Key.Enter or Key.Right)
        {
            if (!Model.CanAddSelected) return;              // 할 수 없을 때는 키를 소비하지 않는다
            Model.AddSelected();
            e.Handled = true;
        }
    }
    #endregion
}
