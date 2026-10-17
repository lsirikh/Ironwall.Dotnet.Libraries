using Ironwall.Dotnet.Libraries.Base.Models;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Monitoring.Models.Maps;

/// <summary>
/// 카메라별 RTSP 팝업 위치(Geo 앵커) 영속 모델. <see cref="MapRoiModel"/> 패턴 답습.
/// 크기(Width/Height)는 영속하지 않음(재오픈 시 기본 크기).
/// </summary>
public class CameraPopupPositionModel : BaseModel, ICameraPopupPositionModel
{
    public CameraPopupPositionModel() { }

    public CameraPopupPositionModel(ICameraPopupPositionModel model) : base(model)
    {
        CameraId = model.CameraId;
        Latitude = model.Latitude;
        Longitude = model.Longitude;
        UpdatedAt = model.UpdatedAt;
    }

    public CameraPopupPositionModel(int cameraId, double latitude, double longitude)
    {
        CameraId = cameraId;
        Latitude = latitude;
        Longitude = longitude;
    }

    [JsonProperty("camera_id", Order = 2)]
    public int CameraId { get; set; }

    [JsonProperty("latitude", Order = 3)]
    public double Latitude { get; set; }

    [JsonProperty("longitude", Order = 4)]
    public double Longitude { get; set; }

    [JsonProperty("updated_at", Order = 5)]
    public DateTime? UpdatedAt { get; set; }
}
