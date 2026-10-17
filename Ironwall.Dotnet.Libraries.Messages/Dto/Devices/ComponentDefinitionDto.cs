using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
/****************************************************************************
   Purpose      : 서버 7.0+ hardware_spec.components[] 부품 선언 DTO (A-devices D-22)
   Created By   : GHLee
   Created On   : 9/18/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 서버 7.0 <c>ComponentDefinition</c> — <c>hardware_spec.components[]</c> 한 항목.
/// <b>"이 장비에 무엇이 달려 있나"(형상)</b> 를 선언한다.
/// </summary>
/// <remarks>
/// <para>실측(배포 Swagger 8.0.1): <c>required=[key, type]</c> · <c>additionalProperties=false</c>.
/// <c>key</c> 는 장비 내 고유 식별자이고 <b>형상↔상태↔설정을 잇는 축</b>이다 —
/// <c>device_status.components.&lt;key&gt;</c> 와 <c>device_config.component_overrides.&lt;key&gt;</c> 가
/// 이 키를 참조한다. 선언하지 않은 key 로 보고·설정하면 <b>둘 다 422</b>
/// (명세 §5.3.3 "선언 없는 key 는 보고도 component_overrides 도 둘 다 422").</para>
/// <para><c>type</c> 은 카탈로그(<c>GET /api/devices/spec</c> 의 <c>component_type[].code</c>) 어휘다 —
/// 우리는 <see cref="ComponentTypeNames"/> 에 <b>이 프로젝트가 실제로 읽고 쓰는 유형만</b> 상수로 둔다.
/// 어휘 정본은 서버 카탈로그이므로 임의 값을 만들지 않는다.</para>
/// <para><b>빈 배열은 "부품 없음"이 아니라 "형상 미입력"</b>이다(서버 설명 원문). 그래서
/// <c>components</c> 를 모르면 <c>null</c> 로 두고 <b>빈 배열을 보내지 않는다</b> —
/// <c>PUT</c> 은 "보낸 축은 통째 교체"라 빈 배열이 서버의 부품 선언을 지운다(명세 §5.5.5).</para>
/// </remarks>
public class ComponentDefinitionDto
{
    /// <summary>장비 내 고유 식별자(필수) — 예 <c>door</c>·<c>heater</c>·<c>fan</c>·<c>actuator</c>·<c>lens</c>.</summary>
    [JsonProperty("key", Order = 1)]
    public string Key { get; set; } = string.Empty;

    /// <summary>부품 유형(필수) — 카탈로그 <c>component_type.code</c>(대문자). <see cref="ComponentTypeNames"/>.</summary>
    [JsonProperty("type", Order = 2)]
    public string Type { get; set; } = string.Empty;

    /// <summary>이 장비에서만 다르게 부르는 이름(≤100). 카탈로그 이름과 같으면 보내지 않는다.</summary>
    [JsonProperty("label", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public string? Label { get; set; }

    /// <summary>달렸으나 사용 안 함 표시. 서버 기본 <c>true</c> 이고 <b><c>false</c> 일 때만</b> 저장·응답된다.</summary>
    [JsonProperty("in_service", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public bool? InService { get; set; }

    /// <summary>접점·버스 채널 번호(0 이상).</summary>
    [JsonProperty("channel", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public int? Channel { get; set; }

    /// <summary>장비 내 물리 위치(≤100) — 예 <c>전면</c>.</summary>
    [JsonProperty("position", Order = 6, NullValueHandling = NullValueHandling.Ignore)]
    public string? Position { get; set; }

    [JsonProperty("manufacturer", Order = 7, NullValueHandling = NullValueHandling.Ignore)]
    public string? Manufacturer { get; set; }

    [JsonProperty("model", Order = 8, NullValueHandling = NullValueHandling.Ignore)]
    public string? Model { get; set; }

    [JsonProperty("serial", Order = 9, NullValueHandling = NullValueHandling.Ignore)]
    public string? Serial { get; set; }

    /// <summary>부품 펌웨어(≤50) — 장비 <c>hardware_spec.firmware</c> 와 같은 상한이다.</summary>
    [JsonProperty("firmware", Order = 10, NullValueHandling = NullValueHandling.Ignore)]
    public string? Firmware { get; set; }

    [JsonProperty("hardware_rev", Order = 11, NullValueHandling = NullValueHandling.Ignore)]
    public string? HardwareRev { get; set; }

    /// <summary>설치 시각 — ISO 8601, <b>오프셋 필수</b>.</summary>
    [JsonProperty("installed_at", Order = 12, NullValueHandling = NullValueHandling.Ignore)]
    public string? InstalledAt { get; set; }

    /// <summary>교체 시각 — ISO 8601, <b>오프셋 필수</b>.</summary>
    [JsonProperty("replaced_at", Order = 13, NullValueHandling = NullValueHandling.Ignore)]
    public string? ReplacedAt { get; set; }

    /// <summary>
    /// 정적 제원(참고값)·벤더 확장만. <b>측정값(거리·좌표·속도·진동 크기)은 여기가 아니라 탐지 이벤트 <c>detail</c></b> 이다.
    /// </summary>
    /// <remarks>
    /// 카메라의 어안·열화상 표현이 여기 온다 — 명세 §4 <c>EnumCameraType</c> 주의문의
    /// <c>{"key":"lens","type":"OPTICAL_LENS","spec":{"shape":"FISHEYE"}}</c> 예시가 정본이다.
    /// </remarks>
    [JsonProperty("spec", Order = 14, NullValueHandling = NullValueHandling.Ignore)]
    public JObject? Spec { get; set; }
}

/// <summary>
/// 서버 카탈로그 <c>component_type.code</c> 중 <b>이 라이브러리가 실제로 읽고 쓰는 유형</b>만 모은 상수.
/// </summary>
/// <remarks>
/// 어휘 정본은 <c>GET /api/devices/spec</c> 이다(A-devices D-32 — 클라이언트 미구현).
/// 여기에 없는 유형이 필요하면 카탈로그를 먼저 확인하고 추가한다. 추측으로 새 코드를 만들지 않는다.
/// </remarks>
public static class ComponentTypeNames
{
    /// <summary>함체 문 위치 — 옛 <c>enclosures.door_status</c>. 상태 어휘 <c>OPEN</c>·<c>CLOSED</c>.</summary>
    public const string DoorSensor = "DOOR_SENSOR";

    /// <summary>통문 문 구동부 — 옛 <c>gates.gate_status</c>. 상태 어휘 <c>OPEN</c>·<c>CLOSED</c>·<c>RUNNING</c>.</summary>
    public const string DoorActuator = "DOOR_ACTUATOR";

    public const string Heater = "HEATER";
    public const string Fan = "FAN";

    /// <summary>어안 렌즈를 <c>spec.shape="FISHEYE"</c> 로 담는 자리(옛 <c>hardware_spec.lens</c>).</summary>
    public const string OpticalLens = "OPTICAL_LENS";

    /// <summary>열화상 카메라(옛 <c>hardware_spec.imaging</c>).</summary>
    public const string ThermalCamera = "THERMAL_CAMERA";

    /// <summary>EO(주야간 영상) 카메라(옛 <c>hardware_spec.imaging</c>).</summary>
    public const string EoCamera = "EO_CAMERA";
}

/// <summary>부품 건강 상태 — 서버 <c>EnumComponentHealth</c>(실측 4값).</summary>
public static class ComponentHealthNames
{
    public const string Ok = "OK";
    public const string Degraded = "DEGRADED";
    public const string Fault = "FAULT";
    public const string Unknown = "UNKNOWN";
}
