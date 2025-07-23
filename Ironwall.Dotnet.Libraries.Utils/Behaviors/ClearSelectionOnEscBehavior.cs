using Microsoft.Xaml.Behaviors;
using System;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Utils.Behaviors;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/10/2025 2:41:53 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>DataGrid에서 <kbd>ESC</kbd> 키를 누르면 선택을 해제한다.</summary>
public sealed class ClearSelectionOnEscBehavior : Behavior<DataGrid>
{
    protected override void OnAttached()
    {
        AssociatedObject.KeyDown += OnKeyDown;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.KeyDown -= OnKeyDown;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        var dg = AssociatedObject;
        dg.SelectedItem = null;
        dg.SelectedIndex = -1;
        dg.UnselectAll();                  // 멀티 선택 지원 시

        // 바인딩에 즉시 반영되도록
        if (dg.CommitEdit())
            dg.Items.Refresh();

        e.Handled = true;                  // ESC 기본 동작(편집 취소 등) 막고 싶지 않다면 false 로
    }
}