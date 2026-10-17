using System;

namespace Ironwall.Dotnet.Libraries.Enums
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/25/2024 2:16:13 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    /// <summary>
    /// 강풍 모드 (NATS v1.2 §5.1 PidsProxy WINDY 제어)
    /// wind0=평상, wind1=약풍, wind2=강풍, wind3=태풍
    /// </summary>
    public enum EnumWindyMode
    {
        wind0,
        wind1,
        wind2,
        wind3
    }
}