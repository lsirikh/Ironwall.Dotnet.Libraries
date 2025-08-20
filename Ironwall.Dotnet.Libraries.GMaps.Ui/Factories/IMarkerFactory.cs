using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
public interface IMarkerFactory
{
    IEditableMarker CreateMarker(ISymbolModel symbolModel);
}