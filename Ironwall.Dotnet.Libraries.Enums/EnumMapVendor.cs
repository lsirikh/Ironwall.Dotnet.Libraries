using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace Ironwall.Dotnet.Libraries.Enums;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/24/2025 8:26:01 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// EnumMapVendor를 구성한 이유:
/// 
/// 1. 기존 제공자 지도의 "회사" 구분
/// 2. 각 회사별 API 설정 방식이 다름
/// 3. 라이센스, 사용 제한, 요금 정책이 회사별로 상이
/// 4. GMap.NET에서 지원하는 Provider들을 체계적으로 관리
/// </summary>
public enum EnumMapVendor
{
    [Display(Name = "구글")]
    [Description("Google Maps API - 높은 품질, 유료 API 키 필요")]
    Google = 1,

    [Display(Name = "마이크로소프트")]
    [Description("Bing Maps API - Bing 검색과 통합, 기업용 특화")]
    Microsoft = 2,      // Bing

    [Display(Name = "오픈스트리트맵")]
    [Description("OpenStreetMap - 무료 오픈소스, 커뮤니티 기반")]
    OpenStreetMap = 3,

    [Display(Name = "Esri")]
    [Description("ArcGIS Online - GIS 전문, 고급 분석 기능")]
    Esri = 4,           // ArcGIS

    [Display(Name = "야후")]
    [Description("Yahoo Maps - 레거시 서비스")]
    Yahoo = 5,

    [Display(Name = "얀덱스")]
    [Description("Yandex Maps - 러시아/동유럽 특화")]
    Yandex = 6,

    [Display(Name = "Mapy.cz")]
    [Description("Mapy.cz - 체코 로컬 지도 서비스")]
    Mapy = 7,

    [Display(Name = "위키맵피아")]
    [Description("WikiMapia - 위키 기반 지도 정보")]
    WikiMapia = 8,

    [Display(Name = "맵퀘스트")]
    [Description("MapQuest - 미국 기반 내비게이션")]
    MapQuest = 9,

    [Display(Name = "클라우드메이드")]
    [Description("CloudMade - OSM 기반 상용 서비스")]
    CloudMade = 10,

    [Display(Name = "니어맵")]
    [Description("NearMap - 고해상도 항공사진 특화")]
    NearMap = 11,

    [Display(Name = "기타")]
    [Description("기타 지도 제공업체")]
    Other = 99
}