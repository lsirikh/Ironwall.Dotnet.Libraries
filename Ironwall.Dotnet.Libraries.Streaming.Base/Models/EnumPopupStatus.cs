using System;

namespace Ironwall.Dotnet.Libraries.Streaming.Base.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 10/10/2025 11:21:40 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 이벤트 처리 상태
/// </summary>
public enum EnumPopupStatus
{
    Idle,       // 대기 중
    Processing,    // 처리 중
    Completed,     // 완료
    Cancelled,     // 취소됨
    Failed         // 실패
}