using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace Ironwall.Dotnet.Libraries.Enums;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/24/2025 8:08:15 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
#region - EnumMapCategory -
/// <summary>
/// 지도 내용/용도별 분류
/// (기존 MapCategory → EnumMapCategory)
/// </summary>
public enum EnumMapCategory
{
    /// <summary>
    /// 일반 지도 (도로, 지명, 행정구역 등)
    /// </summary>
    [Display(Name = "일반지도")]
    [Description("도로, 지명, 행정구역이 표시된 기본 지도")]
    Standard = 1,

    /// <summary>
    /// 위성 지도 (항공/위성 사진)
    /// </summary>
    [Display(Name = "위성지도")]
    [Description("위성이나 항공기에서 촬영한 실제 지표면 이미지")]
    Satellite = 2,

    /// <summary>
    /// 하이브리드 (위성 + 도로/지명)
    /// </summary>
    [Display(Name = "하이브리드")]
    [Description("위성 이미지 위에 도로와 지명 정보가 겹쳐진 지도")]
    Hybrid = 3,

    /// <summary>
    /// 지형 지도 (등고선, 고도, 지형 특징)
    /// </summary>
    [Display(Name = "지형지도")]
    [Description("등고선과 고도 정보가 표시된 지형 중심 지도")]
    Terrain = 4,

    /// <summary>
    /// 군사 지도 (군사 작전, 보안 지역)
    /// </summary>
    [Display(Name = "군사지도")]
    [Description("군사 작전 및 보안 목적의 특수 지도")]
    Military = 5,

    /// <summary>
    /// 측량 지도 (정밀 좌표, 경계선)
    /// </summary>
    [Display(Name = "측량지도")]
    [Description("정밀 측량 데이터 기반의 공식 지도")]
    Survey = 6,

    /// <summary>
    /// 해상 지도 (항해용, 수심 정보)
    /// </summary>
    [Display(Name = "해상지도")]
    [Description("선박 항해를 위한 해상 정보 지도")]
    Marine = 7,

    /// <summary>
    /// 항공 지도 (비행 경로, 공항 정보)
    /// </summary>
    [Display(Name = "항공지도")]
    [Description("항공기 운항을 위한 항공 정보 지도")]
    Aviation = 8,

    /// <summary>
    /// 기타 (위 분류에 해당하지 않는 특수 목적)
    /// </summary>
    [Display(Name = "기타")]
    [Description("기타 특수 목적의 지도")]
    Other = 99
}
#endregion