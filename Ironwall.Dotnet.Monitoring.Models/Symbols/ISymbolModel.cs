using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
public interface ISymbolModel : IBaseModel
{
    float Altitude { get; set; }
    double Bearing { get; set; }
    EnumMarkerCategory Category { get; set; }
    EnumOperationState OperationState { get; set; }
    double Height { get; set; }
    double Width { get; set; }
    double Latitude { get; set; }
    double Longitude { get; set; }
    int Pid { get; set; }
    string Title { get; set; }
    bool ShowShape { get; set; }
    bool ShowTitle { get; set; }
}