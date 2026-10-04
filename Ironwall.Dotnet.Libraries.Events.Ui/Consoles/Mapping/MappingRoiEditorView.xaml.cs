using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 프리셋 ROI 설정 창 코드비하인드 — 버튼 · 키 배선만
   Created By   : Claude
   Created On   : 2026-10-04
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>프리셋 ROI 설정 뷰. 판정은 전부 <see cref="MappingRoiEditorViewModel"/> 에 있다.</summary>
public partial class MappingRoiEditorView : UserControl
{
    /// <summary>생성자.</summary>
    public MappingRoiEditorView()
    {
        InitializeComponent();
    }

    private MappingRoiEditorViewModel? Model => DataContext as MappingRoiEditorViewModel;

    /// <summary>
    /// 캔버스에 초점이 있으면 키를 캔버스에 먼저 준다 — 대화상자 틀이 <c>PreviewKeyDown</c> 에서 ESC 를 닫기로 먹기 전에
    /// (이 뷰가 틀의 조상이라 터널링에서 먼저 받는다).
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled || Keyboard.FocusedElement is not MappingRoiCanvas canvas) return;
        if (canvas.HandleKey(e.Key)) e.Handled = true;
    }

    private void OnNew(object sender, RoutedEventArgs e) => Model?.BeginNew();

    private void OnUndo(object sender, RoutedEventArgs e) => Model?.UndoPoint();

    private void OnClosePolygon(object sender, RoutedEventArgs e) => Model?.ClosePolygon();

    private void OnRedraw(object sender, RoutedEventArgs e) => Model?.Redraw();

    private void OnSave(object sender, RoutedEventArgs e) => _ = Model?.SaveAsync();

    private void OnDelete(object sender, RoutedEventArgs e) => Model?.RequestDelete();

    private void OnCancelDelete(object sender, RoutedEventArgs e) => Model?.CancelDelete();

    private void OnConfirmDelete(object sender, RoutedEventArgs e) => _ = Model?.ConfirmDeleteAsync();

    private void OnClose(object sender, RoutedEventArgs e) => _ = Model?.CloseAsync();

    private void OnRetryVideo(object sender, RoutedEventArgs e) => Model?.Video?.Retry();

    private void OnGotoPreset(object sender, RoutedEventArgs e) => _ = Model?.GotoPresetAsync();
}
