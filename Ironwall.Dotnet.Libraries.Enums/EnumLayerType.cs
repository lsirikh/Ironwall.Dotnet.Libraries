using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace Ironwall.Dotnet.Libraries.Enums;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/24/2025 8:27:03 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// EnumLayerType을 구성한 이유:
/// 
/// 1. 복잡한 GIS 시스템에서 레이어 관리 필요
/// 2. 지도 위에 다양한 정보를 계층적으로 표시
/// 3. 사용자가 레이어별로 ON/OFF 제어
/// 4. 렌더링 순서와 스타일 적용 방식이 다름
/// </summary>
public enum EnumLayerType
{
    [Display(Name = "기본 레이어")]
    [Description("배경이 되는 기본 지도 레이어")]
    Base = 1,

    [Display(Name = "오버레이")]
    [Description("기본 지도 위에 겹쳐지는 추가 정보 레이어")]
    Overlay = 2,

    [Display(Name = "마커")]
    [Description("특정 위치를 표시하는 점 레이어")]
    Marker = 3,

    [Display(Name = "벡터")]
    [Description("선, 면으로 구성된 벡터 데이터 레이어")]
    Vector = 4,

    [Display(Name = "주석")]
    [Description("텍스트, 화살표 등 설명 정보 레이어")]
    Annotation = 5
}