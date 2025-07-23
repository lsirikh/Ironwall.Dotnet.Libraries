using System;

namespace Ironwall.Dotnet.Libraries.Enums;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/7/2025 9:23:18 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public enum EnumDetectionType : int
{
    NONE = 0,               //0
    CABLE_CUTTING = 1,      //1
    CABLE_CONNECTED = 2,    //2
    PIR_SENSOR = 3,         //3
    THERMAL_SENSOR = 5,     //4
    VIBRATION_SENSOR = 6,   //5
    CONTACT_SENSOR = 10,     //10
    DISTANCE_SENSOR = 11,    //11
}