using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
public interface IInfraSymbolModel : ISymbolModel
{
    int BasementFloorCount { get; set; }
    double BuildingArea { get; set; }
    EnumBuildingType BuildingType { get; set; }
    EnumBuildingUsage BuildingUsage { get; set; }
    int FloorCount { get; set; }
}