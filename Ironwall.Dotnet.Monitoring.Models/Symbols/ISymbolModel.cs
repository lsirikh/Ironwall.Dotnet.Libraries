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
    EnumColorType FillColor { get; set; }
    EnumColorType StrokeColor { get; set; }
    double StrokeThickness { get; set; }
    EnumMarkerCategory Category { get; set; }
    EnumOperationState OperationState { get; set; }
    int ZIndex { get; set; }

}