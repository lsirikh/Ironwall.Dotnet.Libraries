using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;

namespace GMap.NET.WindowsPresentation
{
    public static class GMapUtil
    {
        public static FrameworkElement GetFrameworkElement(DependencyObject parent, string name)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is FrameworkElement && ((FrameworkElement)child).Name == name)
                {
                    return child as FrameworkElement;
                }
                else
                {
                    var target = GetFrameworkElement(child, name);

                    if (target != null)
                    {
                        return target;
                    }
                }
            }

            return null;
        }
    }

}
