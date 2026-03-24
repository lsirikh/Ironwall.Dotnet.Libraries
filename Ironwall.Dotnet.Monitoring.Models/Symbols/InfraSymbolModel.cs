using Ironwall.Dotnet.Libraries.Enums;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/19/2025 7:36:01 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class InfraSymbolModel : SymbolModel, IInfraSymbolModel
{
    public InfraSymbolModel()
    {
        Width = 60;
        Height = 60;
        ShowShape = true;
        ShowTitle = false;
        StrokeColor = EnumColorType.Black;
        FillColor = EnumColorType.Transparent;
        TitleSize = 10;
        Category = EnumMarkerCategory.INFRASTRUCTURE;
        OperationState = EnumOperationState.NONE;
    }

    #region - 건물 기본 정보 -

    /// <summary>
    /// 건물 종류
    /// </summary>
    public EnumBuildingType BuildingType { get; set; } = EnumBuildingType.Factory;

    /// <summary>
    /// 건물 용도
    /// </summary>
    public EnumBuildingUsage BuildingUsage { get; set; } = EnumBuildingUsage.Office;

    /// <summary>
    /// 층수
    /// </summary>
    public int FloorCount { get; set; }

    /// <summary>
    /// 지하층수
    /// </summary>
    public int BasementFloorCount { get; set; }

    /// <summary>
    /// 건물 면적 (㎡)
    /// </summary>
    public double BuildingArea { get; set; }

    #endregion
}