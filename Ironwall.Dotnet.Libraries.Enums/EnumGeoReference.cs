using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace Ironwall.Dotnet.Libraries.Enums;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/24/2025 8:28:16 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 지리참조 방식
/// (기존 GeoReferenceMethod → EnumGeoReference)
/// </summary>
public enum EnumGeoReference
{
    /// <summary>
    /// 자동 (기존 GeoTIFF 정보 사용)
    /// </summary>
    [Display(Name = "자동")]
    [Description("GeoTIFF 파일의 기존 지리참조 정보 사용")]
    Automatic = 1,

    /// <summary>
    /// 수동 기준점 입력
    /// </summary>
    [Display(Name = "수동 기준점")]
    [Description("사용자가 직접 기준점을 입력하여 지리참조")]
    ManualControlPoints = 2,

    /// <summary>
    /// 경계 좌표 입력
    /// </summary>
    [Display(Name = "경계 좌표")]
    [Description("이미지의 4개 모서리 좌표를 입력")]
    BoundingBox = 3,

    /// <summary>
    /// World 파일 사용
    /// </summary>
    [Display(Name = "World 파일")]
    [Description("별도의 World 파일(.tfw)을 사용")]
    WorldFile = 4
}