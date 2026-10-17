using Ironwall.Dotnet.Libraries.Base.Models;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Monitoring.Models.Maps;

/// <summary>
/// 카메라 PTZ 프리셋 로컬 DB 모델(CameraPopup_PTZ_Control). <see cref="CameraPopupPositionModel"/> 패턴 답습.
/// Id(BaseModel) = DB AUTO_INCREMENT PK. Zoom은 double로 일관(int 절삭 금지, NFR-DATA-01).
/// </summary>
public class PtzPresetModel : BaseModel, IPtzPresetModel
{
    public PtzPresetModel() { }

    public PtzPresetModel(IPtzPresetModel model) : base(model)
    {
        CameraId = model.CameraId;
        PresetName = model.PresetName;
        IsHome = model.IsHome;
        Pan = model.Pan;
        Tilt = model.Tilt;
        Zoom = model.Zoom;
        PanTiltSpace = model.PanTiltSpace;
        ZoomSpace = model.ZoomSpace;
        UpdatedAt = model.UpdatedAt;
    }

    [JsonProperty("camera_id", Order = 2)] public int CameraId { get; set; }
    [JsonProperty("preset_name", Order = 3)] public string PresetName { get; set; } = string.Empty;
    [JsonProperty("is_home", Order = 4)] public bool IsHome { get; set; }
    [JsonProperty("pan", Order = 5)] public double Pan { get; set; }
    [JsonProperty("tilt", Order = 6)] public double Tilt { get; set; }
    [JsonProperty("zoom", Order = 7)] public double Zoom { get; set; }
    [JsonProperty("pan_tilt_space", Order = 8)] public string? PanTiltSpace { get; set; }
    [JsonProperty("zoom_space", Order = 9)] public string? ZoomSpace { get; set; }
    [JsonProperty("updated_at", Order = 10)] public DateTime? UpdatedAt { get; set; }
}
