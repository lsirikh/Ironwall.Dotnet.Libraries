using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

/// <summary>
/// 마이크 방송 시작/중지 Body — <c>BROADCAST_MIC_START</c> / <c>BROADCAST_MIC_STOP</c>
/// (PRD symbol-detail-and-door-control FR-23, 서버 규격 확정 대기 중).
///
/// <para><b>마이크는 방송서버에 물려 있고 GIS 가 지정하는 것은 대상 스피커뿐</b>이다(사용자 확정 2026-09-08).
/// 그래서 <c>BROADCAST_PLAY</c>·<c>BROADCAST_STOP</c> 과 <b>같은 모양</b>(speaker_ids)이며,
/// 입력 장치·오디오 파라미터는 들어가지 않는다.</para>
/// </summary>
public class BroadcastMicBodyDto
{
    [JsonProperty("speaker_ids")]
    public List<int> SpeakerIds { get; set; } = new();
}
