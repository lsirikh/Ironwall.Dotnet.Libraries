namespace Ironwall.Dotnet.Libraries.GMaps.Models;

public interface IGMapSetupModel
{
    HomePositionModel? HomePosition { get; set; }
    string? MapMode { get; set; }
    string? MapName { get; set; }
    string? MapType { get; set; }
    string? TileDirectory { get; set; }
}