using Ironwall.Dotnet.Libraries.Base.Models;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Monitoring.Models.Maps;
/****************************************************************************
   Purpose      : 관심지역(ROI) 모델 구현체
   Created By   : GHLee
   Created On   : 3/17/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class MapRoiModel : BaseModel, IMapRoiModel
{
    public MapRoiModel() { }

    public MapRoiModel(IMapRoiModel model) : base(model)
    {
        Title = model.Title;
        Latitude = model.Latitude;
        Longitude = model.Longitude;
        Altitude = model.Altitude;
        Zoom = model.Zoom;
        MapId = model.MapId;
        CreatedAt = model.CreatedAt;
        UpdatedAt = model.UpdatedAt;
    }

    [JsonProperty("title", Order = 2)]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("latitude", Order = 3)]
    public double Latitude { get; set; }

    [JsonProperty("longitude", Order = 4)]
    public double Longitude { get; set; }

    [JsonProperty("altitude", Order = 5)]
    public double Altitude { get; set; }

    [JsonProperty("zoom", Order = 6)]
    public int Zoom { get; set; } = 15;

    [JsonProperty("map_id", Order = 7)]
    public int MapId { get; set; }

    [JsonProperty("created_at", Order = 8)]
    public DateTime? CreatedAt { get; set; }

    [JsonProperty("updated_at", Order = 9)]
    public DateTime? UpdatedAt { get; set; }
}
