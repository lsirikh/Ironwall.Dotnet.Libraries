using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;
using System;
using System.Net.NetworkInformation;

namespace Ironwall.Dotnet.Monitoring.Models.Maps;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/24/2025 8:35:20 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 커스텀 지도 모델 (순수 데이터, 메서드 없음)
/// </summary>
public class CustomMapModel : MapModel, ICustomMapModel
{
    public CustomMapModel()
    {
        GeoReferenceMethod = EnumGeoReference.Automatic;
        ResolutionUnit = "degrees";
        ControlPoints = new List<IGeoControlPointModel>();
        Status = EnumMapStatus.Processing;
    }

    public CustomMapModel(ICustomMapModel model) : base(model)
    {
        SourceImagePath = model.SourceImagePath;
        TilesDirectoryPath = model.TilesDirectoryPath;
        OriginalWidth = model.OriginalWidth;
        OriginalHeight = model.OriginalHeight;
        OriginalFileSize = model.OriginalFileSize;
        TotalTileCount = model.TotalTileCount;
        TilesDirectorySize = model.TilesDirectorySize;
        PixelResolutionX = model.PixelResolutionX;
        PixelResolutionY = model.PixelResolutionY;
        ResolutionUnit = model.ResolutionUnit;
        GeoReferenceMethod = model.GeoReferenceMethod;
        GeoTransformMatrix = model.GeoTransformMatrix;
        ControlPointCount = model.ControlPointCount;
        ProcessedAt = model.ProcessedAt;
        ProcessingTimeMinutes = model.ProcessingTimeMinutes;
        QualityScore = model.QualityScore;
        ControlPoints = model.ControlPoints?.ToList() ?? new List<IGeoControlPointModel>();
    }

    public override EnumMapProvider ProviderType => EnumMapProvider.Custom;

    #region - Custom Map Properties (순수 데이터) -
    [JsonProperty("source_image_path", Order = 20)]
    public string SourceImagePath { get; set; } = string.Empty;

    [JsonProperty("tiles_directory_path", Order = 21)]
    public string TilesDirectoryPath { get; set; } = string.Empty;

    [JsonProperty("original_width", Order = 22)]
    public int OriginalWidth { get; set; }

    [JsonProperty("original_height", Order = 23)]
    public int OriginalHeight { get; set; }

    [JsonProperty("original_file_size", Order = 24)]
    public long OriginalFileSize { get; set; }

    [JsonProperty("total_tile_count", Order = 25)]
    public int TotalTileCount { get; set; }

    [JsonProperty("tiles_directory_size", Order = 26)]
    public long TilesDirectorySize { get; set; }

    [JsonProperty("pixel_resolution_x", Order = 27)]
    public double? PixelResolutionX { get; set; }

    [JsonProperty("pixel_resolution_y", Order = 28)]
    public double? PixelResolutionY { get; set; }

    [JsonProperty("resolution_unit", Order = 29)]
    public string? ResolutionUnit { get; set; }

    [JsonProperty("geo_reference_method", Order = 30)]
    public EnumGeoReference GeoReferenceMethod { get; set; }

    [JsonProperty("geo_transform_matrix", Order = 31)]
    public string? GeoTransformMatrix { get; set; }

    [JsonProperty("control_point_count", Order = 32)]
    public int? ControlPointCount { get; set; }

    [JsonProperty("processed_at", Order = 33)]
    public DateTime? ProcessedAt { get; set; }

    [JsonProperty("processing_time_minutes", Order = 34)]
    public int? ProcessingTimeMinutes { get; set; }

    [JsonProperty("quality_score", Order = 35)]
    public double? QualityScore { get; set; }

    [JsonProperty("control_points", Order = 36)]
    public List<IGeoControlPointModel> ControlPoints { get; set; }
    #endregion

    // 메서드 없음! 오직 데이터만!
}