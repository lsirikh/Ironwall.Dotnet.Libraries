using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
public interface IGeometricSymbolModel : ISymbolModel
{
    double Opacity { get; set; }
    EnumShapeType ShapeType { get; set; }
}