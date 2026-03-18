using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Monitoring.Models.Maps;

public interface IMapLayerModel : IBaseModel
{
    string Name { get; set; }
    string LayerType { get; set; }       // "OverlayMap", "OverlayImage", "Symbol"
    string? Category { get; set; }       // "PidsCamera", "Military" etc.
    bool IsVisible { get; set; }
    double Opacity { get; set; }
    int ZOrder { get; set; }
    int? MapId { get; set; }
    string? FilePath { get; set; }
    DateTime? CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}
