using System;

namespace Ironwall.Dotnet.Libraries.Enums;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/26/2025 6:33:45 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 심각도 (간단 버전)
/// </summary>
public enum EnumSeverityLevel
{
    NORMAL = 1,    // 평소
    CAUTION = 2,    // 주의
    WARNING = 3,    // 경고  
    CRITICAL = 4    // 심각
}