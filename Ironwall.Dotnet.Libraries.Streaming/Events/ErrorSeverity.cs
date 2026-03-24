using System;

namespace Ironwall.Dotnet.Libraries.Streaming.Events;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/24/2025 3:12:45 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 에러 심각도
/// </summary>
public enum ErrorSeverity
{
    Info,
    Warning,
    Error,
    Critical
}