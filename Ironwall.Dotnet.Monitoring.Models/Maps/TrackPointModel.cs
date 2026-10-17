using Ironwall.Dotnet.Libraries.Base.Models;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Monitoring.Models.Maps;

/// <summary>
/// 추적 좌표 로컬 DB 모델(CameraTrackPoints). <see cref="PtzPresetModel"/> 패턴.
/// 영속 = 우선 로컬 자체 DB(2026-06-25 결정). Playback이 시간범위로 조회.
/// </summary>
public class TrackPointModel : BaseModel, ITrackPointModel
{
    public TrackPointModel() { }

    public TrackPointModel(ITrackPointModel model) : base(model)
    {
        CameraId = model.CameraId;
        TrackId = model.TrackId;
        Label = model.Label;
        ThreatLevel = model.ThreatLevel;
        Latitude = model.Latitude;
        Longitude = model.Longitude;
        DistanceM = model.DistanceM;
        SpeedMps = model.SpeedMps;
        ObservedAt = model.ObservedAt;
    }

    [JsonProperty("camera_id", Order = 2)] public int CameraId { get; set; }
    [JsonProperty("track_id", Order = 3)] public string TrackId { get; set; } = string.Empty;
    [JsonProperty("label", Order = 4)] public string? Label { get; set; }
    [JsonProperty("threat_level", Order = 5)] public string? ThreatLevel { get; set; }
    [JsonProperty("latitude", Order = 6)] public double Latitude { get; set; }
    [JsonProperty("longitude", Order = 7)] public double Longitude { get; set; }
    [JsonProperty("distance_m", Order = 8)] public double? DistanceM { get; set; }
    [JsonProperty("speed_mps", Order = 9)] public double? SpeedMps { get; set; }
    [JsonProperty("observed_at", Order = 10)] public DateTime ObservedAt { get; set; }
}
