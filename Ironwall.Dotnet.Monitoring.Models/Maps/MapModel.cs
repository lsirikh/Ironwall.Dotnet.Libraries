using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;
using System;
using System.Net.NetworkInformation;

namespace Ironwall.Dotnet.Monitoring.Models.Maps;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/24/2025 5:51:38 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 지도 추상 기본 클래스 (순수 데이터, 메서드 없음)
/// </summary>
public abstract class MapModel : BaseModel, IMapModel
{
    protected MapModel()
    {
        CreatedAt = DateTime.Now;
        Status = EnumMapStatus.Active;
        MinZoomLevel = 0;
        MaxZoomLevel = 18;
        TileSize = 256;
        DataType = EnumMapData.Raster;
        CoordinateSystem = "WGS84";
    }

    protected MapModel(IMapModel model) : base(model)
    {
        Name = model.Name;
        Description = model.Description;
        Category = model.Category;
        DataType = model.DataType;
        CoordinateSystem = model.CoordinateSystem;
        EpsgCode = model.EpsgCode;
        MinLatitude = model.MinLatitude;
        MaxLatitude = model.MaxLatitude;
        MinLongitude = model.MinLongitude;
        MaxLongitude = model.MaxLongitude;
        MinZoomLevel = model.MinZoomLevel;
        MaxZoomLevel = model.MaxZoomLevel;
        TileSize = model.TileSize;
        Status = model.Status;
        CreatedAt = model.CreatedAt;
        UpdatedAt = model.UpdatedAt;
        CreatedBy = model.CreatedBy;
    }

    [JsonProperty("name", Order = 2)]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description", Order = 3)]
    public string? Description { get; set; }

    [JsonProperty("provider_type", Order = 4)]
    public abstract EnumMapProvider ProviderType { get; }

    [JsonProperty("category", Order = 5)]
    public EnumMapCategory Category { get; set; }

    [JsonProperty("data_type", Order = 6)]
    public EnumMapData DataType { get; set; }

    [JsonProperty("coordinate_system", Order = 7)]
    public string? CoordinateSystem { get; set; }

    [JsonProperty("epsg_code", Order = 8)]
    public string? EpsgCode { get; set; }

    [JsonProperty("min_latitude", Order = 9)]
    public double? MinLatitude { get; set; }

    [JsonProperty("max_latitude", Order = 10)]
    public double? MaxLatitude { get; set; }

    [JsonProperty("min_longitude", Order = 11)]
    public double? MinLongitude { get; set; }

    [JsonProperty("max_longitude", Order = 12)]
    public double? MaxLongitude { get; set; }

    [JsonProperty("min_zoom_level", Order = 13)]
    public int MinZoomLevel { get; set; }

    [JsonProperty("max_zoom_level", Order = 14)]
    public int MaxZoomLevel { get; set; }

    [JsonProperty("tile_size", Order = 15)]
    public int TileSize { get; set; }

    [JsonProperty("status", Order = 16)]
    public EnumMapStatus Status { get; set; }

    [JsonProperty("created_at", Order = 17)]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("updated_at", Order = 18)]
    public DateTime? UpdatedAt { get; set; }

    [JsonProperty("created_by", Order = 19)]
    public string? CreatedBy { get; set; }
}