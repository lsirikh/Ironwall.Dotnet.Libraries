using System;

namespace Ironwall.Dotnet.Libraries.Base.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 3/24/2025 2:26:11 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/

public class SplashScreenMessage
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class InitialLoadFinishMessage
{
    public string ServiceName { get; set; } = string.Empty;
    public bool Result { get; set; }
}
