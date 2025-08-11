using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace Ironwall.Dotnet.Libraries.Enums;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/24/2025 8:10:10 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
#region - EnumMapData -
/// <summary>
/// 지도 데이터 형식 구분
/// (기존 MapDataType → EnumMapData)
/// </summary>
public enum EnumMapData
{
    /// <summary>
    /// 래스터 (픽셀 기반, 비트맵 이미지)
    /// </summary>
    [Display(Name = "래스터")]
    [Description("픽셀 기반의 이미지 데이터 (TIFF, PNG 등)")]
    Raster = 1,

    /// <summary>
    /// 벡터 (도형 기반, 수학적 표현)
    /// </summary>
    [Display(Name = "벡터")]
    [Description("점, 선, 면으로 구성된 기하학적 데이터")]
    Vector = 2,

    /// <summary>
    /// 하이브리드 (래스터 + 벡터 조합)
    /// </summary>
    [Display(Name = "하이브리드")]
    [Description("래스터와 벡터 데이터가 결합된 형태")]
    Hybrid = 3
}
#endregion