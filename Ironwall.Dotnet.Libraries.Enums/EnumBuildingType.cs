using System.ComponentModel.DataAnnotations;
using System;

namespace Ironwall.Dotnet.Libraries.Enums;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/19/2025 7:47:34 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public enum EnumBuildingType
{
    //General,        // 일반 건물
    //Office,         // 사무실
    [Display(Name = "공장")]
    Factory,        // 공장
    [Display(Name = "막사")]
    Barracks,
    [Display(Name = "초소")]
    GuardPost,
    [Display(Name = "감시탑")]
    Watchtower,
    [Display(Name = "창고")]
    Warehouse,
    [Display(Name = "게이트")]
    Gate,
    [Display(Name = "안테나")]
    Antenna,
    [Display(Name = "발전기")]
    Generator,
    [Display(Name = "물탱크")]
    WaterTank,
    [Display(Name = "헬리패드")]
    Helipad,
    [Display(Name = "교량")]
    Bridge,
    [Display(Name = "전신주")]
    PowerPole,
    //Warehouse,      // 창고
    //Hospital,       // 병원
    //School,         // 학교
    //Government,     // 정부청사
    //Commercial,     // 상업시설
    //Residential,    // 주거시설
    //PowerPlant,     // 발전소
    //DataCenter,     // 데이터센터
    //WaterTreatment, // 정수시설
    //Bridge,         // 교량
    //Tunnel          // 터널
}
