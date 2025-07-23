using Microsoft.Xaml.Behaviors;
using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Utils.Behaviors;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/18/2025 5:31:18 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 로드되거나 포커스가 필요할 때 버튼에 자동으로 포커스를 줍니다.
/// </summary>
public sealed class ButtonFocusBehavior : Behavior<Button>
{
    protected override void OnAttached()
    {
        AssociatedObject.Loaded += OnLoaded;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Loaded -= OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AssociatedObject.Focus();  // 키보드 포커스
        Keyboard.Focus(AssociatedObject);
    }
}