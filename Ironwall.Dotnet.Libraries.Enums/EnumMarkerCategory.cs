using System;

namespace Ironwall.Dotnet.Libraries.Enums;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/22/2025 6:34:11 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public enum EnumMarkerCategory
{
    /// <summary>
    /// 기본 도형 및 텍스트 마커 (범용)
    /// </summary>
    BASIC_SHAPES,

    /// <summary>
    /// 차량 및 교통수단 (민간)
    /// </summary>
    VEHICLES,

    /// <summary>
    /// 군사 부호/심볼 (NATO 표준 등)
    /// </summary>
    MILITARY_SYMBOLS,

    /// <summary>
    /// PIDS 시스템 장비 (침입탐지시스템)
    /// </summary>
    PIDS_EQUIPMENT,

    /// <summary>
    /// 지역/구역 표시 (경계, 도로 등)
    /// </summary>
    AREA_BOUNDARY,

    /// <summary>
    /// 분석/예측 마커 (추정 위치 등)
    /// </summary>
    ANALYSIS,

    /// <summary>
    /// 인프라/시설물
    /// </summary>
    INFRASTRUCTURE,

    /// <summary>
    /// 독립적 이벤트 심볼 (특수 상황용)
    /// </summary>
    EVENT_SYMBOLS
}