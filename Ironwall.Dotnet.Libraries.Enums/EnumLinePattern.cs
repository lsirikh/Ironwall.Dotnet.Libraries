using System;
using System.ComponentModel;

namespace Ironwall.Dotnet.Libraries.Enums;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/12/2025 10:48:32 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public enum EnumLinePattern
{
    [Description("실선")]
    Solid = 0,          // 실선 ——————
    [Description("점선")]
    Dashed = 1,         // 점선 — — — —
    [Description("점점선")]
    Dotted = 2,         // 점점선 ● ● ● ●
    [Description("점-선 패턴")]
    DashDot = 3,        // 점-선 패턴 —●—●—●
    [Description("이중선")]
    DoubleLine = 4,     // 이중선 ══════
    [Description("화살표 패턴")]
    Arrow = 5           // 화살표 패턴 →→→→
}