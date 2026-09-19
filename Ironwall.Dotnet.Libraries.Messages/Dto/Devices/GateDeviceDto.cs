using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// Gate(통문) 디바이스 DTO — 서버 `GateResponse`(api-test-server `app/schemas/device.py`) 대응.
/// <para>필드는 2026-09-08 로컬 서버 실측으로 확정했다(<c>GET /api/devices/gates</c>):
/// 공통 <see cref="BaseDeviceDto"/> + <see cref="GateStatus"/> · <see cref="Urls"/> · <see cref="LinkInfo"/>.</para>
/// <para><b>주의</b>: <see cref="GateStatus"/> 는 <b>명령으로 바뀌지 않는다</b>. 서버는 개폐 명령을 받아
/// NATS 로 전파만 하고, 실제 상태는 담당 매니저가 부품 상태로 되돌려 보고할 때 바뀐다
/// (operation-event PRD v1.5 FR-15). 낙관적으로 이 값을 갱신하면 구동 실패 시 화면이 거짓말을 한다.</para>
/// </summary>
public class GateDeviceDto : BaseDeviceDto
{
    public GateDeviceDto()
    {
        TypeDevice = "Gate";
    }

    /// <summary>통문 개폐 상태 (EnumGateStatus: CLOSED, OPEN, 그리고 7.0+ 는 <c>RUNNING</c>).</summary>
    /// <remarks>
    /// <para><b>7.0+ 응답에는 이 키가 없다</b> — <c>gates.gate_status</c> 스칼라 컬럼이 제거되고
    /// 문 위치가 <c>device_status.components.&lt;DOOR_ACTUATOR 부품 key&gt;.state</c> 로 옮겨졌다
    /// (명세 §4 "<s>EnumGateStatus</s>" 절 · §5 머리 이관표 · §5.12 "문 위치는 문 구동부
    /// (<c>DOOR_ACTUATOR</c>, <b>라이브 key <c>actuator</c></b>)의 상태입니다").
    /// <b>통문을 <c>door</c> 로 읽으면 영원히 <c>null</c></b> 이다 — 함체와 부품 key 가 다르다.</para>
    /// <para><c>RUNNING</c>(구동 중)은 <b>스칼라로는 표현조차 못 하던</b> 상태다. 부품 축을 읽어야
    /// 비로소 "열리는 중"을 구분할 수 있다.</para>
    /// <para><b>nullable 이고 이니셜라이저가 없다</b>(2026-09-18 정정) — 종전
    /// <c>= "CLOSED"</c> 는 서버가 키를 안 보낼 때 그 값이 <b>실제 관측값처럼 도착</b>하게 만들어
    /// 문이 열려 있어도 화면이 닫힘으로 굳는 원인이었다. 6.3.2 운영 서버에는
    /// <c>/api/devices/gates</c> 경로가 <b>0건</b>이라(실측) 이 스칼라의 쓰기 경로는 실질적으로 없다.</para>
    /// </remarks>
    [JsonProperty("gate_status", Order = 12, NullValueHandling = NullValueHandling.Ignore)]
    public string? GateStatus
    {
        get => GateStateEffective;
        set => _gateStatus = value;
    }

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

    #region - 7.0 축(axis) 투영 (FR-09) -
    /// <summary>
    /// 7.0 <c>type_gate</c>(<c>EnumGateType</c> = Sliding·Swing·Barrier·Unknown) — <b>개폐 방식</b> 축.
    /// </summary>
    /// <remarks>
    /// 6.3 에는 대응 필드가 없다(서버도 NOT NULL <c>Unknown</c> 으로 시작). 모르면 보내지 않고
    /// 서버 기본값에 맡긴다 — <c>GateUpdate</c> 의 <c>null</c> 은 422 다.
    /// 결선 방식(접점·컨버터)은 종류가 아니라 <c>connection</c> 축이다.
    /// </remarks>
    [JsonProperty("type_gate", Order = 29, NullValueHandling = NullValueHandling.Ignore)]
    public string? TypeGate { get; set; }

    public bool ShouldSerializeTypeGate() => UseAxisWrite && TypeGate != null;

    /// <summary>
    /// 7.0 <c>connection</c> — 6.3 평면 <c>urls</c>·<c>link_info</c> 의 새 자리(A-devices D-12:
    /// 결선은 <c>type</c>·<c>parent_device_id</c>·<c>channel</c>, 링크는 <c>connection.urls</c>).
    /// </summary>
    /// <remarks>
    /// <para>실을 내용이 없으면 <c>null</c> — <c>GateCreate.connection</c> 은 <b>필수가 아니다</b>
    /// (<c>required=[number_device, name_device]</c>).</para>
    /// <para><c>link_info.type</c> 은 <c>EnumConnectionType</c> 어휘로 그대로 넘긴다. 벗어난 값을
    /// 임의 보정하면 결선 방식을 조용히 바꿔 버리므로, 서버가 422 로 거부하게 둔다.</para>
    /// <para>setter 는 <b>7.0+ 응답 역투영</b>이다(D-24) — 응답에 평면 <c>urls</c>·<c>link_info</c> 가
    /// 없으므로 이 되돌림이 없으면 통문 링크와 결선 정보가 화면에서 사라진다.
    /// 되돌릴 때 상위 장비 id 는 6.3 이름 <c>parent_hint</c> 로도 함께 남겨 기존 호출부를 지킨다.</para>
    /// <para>원격 운영 6.3.2 에는 <c>/api/devices/gates</c> 경로가 <b>0건</b>이다(실측) —
    /// 통문은 6.3.17 이후 판에서만 존재하므로 이 투영은 실질적으로 7.0 전용이다.</para>
    /// </remarks>
    [JsonProperty("connection", Order = 30, NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public ConnectionAxisDto? ConnectionAxis
    {
        get
        {
            var link = LinkInfo;
            var type = DeviceAxisWrite.NullIfEmpty(link?.Value<string>("type"));
            var channel = link?.Value<int?>("channel");
            // 6.3 은 상위 장비를 'parent_hint' 로 불렀다 — 7.0 자리 이름은 'parent_device_id' 다.
            var parent = link?.Value<int?>("parent_device_id") ?? link?.Value<int?>("parent_hint");

            if (type == null && channel == null && parent == null && Urls == null) return null;

            return new ConnectionAxisDto
            {
                Type = type,
                Channel = channel,
                ParentDeviceId = parent,
                Urls = Urls,
            };
        }
        set
        {
            ReceivedConnection = value;   // raw capture for read mapping (device-console-v8 FR-03)
            if (value == null) return;

            if (DeviceAxisWrite.AsJObject(value.Urls) is { } urls) Urls = urls;

            var link = LinkInfo ?? new JObject();
            if (DeviceAxisWrite.NullIfEmpty(value.Type) is { } type) link["type"] = type;
            if (value.Channel.HasValue) link["channel"] = value.Channel.Value;
            if (value.ParentDeviceId.HasValue)
            {
                link["parent_device_id"] = value.ParentDeviceId.Value;
                link["parent_hint"] = value.ParentDeviceId.Value;   // 6.3 이름 — 기존 호출부 보존
            }
            if (link.Count > 0) LinkInfo = link;
        }
    }

    public bool ShouldSerializeConnectionAxis() => UseAxisWrite && ConnectionAxis != null;

    /// <summary>
    /// 7.0+ <c>hardware_spec</c> — 통문은 <c>DOOR_ACTUATOR</c>·<c>LIMIT_SWITCH</c>·<c>DOOR_LOCK</c>·
    /// <c>CONTACT_INPUT</c> 을 선언한다(명세 §5.12 생성표).
    /// </summary>
    /// <remarks>문 위치를 <b>선언된 <c>DOOR_ACTUATOR</c> 부품 key</b> 로 읽기 위한 재료다.</remarks>
    [JsonProperty("hardware_spec", Order = 31, NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public HardwareSpecDto? HardwareSpec
    {
        get => HardwareSpecCore;
        set => HardwareSpecCore = value;
    }

    public bool ShouldSerializeHardwareSpec() => ShouldSerializeHardwareSpecCore();

    // ── 7.0 에서 제거된 평면 키 ──
    /// <summary>문 위치는 쓰기 자리가 달라졌다 — <c>PATCH /{type}/{id}/component-status</c>(D-12·D-30).</summary>
    public bool ShouldSerializeGateStatus() => !UseAxisWrite && GateStatus != null;

    public bool ShouldSerializeUrls() => !UseAxisWrite && Urls != null;
    public bool ShouldSerializeLinkInfo() => !UseAxisWrite && LinkInfo != null;
    #endregion

    #region - 문 위치 읽기 (판본 무관) -
    /// <summary>
    /// 문 위치 — <b>6.3 스칼라 / 7.0+ 관측 축</b> 어느 쪽에서든 읽는다. 모르면 <c>null</c>.
    /// 7.0+ 는 <c>OPEN</c>·<c>CLOSED</c> 외에 <c>RUNNING</c>(구동 중)이 올 수 있다.
    /// </summary>
    /// <remarks>
    /// 판정 순서는 명세 §5.0.3.1 그대로다:
    /// ① 관측 축에서 <b>선언된 <c>DOOR_ACTUATOR</c> 부품 key</b> 의 <c>state</c>
    /// → ② 선언이 비었으면 관례 key <c>actuator</c>, 그다음 <c>door</c>
    /// → ③ 관측 축 자체가 없으면(6.3 응답, 또는 <c>view=basic</c>) 스칼라 <c>gate_status</c>.
    /// <para><c>null</c> = "모름". <c>view=basic</c> 이라 섹션을 못 받은 경우도 여기로 떨어진다.</para>
    /// </remarks>
    [JsonIgnore]
    public string? GateStateEffective
        => DeviceStatusAxis == null
            ? DeviceAxisWrite.NullIfEmpty(_gateStatus)
            : DeviceAxisWrite.NullIfEmpty(GateComponent?.State);

    /// <summary>문 구동부의 관측 시각 — <b><c>null</c> 가능</b>(옛 스칼라에서 이관된 행에는 시각이 없다).</summary>
    [JsonIgnore]
    public string? GateStateObservedAt => GateComponent?.ObservedAt;

    /// <summary>문 구동부의 건강 상태(<see cref="ComponentHealthNames"/>).</summary>
    [JsonIgnore]
    public string? GateHealth => GateComponent?.Health;

    /// <summary>선언 우선 → 관례 key <c>actuator</c>·<c>door</c> 폴백으로 찾은 문 구동부 상태.</summary>
    [JsonIgnore]
    public ComponentStatusDto? GateComponent
        => ResolveComponentStatus(ComponentTypeNames.DoorActuator, ACTUATOR_KEY_FALLBACK, DOOR_KEY_FALLBACK);
    #endregion

    #region - Attributes -
    /// <summary>6.3 스칼라 <c>gate_status</c> 의 배후 값. <b>기본값을 두지 않는다</b> — 모르면 <c>null</c>.</summary>
    private string? _gateStatus;

    /// <summary>명세 §5.0.3.1 폴백 어휘 — 통문은 <c>actuator</c>·<c>door</c> 순서다(라이브 key 는 <c>actuator</c>).</summary>
    private const string ACTUATOR_KEY_FALLBACK = "actuator";
    private const string DOOR_KEY_FALLBACK = "door";
    #endregion
}
