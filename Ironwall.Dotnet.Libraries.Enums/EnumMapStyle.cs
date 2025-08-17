using System;

namespace Ironwall.Dotnet.Libraries.Enums;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/24/2025 8:29:11 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 지도 스타일
/// (기존 MapStyle → EnumMapStyle)
/// </summary>
public enum EnumMapStyle
{
    Normal = 1,
    Satellite = 2,
    Hybrid = 3,
    Terrain = 4,
    Physical = 5,
    Roads = 6,
    Labels = 7,
    Custom = 99
}