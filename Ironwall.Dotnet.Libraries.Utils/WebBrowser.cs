using Microsoft.Win32;
using System;

namespace Ironwall.Dotnet.Libraries.Utils;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/7/2025 9:49:25 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public static class WebBrowser
{
    #region - Static Procedures -
    public static string WebBrowserChrome
    {
        get
        {
            return (string)(Registry.GetValue("HKEY_LOCAL_MACHINE" + ChromeAppKey, "", null) ??
                                Registry.GetValue("HKEY_CURRENT_USER" + ChromeAppKey, "", null));
        }
    }
    #endregion

    #region - Attributes = 
    private const string ChromeAppKey = @"\Software\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe";
    #endregion
}