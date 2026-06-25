using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Monitoring.Models.Maps;

/// <summary>
/// 추적 좌표 1건(로컬 DB <c>CameraTrackPoints</c> 행). TRACKING_STATUS targets[] 수신분을 영속.
/// <see cref="IPtzPresetModel"/> 패턴. Id(BaseModel)=AUTO_INCREMENT PK.
/// </summary>
public interface ITrackPointModel : IBaseModel
{
    /// <summary>카메라 ID (복합키 절반).</summary>
    int CameraId { get; set; }
    /// <summary>타겟 식별자 (복합키 절반).</summary>
    string TrackId { get; set; }
    /// <summary>타겟 분류 (person/car/animal …, 원형 문자열).</summary>
    string? Label { get; set; }
    /// <summary>위협 등급 원형 문자열 (NORMAL/CAUTION/THREAT).</summary>
    string? ThreatLevel { get; set; }
    double Latitude { get; set; }
    double Longitude { get; set; }
    /// <summary>카메라 거리(m).</summary>
    double? DistanceM { get; set; }
    /// <summary>속도(m/s, 클라 계산값 저장).</summary>
    double? SpeedMps { get; set; }
    /// <summary>객체 관측 시각(UTC). Playback 정렬·범위 조회 기준.</summary>
    DateTime ObservedAt { get; set; }
}
