using Ironwall.Dotnet.Libraries.Base.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Monitoring.Models.Maps;
/// <summary>
/// 지리참조 기준점 모델 (순수 데이터, 메서드 없음)
/// </summary>
public class GeoControlPointModel : BaseModel, IGeoControlPointModel
{
    public GeoControlPointModel() { }

    public GeoControlPointModel(IGeoControlPointModel model) : base(model)
    {
        CustomMapId = model.CustomMapId;
        PixelX = model.PixelX;
        PixelY = model.PixelY;
        Latitude = model.Latitude;
        Longitude = model.Longitude;
        AccuracyMeters = model.AccuracyMeters;
        Description = model.Description;
    }

    [JsonProperty("custom_map_id", Order = 2)]
    public int CustomMapId { get; set; }

    [JsonProperty("pixel_x", Order = 3)]
    public double PixelX { get; set; }

    [JsonProperty("pixel_y", Order = 4)]
    public double PixelY { get; set; }

    [JsonProperty("latitude", Order = 5)]
    public double Latitude { get; set; }

    [JsonProperty("longitude", Order = 6)]
    public double Longitude { get; set; }

    [JsonProperty("accuracy_meters", Order = 7)]
    public double? AccuracyMeters { get; set; }

    [JsonProperty("description", Order = 8)]
    public string? Description { get; set; }

    // 메서드 없음! 오직 데이터만!
}
