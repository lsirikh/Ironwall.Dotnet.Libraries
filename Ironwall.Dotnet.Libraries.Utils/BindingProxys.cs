using System;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Utils;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/11/2025 6:19:29 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
//public class BindingProxy : Freezable
//{
//    protected override Freezable CreateInstanceCore()
//    {
//        return new BindingProxy();
//    }

//    public object Data
//    {
//        get { return GetValue(DataProperty); }
//        set { SetValue(DataProperty, value); }
//    }

//    public static readonly DependencyProperty DataProperty =
//        DependencyProperty.Register("Data", typeof(object), typeof(BindingProxy));
//}

public class BindingProxy : Freezable
{

    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object),
            typeof(BindingProxy), new UIPropertyMetadata(null));

    protected override Freezable CreateInstanceCore() => new BindingProxy();
}