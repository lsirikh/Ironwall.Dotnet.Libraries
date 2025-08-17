using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace Ironwall.Dotnet.Libraries.Enums;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/24/2025 8:07:23 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
#region - EnumMapProvider -
/// <summary>
/// 지도 제공 방식 구분
/// (기존 MapProviderType → EnumMapProvider)
/// </summary>
public enum EnumMapProvider
{
    /// <summary>
    /// 기존 제공자 (Google, Bing, OpenStreetMap 등)
    /// </summary>
    [Display(Name = "기존 제공자")]
    [Description("Google, Bing 등 외부 서비스에서 제공하는 지도")]
    Defined = 1,

    /// <summary>
    /// 커스텀 생성 (직접 타일 생성한 지도)
    /// </summary>
    [Display(Name = "커스텀 생성")]
    [Description("TIFF 파일에서 직접 생성한 타일 지도")]
    Custom = 2
}
#endregion