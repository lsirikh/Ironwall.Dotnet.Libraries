using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

/****************************************************************************
   Purpose      : 제어 명령(클라 → 매니저) body 의 공통 선택 칸 — 발신자 · 단말.
                  브로커 연동설계 v2.0.12 §7 머리 "제어 명령 공통 선택 칸".
                  방송 · TTS · 경광등 · PTZ · §8.3 카메라 설정 명령 body 가 상속한다.
                  (문 개폐 · PTZ_AIM_LOCATION 은 requested_by 를 이미 가진 DTO 라 같은 칸을 직접 둔다.)
   Created By   : Claude
   Created On   : 2026-10-15
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 제어 명령 공통 선택 칸. 두 칸 모두 <b>선택</b>이라 값이 없으면 싣지 않는다(null 은 직렬화에서 뺀다).
/// 수행자는 판정에 쓰지 않고 기록 · 추적용으로만 쓴다.
/// </summary>
public abstract class ControlCommandBodyDto
{
    /// <summary>명령을 낸 운영자 계정(login_id) — 발신 클라가 채운다. 선택.</summary>
    [JsonProperty("requested_by", NullValueHandling = NullValueHandling.Ignore)]
    public string? RequestedBy { get; set; }

    /// <summary>
    /// 명령을 낸 단말 — 통제DB 세션 <c>client_id</c>(로그인 헤더 <c>X-Client-Id</c> 와 같은 값, 설치마다 다르다).
    /// SSO(OIDC) <c>client_id</c> 는 쓰지 않는다. 선택.
    /// </summary>
    [JsonProperty("requested_client_id", NullValueHandling = NullValueHandling.Ignore)]
    public string? RequestedClientId { get; set; }
}
