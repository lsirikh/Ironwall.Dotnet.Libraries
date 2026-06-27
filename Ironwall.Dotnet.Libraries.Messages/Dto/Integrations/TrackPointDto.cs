using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;

/// <summary>
/// 추적점(track point) DTO — 서버 <c>GET /api/tracking/points</c> 응답 1행.
/// <para>⚠ <see cref="ObservedAt"/>는 반드시 <c>string</c>: 서버가 KST <c>+09:00</c> 문자열로 반환하는데
/// ApiMessageHelper.JsonSettings의 <c>DateParseHandling.None</c> 때문에 DateTime이면 offset이 유실된다.
/// 매퍼(TrackPointDtoMapper)가 UTC <see cref="System.DateTime"/>으로 변환한다.</para>
/// </summary>
public class TrackPointDto : BaseDto
{
    /// <summary>카메라 ID</summary>
    [JsonProperty("camera_id", Order = 2)]
    public int CameraId { get; set; }

    /// <summary>타겟 고유 식별자</summary>
    [JsonProperty("track_id", Order = 3)]
    public string TrackId { get; set; } = string.Empty;

    /// <summary>타겟 분류 (person/car/animal 등)</summary>
    [JsonProperty("label", Order = 4)]
    public string? Label { get; set; }

    /// <summary>위협 등급 (NORMAL/CAUTION/THREAT)</summary>
    [JsonProperty("threat_level", Order = 5)]
    public string? ThreatLevel { get; set; }

    /// <summary>위도</summary>
    [JsonProperty("latitude", Order = 6)]
    public double Latitude { get; set; }

    /// <summary>경도</summary>
    [JsonProperty("longitude", Order = 7)]
    public double Longitude { get; set; }

    /// <summary>카메라로부터 거리(m)</summary>
    [JsonProperty("distance_m", Order = 8)]
    public double? DistanceM { get; set; }

    /// <summary>탐지 신뢰도(0~1)</summary>
    [JsonProperty("confidence", Order = 9)]
    public double? Confidence { get; set; }

    /// <summary>관측 시각 — 서버 KST <c>+09:00</c> 문자열(예: "2026-06-26T19:30:00+09:00"). 매퍼가 UTC로 변환.</summary>
    [JsonProperty("observed_at", Order = 10)]
    public string? ObservedAt { get; set; }

    /// <summary>추적 상태(active 고정)</summary>
    [JsonProperty("tracking_state", Order = 11)]
    public string? TrackingState { get; set; }

    /// <summary>속도(m/s) — 서버 계산 또는 null(클라 재계산)</summary>
    [JsonProperty("speed_mps", Order = 12)]
    public double? SpeedMps { get; set; }

    /// <summary>세션 시퀀스(선택)</summary>
    [JsonProperty("session_seq", Order = 13)]
    public int? SessionSeq { get; set; }
}
