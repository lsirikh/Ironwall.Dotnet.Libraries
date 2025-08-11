using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
public interface ISymbolModel : IBaseModel
{
    float Altitude { get; set; }
    double Bearing { get; set; }
    EnumMarkerCategory Category { get; set; }
    double Height { get; set; }
    double Latitude { get; set; }
    double Longitude { get; set; }
    EnumOperationState OperationState { get; set; }
    int Pid { get; set; }
    float Pitch { get; set; }
    float Roll { get; set; }
    string Title { get; set; }
    bool Visibility { get; set; }
    double Width { get; set; }
}