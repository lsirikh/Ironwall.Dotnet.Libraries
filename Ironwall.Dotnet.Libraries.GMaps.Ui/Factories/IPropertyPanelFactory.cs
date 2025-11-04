using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
public interface IPropertyPanelFactory
{
    GMapPropertyBaseControl CreatePropertyPanel(IEditableMarker marker);
}