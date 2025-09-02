using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

public interface IGeoEditableMarker : IEditableMarker
{
    EnumShapeType ShapeType { get; set; }
    double Opacity { get; set; }
}