using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media.Media3D;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/5/2025 6:37:17 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class MilitarySymbolModel : SymbolModel, IMilitarySymbolModel
{
    #region - Ctors -
    public MilitarySymbolModel() : base()
    {
        // 기본값 설정
        Category = EnumMarkerCategory.MILITARY_SYMBOLS;
        Affiliation = EnumMilitaryAffiliation.Friend;
        BattleDimension = EnumMilitaryBattleDimension.Land;
        StandardIdentity = EnumMilitaryStandardIdentity.Present;
        UnitType = EnumMilitaryUnitType.Infantry;
        UnitSize = EnumMilitaryUnitSize.Company;

        // 군사 심볼 기본 설정
        Width = 60;
        Height = 60;
        ShowShape = true;
        ShowTitle = false;
        StrokeColor = EnumColorType.Black;
        FillColor = EnumColorType.Transparent;
        TitleSize = 10;
        OperationState = EnumOperationState.NONE;
    }
    #endregion

    #region - Core Military Symbol Properties (NATO APP-6D Required) -

    public EnumMilitaryAffiliation Affiliation { get; set; }
    public EnumMilitaryBattleDimension BattleDimension { get; set; }
    public EnumMilitaryStandardIdentity StandardIdentity { get; set; }
    public EnumMilitaryUnitType UnitType { get; set; }
    public EnumMilitaryUnitSize UnitSize { get; set; }

    #endregion

    #region - Unit Identification Properties (Optional Display Text) -

    public string? UnitDesignator { get; set; }
    public string? HigherFormation { get; set; }
    public string? CallSign { get; set; }
    public string? CountryCode { get; set; }

    #endregion
}