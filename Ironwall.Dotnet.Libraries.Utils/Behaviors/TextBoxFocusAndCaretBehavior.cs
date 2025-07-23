using Microsoft.Xaml.Behaviors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Utils.Behaviors;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/18/2025 4:51:21 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class TextBoxFocusAndCaretBehavior : Behavior<TextBox>
{
    protected override void OnAttached()
    {
        var tb = AssociatedObject;
        tb.Loaded += OnLoaded;
        tb.GotKeyboardFocus += OnGotKeyboardFocus;
        tb.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
    }

    protected override void OnDetaching()
    {
        var tb = AssociatedObject;
        tb.Loaded -= OnLoaded;
        tb.GotKeyboardFocus -= OnGotKeyboardFocus;
        tb.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 뷰가 처음 로드될 때 한 번
        AssociatedObject.Focus();
        MoveCaretToEnd();
    }

    private void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // 탭이나 코드로 포커스가 넘어올 때
        MoveCaretToEnd();
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var tb = (TextBox)sender;
        if (!tb.IsKeyboardFocusWithin)
        {
            e.Handled = true;    // 마우스 기본 포커스 동작 차단
            tb.Focus();          // 강제 포커스
            // GotKeyboardFocus 이벤트가 곧 발생 → 캐럿 이동
        }
    }

    private void MoveCaretToEnd()
    {
        var tb = AssociatedObject;
        tb.CaretIndex = tb.Text?.Length ?? 0;
    }
}