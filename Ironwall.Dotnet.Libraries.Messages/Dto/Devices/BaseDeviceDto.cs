using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// Device 공통 기반 DTO
/// <para>모든 장비 타입(Controller, Sensor, Camera, Speaker, Enclosure, Lamp)의 공통 속성</para>
/// <para>Event DTO의 nested device 필드에 사용</para>
/// </summary>
public class BaseDeviceDto : BaseDto
{
    /// <summary>
    /// 디바이스 번호
    /// </summary>
    [JsonProperty("number_device", Order = 2)]
    public int NumberDevice { get; set; }

    /// <summary>
    /// 디바이스 이름
    /// </summary>
    [JsonProperty("name_device", Order = 3)]
    public string NameDevice { get; set; } = string.Empty;

    /// <summary>
    /// 디바이스 타입 (discriminator: "Controller", "Sensor", "IpCamera", "IpSpeaker", "Enclosure", "Lamp")
    /// </summary>
    [JsonProperty("type_device", Order = 4)]
    public string TypeDevice { get; set; } = string.Empty;

    /// <summary>
    /// 디바이스 상태 (EnumDeviceStatus: "ACTIVATED", "ERROR", "DEACTIVATED")
    /// </summary>
    [JsonProperty("status", Order = 5)]
    public string Status { get; set; } = "DEACTIVATED";

    /// <summary>
    /// 활성화 여부
    /// </summary>
    [JsonProperty("is_enable", Order = 6)]
    public bool IsEnable { get; set; }

    /// <summary>
    /// 복수 그룹 소속 (응답 전용 — 서버가 DeviceGroupDto 객체 배열로 반환)
    /// </summary>
    [JsonProperty("device_groups", Order = 7, NullValueHandling = NullValueHandling.Ignore)]
    public List<DeviceGroupDto>? DeviceGroups { get; set; }

    /// <summary>
    /// 그룹 ID 배열 (요청 전용 — Model.DeviceGroups에서 직접 매핑)
    /// </summary>
    [JsonProperty("group_ids", Order = 8, NullValueHandling = NullValueHandling.Ignore)]
    public List<int>? GroupIds { get; set; }

    /// <summary>
    /// 펌웨어 버전
    /// </summary>
    [JsonProperty("version", Order = 9)]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 위치 좌표 (위치 설명, 위도, 경도, 고도)
    /// </summary>
    [JsonProperty("geolocation", Order = 10, NullValueHandling = NullValueHandling.Ignore)]
    public GeolocationDto? Geolocation { get; set; }

    /// <summary>
    /// 소속 컨트롤러 ID (Sensor 등). 설계 Gop_Message_Broker §6.4 device.controller_id. optional.
    /// </summary>
    [JsonProperty("controller_id", Order = 11, NullValueHandling = NullValueHandling.Ignore)]
    public int? ControllerId { get; set; }

    /// <summary>
    /// 설계 문서 원칙: Event body의 nested device 객체에서는 created_at/updated_at 제외
    /// </summary>
    [JsonIgnore]
    public override string? CreatedAt { get => base.CreatedAt; set => base.CreatedAt = value; }

    [JsonIgnore]
    public override string? UpdatedAt { get => base.UpdatedAt; set => base.UpdatedAt = value; }

    #region - 서버 계약 세대 분기 (FR-09) -
    /// <summary>
    /// 이 DTO 를 <b>서버 7.0 이상의 축(axis) 쓰기 스키마</b>로 직렬화할지 여부.
    /// <c>false</c>(기본)면 <b>종전 6.3 본문이 한 바이트도 달라지지 않는다</b>.
    /// </summary>
    /// <remarks>
    /// <para>장비 쓰기는 한 본문으로 두 판본을 만족시킬 수 없다 — 6.3.2 는 <c>type_device</c>·<c>status</c>·
    /// <c>mode</c> 를 <b>요구</b>하고 7.0.1 은 <c>additionalProperties:false</c> 로 <b>금지</b>한다(실측 2026-09-18).
    /// 그래서 DTO 를 버전마다 복제하지 않고 <c>ShouldSerializeXxx()</c> 조건 직렬화로
    /// <b>한 DTO 가 두 계약을 모두 표현</b>하게 한다(레포 선례: <c>SpeakerDeviceDto.ShouldSerializeServer</c>).</para>
    /// <para>이 값은 <c>DeviceApiService</c> 가 쓰기 호출 직전에
    /// <c>IServerContractProbe.Contract</c> 로부터 설정한다. Messages 어셈블리는 Api 를 참조하지 않으므로
    /// 계약 세대를 <c>bool</c> 한 칸으로만 받는다(레이어 역참조 방지).</para>
    /// <para>7.0 투영은 <b>읽기가 아니라 쓰기 전용</b>이다 — 축 응답 역직렬화는 별도 과제(A-devices D-24).</para>
    /// </remarks>
    [JsonIgnore]
    public bool UseAxisWrite { get; set; }

    /// <summary>
    /// 소속 부대 id — 서버 8.0 의 부대 편제 축(<c>/api/units</c>).
    /// </summary>
    /// <remarks>
    /// <para><b>전송 조건</b>: 서버 계약이 <c>V8_0</c> 이상일 때만이다. 운영 6.3.2 와 7.0.1 의 쓰기 스키마에는
    /// 이 키가 <b>없고</b> <c>extra="forbid"</c>(7.0)라 보내는 순간 422 다 — 그래서 값 주입 자체를
    /// <c>DeviceApiService</c> 가 계약 세대로 게이트한다(여기서는 <c>null</c> 이면 자동 생략).</para>
    /// <para>8.0 서버는 생략 시 기본 부대로 귀속시키고 <b>서버 로거 경고만</b> 남긴다(응답 <c>warnings[]</c> 아님) —
    /// 즉 누락을 클라이언트가 관측할 수 없으므로 8.0 에서는 <b>명시 전송</b>이 원칙이다.</para>
    /// </remarks>
    [JsonProperty("unit_id", Order = 12, NullValueHandling = NullValueHandling.Ignore)]
    public int? UnitId { get; set; }

    /// <summary>7.0 에서 제거된 키(<c>_legacy.py REMOVED_MOVES["type_device"]=("7.0",…)</c>) — 축 모드에선 미전송.</summary>
    public bool ShouldSerializeTypeDevice() => !UseAxisWrite;

    /// <summary>7.0 에서 <c>hardware_spec.firmware</c> 로 이관된 키. 빈 문자열이면 6.3 에서도 그대로였고, 축 모드에선 미전송.</summary>
    public bool ShouldSerializeVersion() => !UseAxisWrite;

    /// <summary>응답 전용 키 — 7.0 쓰기 스키마 properties 에 없어 실으면 422. 쓰기는 <see cref="GroupIds"/> 를 쓴다.</summary>
    public bool ShouldSerializeDeviceGroups() => !UseAxisWrite && DeviceGroups != null;
    #endregion

    #region - 축(axis) 응답 수용 (A-devices D-24) -
    // ⚠ 축 객체 프로퍼티(connection·device_config·hardware_spec·device_status)에는
    //   [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)] 가 **필수**다.
    //   Newtonsoft 기본값 Auto 는 역직렬화 때 getter 가 돌려준 기존 인스턴스에 값을 채우고
    //   **setter 를 호출하지 않는다**. 우리 축 getter 는 평면 필드로부터 매번 새 객체를 만들어
    //   돌려주므로(=항상 non-null) 역투영 setter 가 영영 안 불리고, 응답의 축 값이 조용히 버려진다.
    //   (실측 2026-09-18: Replace 없이는 카메라 IP·포트·계정·함체 임계치가 전부 유실됐고,
    //    배후가 null 이던 Gate.connection·hardware_spec 만 우연히 동작했다.)

    /// <summary>
    /// 7.0+ 응답의 <c>category_device</c>(<c>controller</c>·<c>sensor</c>·<c>camera</c>·<c>speaker</c>·
    /// <c>enclosure</c>·<c>lamp</c>·<c>gate</c>) — <b>읽기 전용</b>.
    /// </summary>
    /// <remarks>
    /// 카테고리는 <b>경로가 정한다</b>. 쓰기 스키마 properties 에 이름은 있으나
    /// <c>DEVICE_READ_ONLY_KEYS</c> 라 실으면 <c>READ_ONLY_IGNORED</c> 경고만 받고 무시된다 —
    /// 우리 봉투에 <c>warnings</c> 수신이 없으므로(D-26) 아예 보내지 않는다.
    /// </remarks>
    [JsonProperty("category_device", Order = 13, NullValueHandling = NullValueHandling.Ignore)]
    public string? CategoryDevice { get; set; }

    public bool ShouldSerializeCategoryDevice() => false;

    /// <summary>
    /// 7.0+ 응답의 관측 축 <c>device_status</c> — <b>읽기 전용</b>. 문 위치·부품 건강의 유일한 자리.
    /// </summary>
    /// <remarks>
    /// <para>요청 본문에 실으면 <c>422 OBSERVED_FIELD</c> 라 <b>절대 직렬화하지 않는다</b>
    /// (<see cref="DeviceStatusAxisDto"/> remarks).</para>
    /// <para><c>null</c> 은 "부품이 없다"가 아니라 <b>"섹션을 안 받았다"</b> 일 수 있다 —
    /// 목록 기본 프로필 <c>view=basic</c> 에서는 이 키가 오지 않는다(D-25).</para>
    /// </remarks>
    [JsonProperty("device_status", Order = 14, NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public DeviceStatusAxisDto? DeviceStatusAxis { get; set; }

    public bool ShouldSerializeDeviceStatusAxis() => false;

    #region - 축 수신 원본 보존 (device-console-v8 FR-03) — 읽기 전용, 직렬화되지 않는다 -
    /// <summary>
    /// 응답으로 <b>받은 그대로의</b> <c>connection</c> 축. 없었으면 <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <para>파생 DTO 의 <c>ConnectionAxis</c> getter 는 평면 필드(IP·포트·계정…)에서 축을 <b>재조립</b>한다 —
    /// 쓰기 본문을 만들기 위한 것이라 서버가 준 <c>type</c>·<c>parent_device_id</c>·<c>channel</c>·<c>schema</c> 가
    /// 거기엔 없다. 화면이 "받은 값"을 보이려면 재조립본이 아니라 이 원본을 읽어야 한다.</para>
    /// <para><c>connection</c> 을 선언하지 않은 DTO(센서·스피커·함체)는 <see cref="_unmappedKeys"/> 에서 건진다 —
    /// 8.0.1 은 그 카테고리에도 <c>connection</c> 을 싣는다(실측 2026-09-19, <c>meta.sections</c> 에 포함).</para>
    /// </remarks>
    [JsonIgnore]
    public ConnectionAxisDto? ReceivedConnection { get; set; }

    /// <summary>
    /// 응답으로 <b>받은 그대로의</b> <c>device_config</c> 축(임계치·모드·부품 덮어쓰기 세 묶음 전부). 없었으면 <c>null</c>.
    /// </summary>
    /// <remarks>파생 DTO 의 <c>DeviceConfigAxis</c> getter 는 자기가 쓸 묶음만 재조립한다(카메라=모드, 함체=임계치+덮어쓰기).</remarks>
    [JsonIgnore]
    public DeviceConfigAxisDto? ReceivedDeviceConfig { get; set; }

    /// <summary>
    /// 이 DTO 가 선언하지 않은 응답 키의 임시 보관소 — 역직렬화가 끝나면 필요한 축만 건지고 <b>비운다</b>.
    /// <c>WriteData=false</c> 라 쓰기 본문에는 절대 나가지 않는다(7.0+ 쓰기 스키마는 <c>extra="forbid"</c>).
    /// </summary>
    [JsonExtensionData(WriteData = false)]
    private IDictionary<string, JToken>? _unmappedKeys;

    [OnDeserialized]
    internal void CaptureUndeclaredAxes(StreamingContext context)
    {
        if (_unmappedKeys == null) return;

        if (ReceivedConnection == null && _unmappedKeys.TryGetValue("connection", out var connection) && connection is JObject)
            ReceivedConnection = connection.ToObject<ConnectionAxisDto>();

        if (ReceivedDeviceConfig == null && _unmappedKeys.TryGetValue("device_config", out var config) && config is JObject)
            ReceivedDeviceConfig = config.ToObject<DeviceConfigAxisDto>();

        // 이벤트 본문의 nested device 로도 쓰이는 DTO 다 — 안 쓰는 키를 이벤트마다 쥐고 있지 않는다.
        _unmappedKeys = null;
    }
    #endregion

    /// <summary>
    /// <c>hardware_spec</c> 의 공용 배후 저장소. 파생 DTO 가 자기 <c>Order</c> 로 노출한다
    /// (기반에 <c>[JsonProperty]</c> 를 두면 6.3 카메라 본문의 키 순서가 바뀐다 — 무회귀 위반).
    /// </summary>
    [JsonIgnore]
    protected HardwareSpecDto? HardwareSpecStore;

    /// <summary>
    /// 파생 DTO 의 <c>hardware_spec</c> 접근자가 위임하는 지점.
    /// setter 는 <b>7.0 응답 역투영</b>도 겸한다 — <c>version</c> 이 <c>hardware_spec.firmware</c> 로
    /// 이관됐으므로(명세 §5 머리 이관표) 장비 <see cref="Version"/> 이 비어 있으면 거기서 채운다.
    /// </summary>
    protected HardwareSpecDto? HardwareSpecCore
    {
        get => HardwareSpecStore;
        set
        {
            HardwareSpecStore = value;
            if (value != null && string.IsNullOrWhiteSpace(Version) && !string.IsNullOrWhiteSpace(value.Firmware))
                Version = value.Firmware;
        }
    }

    /// <summary>
    /// 축 모드에서만 <c>hardware_spec</c> 을 싣고, 직렬화 직전에 자식 DTO 에 같은 계약 세대를 전파한다.
    /// </summary>
    /// <remarks>
    /// 6.3 의 <b>카메라를 제외한</b> 6종 쓰기 스키마에는 <c>hardware_spec</c> 자체가 없다 —
    /// 그래서 6.3 모드에서는 값이 있어도 보내지 않는다(본문 무회귀).
    /// </remarks>
    protected bool ShouldSerializeHardwareSpecCore()
    {
        if (HardwareSpecStore != null) HardwareSpecStore.UseAxisWrite = UseAxisWrite;
        return UseAxisWrite && HardwareSpecStore != null;
    }

    /// <summary>
    /// <c>hardware_spec.components[]</c> 에서 주어진 유형의 부품 <c>key</c> 를 찾는다(대소문자 무시).
    /// </summary>
    /// <remarks>
    /// 명세 §5.0.3.1 의 문 위치 판정 규칙 ①: "부품 <b>유형</b>은 그 장비의
    /// <c>hardware_spec.components[]</c> 선언에서 읽는다 — <b>선언이 있으면 선언이 이긴다</b>".
    /// </remarks>
    protected string? FindComponentKeyByType(string componentType)
    {
        var list = HardwareSpecStore?.Components;
        if (list == null) return null;
        foreach (var c in list)
        {
            if (c != null && string.Equals(c.Type, componentType, StringComparison.OrdinalIgnoreCase))
                return c.Key;
        }
        return null;
    }

    /// <summary>
    /// 문 위치 부품의 상태를 판본 무관하게 찾는다 — 선언(유형) 우선, 없으면 <paramref name="fallbackKeys"/> 관례 key.
    /// </summary>
    /// <remarks>
    /// 명세 §5.0.3.1 규칙 ②: "형상이 <b>아직 비어 있으면</b> 관례 key 로 폴백한다
    /// (함체 <c>door</c> → DOOR_SENSOR, 통문 <c>actuator</c>·<c>door</c> → DOOR_ACTUATOR)".
    /// </remarks>
    protected ComponentStatusDto? ResolveComponentStatus(string componentType, params string[] fallbackKeys)
    {
        var axis = DeviceStatusAxis;
        if (axis?.Components == null) return null;

        var declared = FindComponentKeyByType(componentType);
        if (declared != null)
        {
            var hit = axis.Find(declared);
            if (hit != null) return hit;
        }

        foreach (var key in fallbackKeys)
        {
            var hit = axis.Find(key);
            if (hit != null) return hit;
        }
        return null;
    }
    #endregion
}
