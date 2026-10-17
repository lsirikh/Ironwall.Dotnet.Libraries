using System.ComponentModel.DataAnnotations;
using System;

namespace Ironwall.Dotnet.Libraries.Enums;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/19/2025 7:48:04 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public enum EnumBuildingUsage
{
    [Display(Name = "업무")]
    Office,         // 업무
    [Display(Name = "생산")]
    Manufacturing,
    [Display(Name = "저장")]
    Storage,
    [Display(Name = "의료")]
    Medical,
    [Display(Name = "교육")]
    Education,
    [Display(Name = "판매")]
    Retail,
    [Display(Name = "주거")]
    Residential,
    [Display(Name = "공공")]
    Public,
    [Display(Name = "복합")]
    Mixed,
}
