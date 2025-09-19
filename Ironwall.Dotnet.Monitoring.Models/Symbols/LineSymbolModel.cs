
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/12/2025 10:30:55 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class LineSymbolModel : SymbolModel, ILineSymbolModel
{
    public LineSymbolModel()
    {

        Width = 60;
        Height = 60;
        ShowShape = true;
        ShowTitle = false;
        StrokeColor = EnumColorType.Black;
        FillColor = EnumColorType.Transparent;
        Category = EnumMarkerCategory.AREA_BOUNDARY;
        TitleSize = 10;
        OperationState = EnumOperationState.NONE;
    }

    /// <summary>
    /// 라인을 구성하는 포인트들 (JSON 직렬화 가능)
    /// </summary>
    public List<GeoPoint> LinePoints { get; set; } = new List<GeoPoint>();

    /// <summary>
    /// 라인 투명도 (0.0 ~ 1.0)
    /// </summary>
    public double LineOpacity { get; set; } = 1.0;

    /// <summary>
    /// 닫힌 경로 여부 (시작점과 끝점 연결)
    /// </summary>
    public bool IsClosedPath { get; set; } = false;

    /// <summary>
    /// 화살표 머리 표시 여부
    /// </summary>
    public bool ShowArrowHead { get; set; } = false;

    /// <summary>
    /// 라인 패턴 타입 (실선, 점선, 이중선 등)
    /// </summary>
    public EnumLinePattern LinePattern { get; set; } = EnumLinePattern.Solid;
}