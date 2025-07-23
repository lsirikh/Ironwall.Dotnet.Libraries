using Microsoft.Xaml.Behaviors;
using System;
using System.Windows.Controls;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Utils.Behaviors;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/10/2025 6:17:24 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class CheckBoxItemClickBehavior<TRow> : Behavior<CheckBox>
        where TRow : class
{
    protected override void OnAttached()
    {
        AssociatedObject.Click += OnClick;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Click -= OnClick;
    }

    private void OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkbox)
            return;

        if ((checkbox.DataContext is not TRow row))
            return;

        var isChecked = checkbox.IsChecked ?? false;

        var prop = typeof(TRow).GetProperty("IsSelected");
        if (prop != null && prop.CanWrite)
            prop.SetValue(row, isChecked);
    }
}