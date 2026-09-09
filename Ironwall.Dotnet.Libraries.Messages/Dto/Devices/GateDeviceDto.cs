using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// Gate(통문) 디바이스 DTO — 서버 `GateResponse`(api-test-server `app/schemas/device.py`) 대응.
/// <para>필드는 2026-09-08 로컬 서버 실측으로 확정했다(<c>GET /api/devices/gates</c>):
/// 공통 <see cref="BaseDeviceDto"/> + <see cref="GateStatus"/> · <see cref="Urls"/> · <see cref="LinkInfo"/>.</para>
/// <para><b>주의</b>: <see cref="GateStatus"/> 는 <b>명령으로 바뀌지 않는다</b>. 서버는 개폐 명령을 받아
/// NATS 로 전파만 하고, 실제 상태는 담당 매니저가 <c>PATCH /{id}/status</c> 로 되돌려 보고할 때 바뀐다
/// (operation-event PRD v1.5 FR-15). 낙관적으로 이 값을 갱신하면 구동 실패 시 화면이 거짓말을 한다.</para>
/// </summary>
public class GateDeviceDto : BaseDeviceDto
{
    public GateDeviceDto()
    {
        TypeDevice = "Gate";
    }

    /// <summary>통문 개폐 상태 (EnumGateStatus: CLOSED, OPEN).</summary>
    [JsonProperty("gate_status", Order = 12)]
    public string GateStatus { get; set; } = "CLOSED";

    /// <summary>이미지·통합관리 링크 (JSONB) — 예 <c>{ "image": "...", "management": "..." }</c>.</summary>
    [JsonProperty("urls", Order = 13, NullValueHandling = NullValueHandling.Ignore)]
    public JObject? Urls { get; set; }

    /// <summary>
    /// 결선 방식 정보 (JSONB) — 예 <c>{ "type": "ENCLOSURE_CONTACT", "channel": 3, "parent_hint": 1351 }</c>.
    /// 서버가 개폐 명령의 구동 주체를 특정하지 못하는 이유이자(제어기 접점 / 함체 접점 / IP 컨버터),
    /// 명령을 받은 매니저가 "자기 몫인지" 판단하는 근거다.
    /// </summary>
    [JsonProperty("link_info", Order = 14, NullValueHandling = NullValueHandling.Ignore)]
    public JObject? LinkInfo { get; set; }
}
