using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

/****************************************************************************
   Purpose      : 통문·함체 문 개폐 명령 body — NATS GATE_DOOR_SET / ENCLOSURE_DOOR_SET.
                  브로커 연동설계 v1.6 §7 "제어 명령 — 함체·통문 문 개폐(all.*)".
                  Subject: sensorway.{부대ID}.all.gate-door / .all.enclosure-door
                  발신 from = "GIS", m_type = "PUB".
   Created By   : GHLee
   Created On   : 2026-09-18
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 문 개폐 <b>명령</b> body. <b>이 명령은 상태를 바꾸지 않는다</b> —
/// 구동 담당 매니저가 <c>PATCH /api/devices/{gates|enclosures}/{id}/component-status</c> 로
/// 문 위치 부품 상태를 되돌릴 때 DBApi 가 <c>OPERATION_EVENT</c> 를 내고, 화면은 그것으로만 전이한다.
/// <para>서버 REST <c>POST …/{id}/control</c> 은 제거됐다(운영 404 / 개발 410 ENDPOINT_REMOVED).</para>
/// </summary>
public class DoorSetBodyDto
{
    /// <summary>열림 명령 값(서버 문 명령 어휘).</summary>
    public const string Open = "OPEN";

    /// <summary>닫힘 명령 값(서버 문 명령 어휘).</summary>
    public const string Close = "CLOSE";

    /// <summary>대상 장비 ID (함체 또는 통문). 필수.</summary>
    [JsonProperty("device_id")]
    public int DeviceId { get; set; }

    /// <summary><see cref="Open"/> / <see cref="Close"/>. 필수.</summary>
    [JsonProperty("command")]
    public string Command { get; set; } = Close;

    /// <summary>
    /// 사람이 읽는 식별 흔적 — <c>[&lt;category_device&gt;:&lt;종류값&gt;] &lt;name_device&gt; (number: N, id: ID)</c>.
    /// 종류를 모르면 <c>[gate]</c> 처럼 카테고리만 온다(명세 허용). <b>수신측 파싱 대상이 아니다.</b> 필수.
    /// </summary>
    [JsonProperty("device_description")]
    public string DeviceDescription { get; set; } = string.Empty;

    /// <summary>명령을 낸 운영자 계정 — 발신 클라가 채운다. 선택.</summary>
    [JsonProperty("requested_by", NullValueHandling = NullValueHandling.Ignore)]
    public string? RequestedBy { get; set; }

    /// <summary>ISO 8601 — <b>오프셋 필수</b>(서버 datetime-aware 규약). 필수.</summary>
    [JsonProperty("requested_at")]
    public string RequestedAt { get; set; } = string.Empty;
}
