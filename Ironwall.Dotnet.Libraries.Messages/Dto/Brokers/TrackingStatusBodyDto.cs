using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
/****************************************************************************
   Purpose      : TRACKING_STATUS body DTO (Gop_Message_Broker §8.3.7)
   Created By   : GHLee
   Created On   : 2026-06-25
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// AiAnalysis가 발행하는 TRACKING_STATUS body.
/// <para><b>전면 교체(하위호환 없음)</b>: 단일 <c>target</c>/<c>target_location</c> → 다중 <c>targets[]</c> 배열.
/// 신규 필드 <c>ttl_sec</c>(소멸 안전장치)·<c>frame_width</c>/<c>frame_height</c>(bbox 해석 기준) 추가.</para>
/// </summary>
public class TrackingStatusBodyDto
{
    /// <summary>카메라 ID. (camera_id + targets[].track_id 복합키로 마커 식별)</summary>
    [JsonProperty("camera_id")]
    public int CameraId { get; set; }

    /// <summary>추적 상태 (소문자 active/lost/idle). lost·idle 시 해당 camera_id 마커 제거(페이드).</summary>
    [JsonProperty("tracking")]
    public string Tracking { get; set; } = string.Empty;

    /// <summary>소멸 안전장치(초) — 갱신 없으면 만료 제거. 미지정 시 설정 폴백(기본 5).</summary>
    [JsonProperty("ttl_sec")]
    public int? TtlSec { get; set; }

    /// <summary>AI 추론 프레임 가로 해상도(px) — bbox 해석 기준. v1 미사용.</summary>
    [JsonProperty("frame_width")]
    public int? FrameWidth { get; set; }

    /// <summary>AI 추론 프레임 세로 해상도(px) — bbox 해석 기준. v1 미사용.</summary>
    [JsonProperty("frame_height")]
    public int? FrameHeight { get; set; }

    /// <summary>탐지/추적 타겟 배열. active면 1개 이상, lost/idle이면 빈 배열 가능.</summary>
    [JsonProperty("targets")]
    public List<TrackingTargetDto>? Targets { get; set; }
}
