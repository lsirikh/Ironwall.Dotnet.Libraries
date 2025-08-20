using Ironwall.Dotnet.Libraries.Enums;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/19/2025 9:53:53 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class GeometricSymbolModel : SymbolModel, IGeometricSymbolModel
{
    #region - Ctors -
    public GeometricSymbolModel() : base()
    {
        // 기하 심볼 기본값 설정
        Category = EnumMarkerCategory.GEOMETRICS;
        ShapeType = EnumShapeType.Circle;
        Width = 30;
        Height = 30;
    }
    public GeometricSymbolModel(string title, double latitude, double longitude,
        EnumShapeType shapeType = EnumShapeType.Circle)
        : base(title, latitude, longitude)
    {
        Category = EnumMarkerCategory.GEOMETRICS;
        ShapeType = shapeType;
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    /// <summary>
    /// 기하학적 모양 타입
    /// </summary>
    public EnumShapeType ShapeType { get; set; }
    /// <summary>
    /// 투명도 (0.0 ~ 1.0)
    /// </summary>
    public double Opacity { get; set; } = 1.0;
    #endregion
    #region - Attributes -
    #endregion
}