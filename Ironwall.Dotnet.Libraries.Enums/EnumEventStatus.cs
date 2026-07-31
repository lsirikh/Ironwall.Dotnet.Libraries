using System;

namespace Ironwall.Dotnet.Libraries.Enums;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/26/2025 6:35:54 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public enum EnumEventStatus
{
    Normal,
    Connection,
    Detecting,
    Fault,
    /// <summary>제어기 무통신(먹통) — 검은색. GMap_Controller_Blackout.</summary>
    Blackout,
}