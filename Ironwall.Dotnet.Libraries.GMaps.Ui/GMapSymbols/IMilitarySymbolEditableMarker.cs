using Ironwall.Dotnet.Libraries.Enums;
using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/8/2025 8:29:10 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 군사 심볼 마커 전용 인터페이스
/// NATO APP-6D 표준 기반 군사 심볼 속성 정의
/// </summary>
public interface IMilitarySymbolEditableMarker : IEditableMarker
{
    #region - Core Military Symbol Properties (NATO APP-6D Required) -

    /// <summary>
    /// 소속 구분 (아군/적군/중립/미확인)
    /// 심볼의 기본 형태와 색상을 결정
    /// </summary>
    EnumMilitaryAffiliation Affiliation { get; set; }

    /// <summary>
    /// 전투 차원 (육상/해상/공중/우주/전자전 등)
    /// 심볼의 기본 프레임 모양을 결정
    /// </summary>
    EnumMilitaryBattleDimension BattleDimension { get; set; }

    /// <summary>
    /// 표준 정체성 (현재/계획/가정)
    /// 심볼의 선 스타일(실선/점선 등)을 결정
    /// </summary>
    EnumMilitaryStandardIdentity StandardIdentity { get; set; }

    /// <summary>
    /// 부대 종류 (보병/기갑/포병/공병 등)
    /// 심볼 내부 아이콘을 결정
    /// </summary>
    EnumMilitaryUnitType UnitType { get; set; }

    /// <summary>
    /// 부대 규모 (분대~군단급)
    /// 심볼 상단의 계급장 표시를 결정
    /// </summary>
    EnumMilitaryUnitSize UnitSize { get; set; }

    #endregion

    #region - Unit Identification Properties (Optional Display Text) -

    /// <summary>
    /// 부대 지시자 (부대명 또는 번호)
    /// 예: "1-501 IN", "Alpha Company", "제1기갑대대"
    /// 심볼 하단에 표시되는 텍스트
    /// </summary>
    string? UnitDesignator { get; set; }

    /// <summary>
    /// 상급 부대명 (Higher Formation)
    /// 예: "3BCT", "제7보병사단"
    /// 심볼 상단에 표시되는 텍스트
    /// </summary>
    string? HigherFormation { get; set; }

    /// <summary>
    /// 부대 별명 또는 콜사인
    /// 예: "Tiger", "Bravo-6"
    /// 심볼 좌측에 표시되는 텍스트
    /// </summary>
    string? CallSign { get; set; }

    /// <summary>
    /// 국가 코드 (ISO 3166-1 alpha-3)
    /// 예: "KOR", "USA", "CHN"
    /// 심볼 우측에 표시되는 텍스트
    /// </summary>
    string? CountryCode { get; set; }

    #endregion

}