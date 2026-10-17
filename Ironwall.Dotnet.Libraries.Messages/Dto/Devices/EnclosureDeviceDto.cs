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
    /// <see cref="HeaterEnabled"/> 가 실제 의도인가 — <c>false</c> 면 이 부품에 <c>enabled</c> 설정이 없다("설정 없음").
    /// 그때는 7.0+ <c>component_overrides</c> 에 <c>enabled</c> 를 싣지 않는다. 직렬화되지 않는다.
    /// </summary>
    /// <remarks>
    /// 7.0+ 응답(<see cref="DeviceConfigAxis"/> setter)이 이 칸을 정한다: 받은 <c>component_overrides</c> 에 그 부품의
    /// <c>enabled</c> 가 있으면 <c>true</c>, 없으면 <c>false</c>. 기본값 <c>true</c> 는 종전 동작 그대로다(코드가 직접
    /// 만든 DTO · 6.3 평면 응답은 늘 값이 있다). 이 칸이 없을 때는 설정 없는 팬을 저장할 때마다 <c>enabled:false</c> 가
    /// 지어내졌다(라이브 하네스 dl.4, 2026-09-26).
    /// </remarks>
    [JsonIgnore]
    public bool HeaterEnabledKnown { get; set; } = true;

    /// <summary><see cref="FanEnabled"/> 가 실제 의도인가 — <see cref="HeaterEnabledKnown"/> 과 같은 계약.</summary>
    [JsonIgnore]
    public bool FanEnabledKnown { get; set; } = true;

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
            return ip == null && port == null ? null : DeviceAxisWrite.BuildIpConnection(IpAddress, IpPort ?? 0, storedType: PreservedConnectionType);
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

            // "설정 없음"(…EnabledKnown=false)인 부품에는 enabled 를 지어내지 않는다 — 받은 항목은 _componentOverrides 로 그대로 간다.
            if (HeaterEnabledKnown) SetEnabledIfPresent(overrides, ComponentTypeNames.Heater, HeaterComponentKeyHint, HeaterEnabled);
            if (FanEnabledKnown) SetEnabledIfPresent(overrides, ComponentTypeNames.Fan, FanComponentKeyHint, FanEnabled);

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
            if (value == null)
            {
                // 7.0+ 가 device_config 를 비워 보냈다 — 어떤 부품에도 설정이 없다.
                HeaterEnabledKnown = false;
                FanEnabledKnown = false;
                return;
            }

            // 임계치 역투영 — 7.0+ 응답에 평면 threshold_config 가 없다(D-24).
            if (DeviceThresholdAxis.FromAxis(value.Thresholds) is { } legacy) ThresholdConfig = legacy;

            _componentOverrides = value.ComponentOverrides;

            var heaterKey = FindComponentKeyByType(ComponentTypeNames.Heater) ?? HEATER_KEY_FALLBACK;
            var fanKey = FindComponentKeyByType(ComponentTypeNames.Fan) ?? FAN_KEY_FALLBACK;

            // 받은 설정이 없으면 "설정 없음"으로 적는다 — false(기본값)를 의도로 되보내지 않게(HeaterEnabledKnown).
            var heater = value.ComponentOverrides == null ? null : ReadEnabled(value.ComponentOverrides, heaterKey);
            var fan = value.ComponentOverrides == null ? null : ReadEnabled(value.ComponentOverrides, fanKey);
            HeaterEnabledKnown = heater.HasValue;
            FanEnabledKnown = fan.HasValue;
            if (heater is { } h) HeaterEnabled = h;
            if (fan is { } f) FanEnabled = f;
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
    /// 히터 부품 key <b>조회 전용 힌트</b> — 쓰기 채널(<see cref="HardwareSpec"/>/<c>HardwareSpecStore</c>)과
    /// 완전히 분리된 자리다. <b><c>[JsonIgnore]</c> — 본문에 절대 실리지 않는다.</b>
    /// </summary>
    /// <remarks>
    /// (D-31 후속 수정, 2026-09-23) 패널의 평범한 편집 경로(<c>DtoToModelHelper.ToEnclosureDeviceDto</c>)는
    /// <c>hardware_spec</c> 을 쓰기 채널에 실은 적이 없다 — 실으면 축소판 문제가 생긴다: 서버는
    /// <b><c>PATCH</c> 에서도 <c>hardware_spec.components</c> 배열을 통째 교체</b>한다(레포 메모
    /// <c>device_six_axes_and_component_catalog</c> — "components 는 PATCH 도 통째 교체"). 조회 목적으로
    /// 채운 값이 실수로 실려 나가면 실제 형상 선언을 덮어쓸 위험이 있다. 그래서 "이 부품이 선언돼 있다면
    /// 그 key" 를 <see cref="SetEnabledIfPresent"/> 에 전달하는 통로를 <b>쓰기 채널과 별도로</b> 둔다 —
    /// 호출부(<c>DtoToModelHelper.ToEnclosureDeviceDto</c>)가 모델의 읽기-시점 캐시
    /// (<c>EnclosureDeviceModel.HeaterComponentKey</c> — <c>DtoToModelHelper.ToEnclosureDeviceModel</c> 이
    /// 직전 GET 응답의 <c>hardware_spec.components</c> 에서 채워 둔 값)를 여기 채운다.
    /// </remarks>
    [JsonIgnore]
    public string? HeaterComponentKeyHint { get; set; }

    /// <summary>팬 부품 key 조회 전용 힌트 — <see cref="HeaterComponentKeyHint"/> 와 같은 계약.</summary>
    [JsonIgnore]
    public string? FanComponentKeyHint { get; set; }

    /// <summary>
    /// 그 부품이 <b>선언돼 있다고 확인됐을 때만</b> <c>enabled</c> 의도를 싣는다.
    /// </summary>
    /// <remarks>
    /// <para>(D-31 수정, 2026-09-23) <c>component_overrides</c> 의 키는 <b>그 장비가 선언한 부품 key</b>
    /// 여야 하고 아니면 422 다("'x' 는 이 장비가 선언한 부품이 아닙니다 — <c>hardware_spec.components</c>
    /// 에 먼저 넣으십시오") — 서버 <c>app/schemas/device_axes.py:1004-1013</c>, <b>v7.0.0 부터</b>
    /// (CHANGELOG.md:411, 모듈 <c>device_axes_io.py</c> 머리 "v7.0 에서 바뀐 것 · D10") 계속 적용된다.
    /// 축 쓰기(<c>UseAxisWrite</c>)는 계약 7.0+ 에서만 켜지므로 이 메서드가 불릴 때는 이미 이 규칙
    /// 아래다 — 판본 분기는 필요 없다.</para>
    /// <para><b>판정 순서</b>: ① 이 DTO 인스턴스 자체가 쓰기 채널에 <c>HardwareSpec</c> 을 들고 있으면
    /// (예: 응답을 그대로 들고 있는 DTO) 그 선언이 <b>최우선</b>이다 — 가장 신선한 신호다.
    /// ② 없으면 <paramref name="keyHint"/>(조회 전용, 본문에 안 실림)로 폴백한다.
    /// ③ 그래도 없으면 <b>아예 싣지 않는다</b> — 관례 key(<c>heater</c>/<c>fan</c>)로 추측하지 않는다.</para>
    /// <para><b>왜 ③이 필요한가(D-31 원인)</b> — 예전엔 선언을 전혀 모를 때도 관례 key 를 무조건 실어
    /// <c>HeaterEnabled</c>/<c>FanEnabled</c> 기본값(<c>false</c>)까지 실려 나갔고, 아무 것도 선언하지 않은
    /// <b>평범한 함체를 생성하기만 해도</b>(체크 하나 안 건드려도) 매번 422 였다(실 API 왕복 하네스 11b0).</para>
    /// <para><b>왜 ②가 필요한가(D-31 후속)</b> — ③만 있으면 <b>선언된</b> 부품(프리셋·조립기로 등록된
    /// 히터·팬)조차 패널 편집에서 매번 무시돼, 저장은 성공하는데 조작이 조용히 사라진다(루프백 API
    /// 왕복 하네스 11d 가 이 경로를 검증한다). ②는 <c>hardware_spec</c> 을 본문에 싣지 않고도 그 실패를 막는다.</para>
    /// </remarks>
    private void SetEnabledIfPresent(JObject overrides, string componentType, string? keyHint, bool enabled)
    {
        var declared = FindComponentKeyByType(componentType) ?? keyHint;
        if (declared == null) return;   // 선언도 힌트도 없다 — 관례 key 로 추측하지 않는다.

        if (overrides[declared] is JObject existing) existing["enabled"] = enabled;
        else overrides[declared] = new JObject { ["enabled"] = enabled };
    }

    private static bool? ReadEnabled(JObject overrides, string key)
    {
        var token = overrides[key]?["enabled"];
        return token != null && token.Type == JTokenType.Boolean ? token.Value<bool>() : null;
    }
    #endregion
}
