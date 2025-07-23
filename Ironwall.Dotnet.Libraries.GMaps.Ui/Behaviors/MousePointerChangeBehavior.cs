using Microsoft.Xaml.Behaviors;
using System;
using System.Windows.Input;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Behaviors;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 10/18/2024 3:00:17 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class MousePointerChangeBehavior : Behavior<UIElement>
{
    // ViewModel에서 커서 이미지 경로를 받아오기 위한 DependencyProperty
    public string CursorImagePath
    {
        get { return (string)GetValue(CursorImagePathProperty); }
        set { SetValue(CursorImagePathProperty, value); }
    }

    public static readonly DependencyProperty CursorImagePathProperty =
        DependencyProperty.Register("CursorImagePath", typeof(string), typeof(MousePointerChangeBehavior), new PropertyMetadata(null, OnCursorImagePathChanged));

    private static void OnCursorImagePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var behavior = d as MousePointerChangeBehavior;
        behavior.UpdateCursor();
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        UpdateCursor();
    }

    private void UpdateCursor()
    {
        if (!string.IsNullOrEmpty(CursorImagePath))
        {
            var cursor = CreateCursorFromImage(CursorImagePath);
            Mouse.OverrideCursor = cursor;
        }
        else
        {
            Mouse.OverrideCursor = null;
        }
    }

    private Cursor CreateCursorFromImage(string imagePath)
    {
        var uri = new Uri(imagePath, UriKind.RelativeOrAbsolute);
        var stream = Application.GetResourceStream(uri)?.Stream;
        return stream != null ? new Cursor(stream) : Cursors.Arrow;
    }
}