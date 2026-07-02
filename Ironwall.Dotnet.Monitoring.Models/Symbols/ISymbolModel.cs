using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
public interface ISymbolModel : IBaseModel
{
    int Pid { get; set; }
    string Title { get; set; }
    double TitleSize { get; set; }
    double Height { get; set; }
    double Width { get; set; }
    double Latitude { get; set; }
    double Longitude { get; set; }
    double Zoom { get; set; }
    float Altitude { get; set; }
    double Bearing { get; set; }
    bool ShowShape { get; set; }
    bool ShowTitle { get; set; }
    bool IsLocked { get; set; }
    EnumColorType FillColor { get; set; }
    EnumColorType StrokeColor { get; set; }
    double StrokeThickness { get; set; }
    EnumMarkerCategory Category { get; set; }
    EnumOperationState OperationState { get; set; }
    int ZOrder { get; set; }

    /// <summary>라벨(제목) 상대 오프셋 X(화면 픽셀, 아이콘 중심 기준). 0=기본위치. 라벨 분리·드래그 영속(Symbol_Label_Decouple FR-LB-01).</summary>
    double LabelOffsetX { get; set; }
    /// <summary>라벨(제목) 상대 오프셋 Y(화면 픽셀, 아이콘 중심 기준). 0=기본위치.</summary>
    double LabelOffsetY { get; set; }
}