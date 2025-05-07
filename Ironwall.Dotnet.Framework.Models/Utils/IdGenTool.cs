using System;

namespace Ironwall.Dotnet.Framework.Models.Utils;

/****************************************************************************
    Purpose      :                                                           
    Created By   : GHLee                                                
    Created On   : 2/5/2024 2:53:45 PM                                                    
    Department   : SW Team                                                   
    Company      : Sensorway Co., Ltd.                                       
    Email        : lsirikh@naver.com                                         
 ****************************************************************************/

public static class IdGenTool
{
    public static string GenIdCode()
    {
        var now = DateTime.Now;
        return $"{now.ToString("yy")}{now.ToString("MM")}{now.ToString("dd")}{now.ToString("HH")}{now.ToString("mm")}{now.ToString("ss")}{now.ToString("fff")}";
    }

    public static int GenIdInt()
    {
        var now = DateTime.Now;
        return int.Parse(now.Ticks.ToString());
    }
}
