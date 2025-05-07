namespace Ironwall.Dotnet.Framework.Helpers;

/****************************************************************************
    Purpose      :                                                           
    Created By   : GHLee                                                
    Created On   : 8/21/2023 2:54:52 PM                                                    
    Department   : SW Team                                                   
    Company      : Sensorway Co., Ltd.                                       
    Email        : lsirikh@naver.com                                         
 ****************************************************************************/

public static class DateTimeHelper
{

    public static DateTime GetCurrentTimeWithoutMS()
    {
        DateTime now = DateTime.Now;
        DateTime roundedNow = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
        return roundedNow;
    }

    public static DateTime GetInputTimeWithoutMS(DateTime dateTime)
    {
        DateTime roundedNow = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second);
        return roundedNow;
    }
}
