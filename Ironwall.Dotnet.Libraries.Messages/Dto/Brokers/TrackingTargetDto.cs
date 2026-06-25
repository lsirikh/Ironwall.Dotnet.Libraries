using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
/****************************************************************************
   Purpose      : TRACKING_STATUS body.targets[] 항목 DTO (Gop_Message_Broker §8.3.7)
   Created By   : GHLee
   Created On   : 2026-06-25
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 추적 타겟 1건. AiAnalysis가 프레임마다 산출하는 객체 단위 정보.
/// <para>enum 필드(<see cref="ThreatLevel"/>)는 와이어 원형(string)으로 보관하고,
/// enum 변환은 오버레이 매니저 층에서 안전 파싱(<c>TrackingEnumExtensions</c>)으로 처리한다.</para>
/// </summary>
public class TrackingTargetDto
{
    /// <summary>타겟 고유 식별자 — 프레임 간 동일 객체 추적/마커 갱신 키. (camera_id + track_id 복합키)</summary>
    [JsonProperty("track_id")]
    public string TrackId { get; set; } = string.Empty;

    /// <summary>타겟 분류 (person, car, vehicle, animal 등).</summary>
    [JsonProperty("label")]
    public string Label { get; set; } = string.Empty;

    /// <summary>위협 등급 원형 문자열 (NORMAL/CAUTION/THREAT). enum 변환은 매니저 층.</summary>
    [JsonProperty("threat_level")]
    public string? ThreatLevel { get; set; }

    /// <summary>탐지 신뢰도 (0.0–1.0).</summary>
    [JsonProperty("confidence")]
    public double? Confidence { get; set; }

    /// <summary>객체 단위 관측 시각 (ISO 8601 UTC ms). 순서 역전 방지 비교 기준.</summary>
    [JsonProperty("observed_at")]
    public string? ObservedAt { get; set; }

    /// <summary>타겟 추정 GPS 좌표 (WGS84) + 카메라 거리.</summary>
    [JsonProperty("location")]
    public TrackingTargetLocationDto? Location { get; set; }

    /// <summary>바운딩 박스 [x, y, w, h] (px, frame_width/height 기준). v1 미사용.</summary>
    [JsonProperty("bbox")]
    public List<int>? Bbox { get; set; }

    /// <summary>타겟 캡처 이미지 URL. v1 미사용.</summary>
    [JsonProperty("thumbnail")]
    public string? Thumbnail { get; set; }
}
