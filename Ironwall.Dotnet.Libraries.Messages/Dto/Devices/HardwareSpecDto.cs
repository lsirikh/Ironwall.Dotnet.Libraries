using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 카메라 하드웨어 스펙 DTO (9필드)
/// </summary>
public class HardwareSpecDto
{
    [JsonProperty("name", Order = 1)]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("location", Order = 2)]
    public string Location { get; set; } = string.Empty;

    [JsonProperty("manufacturer", Order = 3)]
    public string Manufacturer { get; set; } = string.Empty;

    [JsonProperty("model", Order = 4)]
    public string Model { get; set; } = string.Empty;

    [JsonProperty("hardware", Order = 5)]
    public string Hardware { get; set; } = string.Empty;

    /// <summary>
    /// 펌웨어 버전. <b>6.3 상한 100자 → 7.0+ 상한 50자</b>(실측: 운영 6.3.2 <c>maxLength:100</c> ·
    /// 8.0.1 <c>maxLength:50</c> = <c>app/schemas/device.py:363 Field(max_length=50)</c>).
    /// </summary>
    /// <remarks>
    /// <b>절단하지 않는다.</b> 명세에 축소에 따른 변환·절단 규정이 <b>없고</b>, 절단은 되돌릴 수 없는
    /// 데이터 손실이며 "2.11.03-build.20260918" 같은 값에서 뒤쪽 빌드 식별자가 조용히 사라진다.
    /// 51자 이상은 그대로 보내 <b>서버가 422 로 거부하게</b> 둔다 — 값이 잘렸다는 사실을 아무도
    /// 모르는 편보다 요청이 실패하는 편이 낫다. 미리 알고 싶으면 <see cref="IsFirmwareOverAxisLimit"/>.
    /// </remarks>
    [JsonProperty("firmware", Order = 6)]
    public string Firmware { get; set; } = string.Empty;

    [JsonProperty("device_id", Order = 7)]
    public string DeviceId { get; set; } = string.Empty;

    [JsonProperty("mac_address", Order = 8)]
    public string MacAddress { get; set; } = string.Empty;

    [JsonProperty("onvif_version", Order = 9)]
    public string OnvifVersion { get; set; } = string.Empty;

    /// <summary>최대 탐지거리(m) — GIS "특정 위치 확인" aim 반경/FOV 산출용. 미설정 시 직렬화 생략.</summary>
    [JsonProperty("max_detection_range", Order = 10, NullValueHandling = NullValueHandling.Ignore)]
    public double? MaxDetectionRange { get; set; }

    #region - 서버 계약 세대 분기 (FR-09 · A-devices D-22) -
    /// <summary>
    /// 7.0 이상의 <c>HardwareSpec</c> 스키마로 직렬화할지 여부. <c>false</c>(기본)면 6.3 본문 그대로.
    /// </summary>
    /// <remarks>
    /// 7.0 <c>HardwareSpec.additionalProperties=false</c> 이고 properties 는
    /// <c>[schema, manufacturer, model, serial, firmware, hardware_rev, mac_address,
    /// max_detection_range, onvif_version, spec, components]</c> 다. 즉 6.3 의
    /// <c>name</c>·<c>location</c>·<c>hardware</c>·<c>device_id</c> 4키가 <b>제거·개명</b>됐다
    /// (<c>_legacy.py:61-64</c>: <c>device_id</c>→<c>serial</c>, <c>hardware</c>→<c>hardware_rev</c>,
    /// <c>name</c>→<c>name_device</c>, <c>location</c>→<c>geolocation.location</c>).
    /// <para>이 값은 부모 장비 DTO 의 <c>ShouldSerializeHardwareSpec()</c> 이 직렬화 직전에 전파하거나,
    /// <c>PatchHardwareSpecAsync</c> 처럼 단독 전송하는 경우 서비스가 직접 설정한다.</para>
    /// </remarks>
    [JsonIgnore]
    public bool UseAxisWrite { get; set; }

    /// <summary>
    /// 7.0 <c>serial</c> — 6.3 <c>device_id</c> 의 개명. 빈 문자열은 생략(7.0 은 빈 문자열을 거부한다).
    /// </summary>
    /// <remarks>
    /// <b>쓰기는 <see cref="DeviceId"/> 에서 유도하고, 읽기는 거기로 되돌린다</b>(A-devices D-24) —
    /// 7.0+ 응답에는 <c>device_id</c> 가 없으므로 setter 가 없으면 일련번호가 화면에서 사라진다.
    /// </remarks>
    [JsonProperty("serial", Order = 20, NullValueHandling = NullValueHandling.Ignore)]
    public string? SerialAxis
    {
        get => DeviceAxisWrite.NullIfEmpty(DeviceId);
        set { if (DeviceAxisWrite.NullIfEmpty(value) is { } v) DeviceId = v; }
    }

    public bool ShouldSerializeSerialAxis() => UseAxisWrite && SerialAxis != null;

    /// <summary>7.0 <c>hardware_rev</c> — 6.3 <c>hardware</c> 의 개명(읽기는 역투영).</summary>
    [JsonProperty("hardware_rev", Order = 21, NullValueHandling = NullValueHandling.Ignore)]
    public string? HardwareRevAxis
    {
        get => DeviceAxisWrite.NullIfEmpty(Hardware);
        set { if (DeviceAxisWrite.NullIfEmpty(value) is { } v) Hardware = v; }
    }

    public bool ShouldSerializeHardwareRevAxis() => UseAxisWrite && HardwareRevAxis != null;

    /// <summary>
    /// 7.0 <c>schema</c> — 스키마 버전. <b>1 만 받는다</b>(다른 값 422). 미설정이면 생략하고 서버 기본값(1)에 맡긴다.
    /// </summary>
    [JsonProperty("schema", Order = 22, NullValueHandling = NullValueHandling.Ignore)]
    public int? Schema { get; set; }

    public bool ShouldSerializeSchema() => UseAxisWrite && Schema.HasValue;

    /// <summary>
    /// 7.0 <c>spec</c> — <b>표준 키 밖의 모든 것이 들어가는 유일한 자리</b>(벤더 확장·정적 제원).
    /// </summary>
    /// <remarks>
    /// 서버 원문: "표준 키 밖은 전부 <c>spec</c> 안이다. 최상위에 벤더 키를 흩뿌리면 다음 장비에서
    /// 같은 이름이 다른 뜻으로 쓰이고, 그때는 이미 고칠 수 없다."
    /// 해상도·이미지센서·렌즈 초점거리 같은 값이 여기 온다.
    /// </remarks>
    [JsonProperty("spec", Order = 23, NullValueHandling = NullValueHandling.Ignore)]
    public JObject? Spec { get; set; }

    public bool ShouldSerializeSpec() => UseAxisWrite && Spec != null;

    /// <summary>
    /// 7.0 <c>components[]</c> — 이 장비에 달린 부품 선언(형상 축).
    /// </summary>
    /// <remarks>
    /// <para><b>부품 선언은 상태·설정의 전제다</b> — <c>device_status.components.&lt;key&gt;</c> 보고와
    /// <c>device_config.component_overrides.&lt;key&gt;</c> 설정은 여기 선언된 key 만 받는다(선언 없으면 둘 다 422).
    /// 그래서 함체 히터·팬 설정과 문 위치 읽기가 모두 이 배열에 매여 있다.</para>
    /// <para><b>빈 배열은 "부품 없음"이 아니라 "형상 미입력"</b>이고, <c>PUT</c> 은 보낸 축을 통째 교체하므로
    /// 모를 때는 반드시 <c>null</c>(미전송)로 둔다 — 빈 배열을 보내면 서버의 부품 선언이 지워진다.</para>
    /// <para>어안·열화상이 여기로 왔다 — 옛 <c>hardware_spec.lens</c>·<c>imaging</c> 은 <b>7.0 에서 422</b> 이고
    /// 대신 <c>OPTICAL_LENS</c>(<c>spec.shape="FISHEYE"</c>) · <c>THERMAL_CAMERA</c>·<c>EO_CAMERA</c> 부품이다
    /// (명세 §4 <c>EnumCameraType</c> 주의문 · <c>_legacy.py:66-67</c>).</para>
    /// </remarks>
    [JsonProperty("components", Order = 24, NullValueHandling = NullValueHandling.Ignore)]
    public List<ComponentDefinitionDto>? Components { get; set; }

    public bool ShouldSerializeComponents() => UseAxisWrite && Components != null;

    /// <summary>7.0+ <c>firmware</c> 상한(50자)을 넘는가 — 넘으면 축 모드 쓰기가 422 다(절단하지 않는다).</summary>
    [JsonIgnore]
    public bool IsFirmwareOverAxisLimit => (Firmware?.Length ?? 0) > AXIS_FIRMWARE_MAX_LENGTH;

    /// <summary>7.0+ <c>HardwareSpec.firmware</c> maxLength(실측 8.0.1). 6.3 은 100 이었다.</summary>
    public const int AXIS_FIRMWARE_MAX_LENGTH = 50;

    /// <summary>주어진 유형의 부품 선언을 찾는다(대소문자 무시). 없으면 <c>null</c>.</summary>
    public ComponentDefinitionDto? FindComponent(string componentType)
    {
        if (Components == null) return null;
        foreach (var c in Components)
        {
            if (c != null && string.Equals(c.Type, componentType, StringComparison.OrdinalIgnoreCase))
                return c;
        }
        return null;
    }

    // ── 7.0 에서 제거된 키 ──
    public bool ShouldSerializeName() => !UseAxisWrite;
    public bool ShouldSerializeLocation() => !UseAxisWrite;
    public bool ShouldSerializeHardware() => !UseAxisWrite;
    public bool ShouldSerializeDeviceId() => !UseAxisWrite;

    // ── 양쪽 공통 키: 축 모드에서는 빈 문자열을 보내지 않는다 ──
    // (7.0 LegacyFieldRejectMixin 이 빈 문자열을 422 로 거부한다 — 적용 깊이는 미확인이라 보수적으로 막는다)
    public bool ShouldSerializeManufacturer() => !UseAxisWrite || DeviceAxisWrite.NullIfEmpty(Manufacturer) != null;
    public bool ShouldSerializeModel() => !UseAxisWrite || DeviceAxisWrite.NullIfEmpty(Model) != null;
    public bool ShouldSerializeFirmware() => !UseAxisWrite || DeviceAxisWrite.NullIfEmpty(Firmware) != null;
    public bool ShouldSerializeMacAddress() => !UseAxisWrite || DeviceAxisWrite.NullIfEmpty(MacAddress) != null;
    public bool ShouldSerializeOnvifVersion() => !UseAxisWrite || DeviceAxisWrite.NullIfEmpty(OnvifVersion) != null;
    #endregion
}
