using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// Enclosure(함체) 디바이스 DTO (§5.5)
/// <para>서버 6.3(평면 필드)과 7.0+(축 필드)을 <b>같은 DTO 한 개</b>로 표현한다 —
/// <see cref="BaseDeviceDto.UseAxisWrite"/> 가 <c>true</c> 면 평면 키를 끄고 축 키를 켠다.</para>
/// </summary>
public class EnclosureDeviceDto : BaseDeviceDto
{
    public EnclosureDeviceDto()
    {
        TypeDevice = "Enclosure";
    }

    /// <summary>
    /// 도어 상태 (EnumDoorStatus: CLOSED, OPEN)
    /// </summary>
    /// <remarks>
    /// <para><b>7.0+ 응답에는 이 키가 없다</b> — <c>enclosures.door_status</c> 스칼라 컬럼이 제거되고
    /// 문 위치가 <c>device_status.components.&lt;DOOR_SENSOR 부품 key&gt;.state</c> 로 옮겨졌다
    /// (명세 §4 "<s>EnumDoorStatus</s>" 절 · §5 머리 이관표 · §5.5.2 "문 위치는
    /// <c>device_status.components.door.state</c> 입니다"). 그래서 getter 가 <b>관측 축을 먼저</b> 본다 —
    /// 그러지 않으면 키가 비어 기본값 <c>"CLOSED"</c> 가 남고 <b>문이 열려 있어도 화면은 닫힘</b>으로
    /// 굳는다(A-devices D-24, 422 도 예외도 없이 화면만 거짓말한다).</para>
    /// <para><b>nullable 이고 이니셜라이저가 없다</b>(2026-09-18 정정). 종전
    /// <c>public string DoorStatus { get; set; } = "CLOSED";</c> 는 서버가 키를 안 보낼 때
    /// Newtonsoft 가 이니셜라이저 값을 남기므로 <b><c>"CLOSED"</c> 가 실제 관측값처럼 도착</b>했다 —
    /// 호출부의 <c>?? "CLOSED"</c> 방어는 그래서 <b>한 번도 발동하지 않았다</b>. "모름"을 값으로
    /// 표현할 수 있어야 화면이 거짓말을 멈춘다.</para>
    /// <para><b>쓰기 무회귀</b>: 6.3.2 <c>EnclosureCreate.door_status</c> 는 <b>선택</b>이고
    /// <c>default:"CLOSED"</c> 라(실측) 미설정 시 키를 빼도 서버 저장값이 같다. 축 모드에서는
    /// <see cref="ShouldSerializeDoorStatus"/> 가 키를 아예 빼므로 무영향이다.</para>
    /// </remarks>
    [JsonProperty("door_status", Order = 11, NullValueHandling = NullValueHandling.Ignore)]
    public string? DoorStatus
    {
        get => DoorStateEffective;
        set => _doorStatus = value;
    }

    /// <summary>
    /// 임계값 설정 (JSONB) — null이면 직렬화 생략(서버 저장값 null 덮어쓰기 방지)
    /// </summary>
    /// <remarks>
    /// 6.3 평면 어휘(<c>temp_high</c>·<c>temp_low</c>·<c>humidity_high</c>·<c>current_high</c>·
    /// <c>voltage_low</c>·<c>vibration_high</c> — 운영 6.3.2 <c>EnclosureThresholdConfig</c> 실측 6키)를
    /// 그대로 유지한다. 7.0+ 와의 변환은 <see cref="DeviceThresholdAxis"/> 가 양방향으로 처리하므로
    /// <b>호출부는 판본을 몰라도 된다</b>.
    /// </remarks>
    [JsonProperty("threshold_config", Order = 12, NullValueHandling = NullValueHandling.Ignore)]
    public JObject? ThresholdConfig { get; set; }

    /// <summary>
    /// 히터 활성화 여부
    /// </summary>
    [JsonProperty("heater_enabled", Order = 13)]
    public bool HeaterEnabled { get; set; }

    /// <summary>
    /// 팬 활성화 여부
    /// </summary>
    [JsonProperty("fan_enabled", Order = 14)]
    public bool FanEnabled { get; set; }

    /// <summary>
    /// 장비 설명 — 서버 8.0.1 은 7 카테고리 공통으로 저장한다(하네스가 함체에 raw PATCH 로 실측 확인,
    /// device-assembly-preset 왕복 하네스). <c>null</c> 이면 생략한다("값 없음" ≠ "지워라" — <c>NullValueHandling.Ignore</c>).
    /// </summary>
    [JsonProperty("description", Order = 15, NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    #region - 7.0 축(axis) 투영 (FR-09) -
    /// <summary>
    /// 7.0 <c>type_enclosure</c>(<c>EnumEnclosureType</c> = Outdoor·Indoor·Unknown) — <b>설치 환경</b> 축.
    /// </summary>
    /// <remarks>
    /// 6.3 에는 대응 필드가 <b>없다</b>(백필 불가 — 서버도 NOT NULL <c>Unknown</c> 으로 시작한다).
    /// 그래서 값이 없으면 보내지 않고 서버 기본값(<c>Unknown</c>)에 맡긴다. 응답은 필수 키라 항상 채워진다.
    /// <c>EnclosureUpdate</c> 에서 <c>null</c> 은 422 이므로 <b>모를 때 <c>null</c> 을 실지 않는다</b>.
    /// </remarks>
    [JsonProperty("type_enclosure", Order = 29, NullValueHandling = NullValueHandling.Ignore)]
    public string? TypeEnclosure { get; set; }

    public bool ShouldSerializeTypeEnclosure() => UseAxisWrite && TypeEnclosure != null;

    /// <summary>
    /// 축 전용 배후 저장소 — 6.3 함체 쓰기 스키마에는 <c>ip_address</c>·<c>ip_port</c> 자리가 <b>아예 없었다</b>
    /// (D-21 원인: <c>EnclosureDeviceDto</c> 가 이 필드를 전혀 선언하지 않아 프리셋 등록에서 IP·포트가
    /// 조용히 유실됐다 — 서버 스키마 <c>app/schemas/device.py:776</c>는 함체도 <c>connection: Optional[ConnectionAxis]</c>
    /// 를 받는다). <c>[JsonIgnore]</c> 라 6.3 본문 바이트는 절대 늘지 않는다 — 오직 <see cref="ConnectionAxis"/>
    /// 를 통해서만 나간다.
    /// </summary>
    [JsonIgnore]
    public string? IpAddress { get; set; }

    [JsonIgnore]
    public int? IpPort { get; set; }

    /// <summary>
    /// 7.0 <c>connection</c> — 함체는 <c>ControllerConnectionAxis</c> 처럼 필수가 아니다
    /// (<c>EnclosureCreate.connection: Optional[ConnectionAxis] = None</c>). IP 가 하나도 없으면
    /// <c>null</c> 을 돌려줘 키 자체를 뺀다 — <see cref="GateDeviceDto.ConnectionAxis"/> 와 같은 "선택 축" 관례.
    /// </summary>
    /// <remarks>setter 는 7.0+ 응답 역투영(D-24) — 없으면 함체 IP·포트가 화면에서 사라진다.</remarks>
    [JsonProperty("connection", Order = 30, NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public ConnectionAxisDto? ConnectionAxis
    {
        get
        {
            var ip = DeviceAxisWrite.NullIfEmpty(IpAddress);
            var port = DeviceAxisWrite.PortOrNull(IpPort ?? 0);
            return ip == null && port == null ? null : DeviceAxisWrite.BuildIpConnection(IpAddress, IpPort ?? 0);
        }
        set
        {
            ReceivedConnection = value;   // raw capture for read mapping (device-console-v8 FR-03)
            if (value == null) return;
            if (DeviceAxisWrite.NullIfEmpty(value.IpAddress) is { } ip) IpAddress = ip;
            if (value.IpPort is > 0) IpPort = value.IpPort.Value;
        }
    }

    public bool ShouldSerializeConnectionAxis() => UseAxisWrite && ConnectionAxis != null;

    /// <summary>
    /// 7.0+ <c>hardware_spec</c> — 함체는 계측 부품(<c>TEMPERATURE_SENSOR</c> 등)과
    /// <c>DOOR_SENSOR</c>·<c>HEATER</c>·<c>FAN</c> 을 선언한다(명세 §5.5.3).
    /// </summary>
    /// <remarks>
    /// <b>문 위치 읽기와 히터·팬 설정이 이 선언에 매여 있다</b> —
    /// <c>component_overrides</c> 는 선언된 key 만 받고, 문 위치는 선언에서 <c>DOOR_SENSOR</c> 의
    /// key 를 찾아 읽는다. 6.3 쓰기 스키마에는 이 키가 없어 축 모드에서만 전송한다.
    /// </remarks>
    [JsonProperty("hardware_spec", Order = 31, NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public HardwareSpecDto? HardwareSpec
    {
        get => HardwareSpecCore;
        set => HardwareSpecCore = value;
    }

    public bool ShouldSerializeHardwareSpec() => ShouldSerializeHardwareSpecCore();

    /// <summary>
    /// 7.0 <c>device_config</c> — 6.3 평면 <c>threshold_config</c>·<c>heater_enabled</c>·<c>fan_enabled</c> 의 새 자리.
    /// </summary>
    /// <remarks>
    /// <para><b>임계치 어휘 확정(2026-09-18)</b> — 6.3 평면 → 7.0 <c>thresholds{metric:{high,low}}</c> 매핑은
    /// <see cref="DeviceThresholdAxis"/> 에 두었고 근거는 세 곳이 일치한다: 배포 Swagger 8.0.1
    /// <c>DeviceConfigAxis.thresholds</c> 설명(카탈로그 <c>metric_key</c> 6종) · 명세 §5 머리 이관표
    /// (<c>threshold_config</c> → <c>device_config.thresholds</c>, 예 <c>{"temperature":{"high":45}}</c>) ·
    /// <b>서버 자신의 백필 SQL <c>v98_device_axes_backfill.sql:46-51</c></b>.
    /// 종전에 "부품 키 미확인"이라며 비워 두었던 자리다.</para>
    /// <para><b>임계치를 실어야 하는 이유</b> — <c>PUT</c> 은 "보낸 축은 통째 교체"다(명세 §5.5.5 경고).
    /// <c>component_overrides</c> 만 담은 <c>device_config</c> 를 보내면 <b>서버의 임계치가 전부 사라진다</b>.
    /// 두 섹션은 같은 축이라 함께 실어야 한다.</para>
    /// <para><b>부품 key</b> 는 선언(<see cref="HardwareSpec"/>)에서 유형으로 찾고, 선언이 없으면
    /// 관례 key(<c>heater</c>·<c>fan</c> — 명세 §5.5.2 실행 캡처)로 폴백한다. 선언이 있으면 선언이 이긴다.</para>
    /// <para>받은 <c>component_overrides</c> 중 우리가 표현하지 못하는 부품 항목은 그대로 보존해 다시 싣는다
    /// (통째 교체에서 남의 설정을 지우지 않기 위해).</para>
    /// </remarks>
    [JsonProperty("device_config", Order = 32, NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public DeviceConfigAxisDto? DeviceConfigAxis
    {
        get
        {
            var overrides = _componentOverrides != null
                ? (JObject)_componentOverrides.DeepClone()
                : new JObject();

            SetEnabledIfPresent(overrides, ComponentTypeNames.Heater, HEATER_KEY_FALLBACK, HeaterEnabled);
            SetEnabledIfPresent(overrides, ComponentTypeNames.Fan, FAN_KEY_FALLBACK, FanEnabled);

            var thresholds = DeviceThresholdAxis.ToAxis(ThresholdConfig);

            var computed = (thresholds == null && overrides.Count == 0)
                ? null
                : new DeviceConfigAxisDto
                {
                    Thresholds = thresholds,
                    ComponentOverrides = overrides.Count == 0 ? null : overrides,
                };

            // N-02 §3 공통 통로 — 호출자가 채운 조각(예: component_overrides.buzzer, 명시적 null 삭제)을 병합한다.
            // AllowDeviceConfigWrite 가 꺼져 있으면(기본) computed 그대로 — 오늘 본문과 바이트 단위 동일.
            return ComposeDeviceConfigAxis(computed);
        }
        set
        {
            ReceivedDeviceConfig = value;   // raw capture for read mapping (device-console-v8 FR-03)
            if (value == null) return;

            // 임계치 역투영 — 7.0+ 응답에 평면 threshold_config 가 없다(D-24).
            if (DeviceThresholdAxis.FromAxis(value.Thresholds) is { } legacy) ThresholdConfig = legacy;

            _componentOverrides = value.ComponentOverrides;
            if (value.ComponentOverrides == null) return;

            var heaterKey = FindComponentKeyByType(ComponentTypeNames.Heater) ?? HEATER_KEY_FALLBACK;
            var fanKey = FindComponentKeyByType(ComponentTypeNames.Fan) ?? FAN_KEY_FALLBACK;

            if (ReadEnabled(value.ComponentOverrides, heaterKey) is { } heater) HeaterEnabled = heater;
            if (ReadEnabled(value.ComponentOverrides, fanKey) is { } fan) FanEnabled = fan;
        }
    }

    public bool ShouldSerializeDeviceConfigAxis() => UseAxisWrite;

    // ── 7.0 에서 제거된 평면 키 ──
    /// <summary>문 위치는 쓰기 자리가 달라졌다 — <c>PATCH /{type}/{id}/component-status</c>(D-10·D-30).</summary>
    public bool ShouldSerializeDoorStatus() => !UseAxisWrite && DoorStatus != null;

    public bool ShouldSerializeThresholdConfig() => !UseAxisWrite && ThresholdConfig != null;
    public bool ShouldSerializeHeaterEnabled() => !UseAxisWrite;
    public bool ShouldSerializeFanEnabled() => !UseAxisWrite;
    #endregion

    #region - 문 위치 읽기 (판본 무관) -
    /// <summary>
    /// 문 위치 — <b>6.3 스칼라 / 7.0+ 관측 축</b> 어느 쪽에서든 읽는다. 모르면 <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <para>판정 순서는 명세 §5.0.3.1 그대로다:
    /// ① 관측 축에서 <b>선언된 <c>DOOR_SENSOR</c> 부품 key</b> 의 <c>state</c>
    /// → ② 선언이 비었으면 관례 key <c>door</c>
    /// → ③ 관측 축 자체가 없으면(6.3 응답, 또는 <c>view=basic</c>) 스칼라 <c>door_status</c>.</para>
    /// <para><c>null</c> 이 곧 "모름"이다 — 매니저의 첫 보고 전까지 문 위치에는 초기값이 없다
    /// (명세 §5.5.3: 생성 시 <c>door_status</c> 는 422, "문 위치 초기값은 없습니다").
    /// <c>view=basic</c> 이라 섹션을 못 받은 경우도 여기로 떨어진다 — 두 경우는 값으로 구분할 수 없다.</para>
    /// </remarks>
    [JsonIgnore]
    public string? DoorStateEffective
        => DeviceStatusAxis == null
            ? DeviceAxisWrite.NullIfEmpty(_doorStatus)
            : DeviceAxisWrite.NullIfEmpty(DoorComponent?.State);

    /// <summary>문 위치 부품의 관측 시각 — <b><c>null</c> 가능</b>(옛 스칼라에서 이관된 행에는 시각이 없다).</summary>
    [JsonIgnore]
    public string? DoorStateObservedAt => DoorComponent?.ObservedAt;

    /// <summary>문 위치 부품의 건강 상태(<see cref="ComponentHealthNames"/>).</summary>
    [JsonIgnore]
    public string? DoorHealth => DoorComponent?.Health;

    /// <summary>선언 우선 → 관례 key <c>door</c> 폴백으로 찾은 문 위치 부품 상태.</summary>
    [JsonIgnore]
    public ComponentStatusDto? DoorComponent
        => ResolveComponentStatus(ComponentTypeNames.DoorSensor, DOOR_KEY_FALLBACK);
    #endregion

    #region - Attributes -
    /// <summary>6.3 스칼라 <c>door_status</c> 의 배후 값. <b>기본값을 두지 않는다</b> — 모르면 <c>null</c>.</summary>
    private string? _doorStatus;

    /// <summary>7.0+ 응답에서 받은 <c>component_overrides</c> 원본 — 통째 교체에서 남의 항목을 지우지 않기 위해 보존.</summary>
    private JObject? _componentOverrides;

    /// <summary>관례 key(명세 §5.0.3.1 폴백 · §5.5.2 실행 캡처) — 선언이 있으면 선언이 이긴다.</summary>
    private const string DOOR_KEY_FALLBACK = "door";
    private const string HEATER_KEY_FALLBACK = "heater";
    private const string FAN_KEY_FALLBACK = "fan";

    /// <summary>
    /// 그 부품이 <b>선언돼 있을 때만</b> <c>enabled</c> 의도를 싣는다. 선언 목록 자체를 모르면 관례 key 로 싣는다.
    /// </summary>
    /// <remarks>
    /// <c>component_overrides</c> 의 키는 <b>그 장비가 선언한 부품 key</b> 여야 하고 아니면 422 다
    /// ("'x' 는 이 장비가 선언한 부품이 아닙니다 — <c>hardware_spec.components</c> 에 먼저 넣으십시오").
    /// 히터·팬이 <b>없는</b> 함체에 6.3 처럼 무조건 <c>heater</c>·<c>fan</c> 을 실으면 이름만 바꾸는
    /// 수정까지 전부 422 로 죽는다. 그래서 선언을 아는 경우(<c>components</c> 가 실려 온 경우)에는
    /// <b>선언에 있는 부품만</b> 싣고, 선언을 못 받았으면(<c>view=basic</c>·신규 생성) 6.3 의도대로
    /// 관례 key 로 실어 서버가 판정하게 둔다.
    /// </remarks>
    private void SetEnabledIfPresent(JObject overrides, string componentType, string fallbackKey, bool enabled)
    {
        var declared = FindComponentKeyByType(componentType);
        if (declared == null && HardwareSpecStore?.Components != null) return;   // 선언을 아는데 그 부품이 없다

        var key = declared ?? fallbackKey;
        if (overrides[key] is JObject existing) existing["enabled"] = enabled;
        else overrides[key] = new JObject { ["enabled"] = enabled };
    }

    private static bool? ReadEnabled(JObject overrides, string key)
    {
        var token = overrides[key]?["enabled"];
        return token != null && token.Type == JTokenType.Boolean ? token.Value<bool>() : null;
    }
    #endregion
}
