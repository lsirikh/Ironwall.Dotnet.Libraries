using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace Ironwall.Dotnet.Libraries.Enums;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/24/2025 8:14:47 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
#region - EnumMapStatus -
/// <summary>
/// 지도 상태 관리
/// (기존 MapStatus → EnumMapStatus)
/// </summary>
public enum EnumMapStatus
{
    /// <summary>
    /// 활성 (정상 사용 가능)
    /// </summary>
    [Display(Name = "활성")]
    [Description("정상적으로 사용 가능한 상태")]
    Active = 1,

    /// <summary>
    /// 비활성 (일시적 사용 중단)
    /// </summary>
    [Display(Name = "비활성")]
    [Description("일시적으로 사용이 중단된 상태")]
    Inactive = 2,

    /// <summary>
    /// 처리 중 (생성/변환 작업 진행)
    /// </summary>
    [Display(Name = "처리중")]
    [Description("타일 생성이나 변환 작업이 진행 중인 상태")]
    Processing = 3,

    /// <summary>
    /// 오류 (처리 실패, 문제 발생)
    /// </summary>
    [Display(Name = "오류")]
    [Description("처리 실패나 오류가 발생한 상태")]
    Error = 4,

    /// <summary>
    /// 삭제됨 (논리적 삭제)
    /// </summary>
    [Display(Name = "삭제됨")]
    [Description("삭제된 상태 (복구 가능)")]
    Deleted = 5
}
#endregion