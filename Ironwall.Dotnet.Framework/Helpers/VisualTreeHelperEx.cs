using System;
using System.Windows.Media;
using System.Windows;

namespace Ironwall.Dotnet.Framework.Helpers;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/22/2025 2:34:31 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public static class VisualTreeHelperEx
{
    /// <summary>
    /// 주어진 <paramref name="parent"/> 아래에서 가장 먼저 발견되는 타입 <typeparamref name="T"/> 자식 반환
    /// </summary>
    public static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;

        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T target)
                return target;

            var nested = FindChild<T>(child);
            if (nested != null)
                return nested;
        }
        return null;
    }
}