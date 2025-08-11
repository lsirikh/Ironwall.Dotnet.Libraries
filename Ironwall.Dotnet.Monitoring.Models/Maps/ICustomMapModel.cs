using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Monitoring.Models.Maps;
/// <summary>
/// 커스텀 지도 인터페이스 (순수 데이터)
/// </summary>
public interface ICustomMapModel : IMapModel
{
    string SourceImagePath { get; set; }
    string TilesDirectoryPath { get; set; }
    int OriginalWidth { get; set; }
    int OriginalHeight { get; set; }
    long OriginalFileSize { get; set; }
    int TotalTileCount { get; set; }
    long TilesDirectorySize { get; set; }
    double? PixelResolutionX { get; set; }
    double? PixelResolutionY { get; set; }
    string? ResolutionUnit { get; set; }
    EnumGeoReference GeoReferenceMethod { get; set; }
    string? GeoTransformMatrix { get; set; }
    int? ControlPointCount { get; set; }
    DateTime? ProcessedAt { get; set; }
    int? ProcessingTimeMinutes { get; set; }
    double? QualityScore { get; set; }
    List<IGeoControlPointModel> ControlPoints { get; set; }
}