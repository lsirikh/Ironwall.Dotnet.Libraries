using System;
using System.Windows;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Base.Services;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 1/23/2025 7:17:47 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public static class DispatcherService
{
    public static void Invoke(Action action)
    {
        Dispatcher? dispatchObject = Application.Current != null ? Application.Current.Dispatcher : null;
        if (dispatchObject == null || dispatchObject.CheckAccess())
            action();
        else
            dispatchObject.Invoke(action);
    }

    public static async Task BeginInvoke(Action action)
    {
        Dispatcher? dispatchObject = Application.Current != null ? Application.Current.Dispatcher : null;
        if (dispatchObject == null || dispatchObject.CheckAccess())
            action();
        else
            await dispatchObject.BeginInvoke(action);
    }
}