using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Monitoring.Models.Maps;

/// <summary>
/// 지도 기본 인터페이스 (순수 데이터)
/// </summary>
public interface IMapModel : IBaseModel
{
    string Name { get; set; }
    string? Description { get; set; }
    EnumMapProvider ProviderType { get; }
    EnumMapCategory Category { get; set; }
    EnumMapData DataType { get; set; }
    string? CoordinateSystem { get; set; }
    string? EpsgCode { get; set; }
    double? MinLatitude { get; set; }
    double? MaxLatitude { get; set; }
    double? MinLongitude { get; set; }
    double? MaxLongitude { get; set; }
    int MinZoomLevel { get; set; }
    int MaxZoomLevel { get; set; }
    int TileSize { get; set; }
    EnumMapStatus Status { get; set; }
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
    string? CreatedBy { get; set; }
}