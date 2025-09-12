using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
public interface ILineSymbolModel : ISymbolModel
{
    bool IsClosedPath { get; set; }
    double LineOpacity { get; set; }
    EnumLinePattern LinePattern { get; set; }
    List<GeoPoint> LinePoints { get; set; }
    bool ShowArrowHead { get; set; }
    bool ShowEndPoint { get; set; }
    bool ShowStartPoint { get; set; }
}