using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Behaviors;
/****************************************************************************
   Purpose      : DataGrid 편집 중 저장 버튼 클릭 시 편집 내용 커밋
   Created By   : GHLee
   Created On   : 2025-11-26
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 버튼 클릭 시 연결된 DataGrid의 편집 내용을 커밋하는 Behavior
/// <para>문제: DataGrid에서 편집 모드 종료 전에 저장 버튼을 클릭하면
/// IsEdited 플래그가 설정되지 않아 수정사항이 저장되지 않음</para>
/// <para>해결: PreviewMouseLeftButtonDown 이벤트에서 CommitEdit() 호출</para>
/// </summary>
public class CommitEditOnClickBehavior : Behavior<Button>
{
    #region - Dependency Properties -
    public static readonly DependencyProperty DataGridProperty =
        DependencyProperty.Register(
            nameof(DataGrid),
            typeof(DataGrid),
            typeof(CommitEditOnClickBehavior));

    /// <summary>
    /// 편집 커밋 대상 DataGrid
    /// </summary>
    public DataGrid DataGrid
    {
        get => (DataGrid)GetValue(DataGridProperty);
        set => SetValue(DataGridProperty, value);
    }
    #endregion

    #region - Overrides -
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewMouseLeftButtonDown += OnPreviewClick;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.PreviewMouseLeftButtonDown -= OnPreviewClick;
        base.OnDetaching();
    }
    #endregion

    #region - Event Handlers -
    /// <summary>
    /// 버튼 클릭 전 DataGrid 편집 커밋
    /// PreviewMouseLeftButtonDown을 사용하여 Click 이벤트보다 먼저 실행
    /// </summary>
    private void OnPreviewClick(object sender, MouseButtonEventArgs e)
    {
        // Cell 편집 커밋
        DataGrid?.CommitEdit(DataGridEditingUnit.Cell, true);
        // Row 편집 커밋
        DataGrid?.CommitEdit(DataGridEditingUnit.Row, true);
    }
    #endregion
}
