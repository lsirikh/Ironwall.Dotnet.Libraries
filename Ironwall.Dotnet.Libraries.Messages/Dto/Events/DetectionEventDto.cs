using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 침입 탐지 이벤트 DTO (Nested device 구조)
/// </summary>
public class DetectionEventDto : BaseDto, IDeviceEventDto, IActionReportableEventDto
{
    /// <summary>
    /// 이벤트 타입 (EnumEventType: "Intrusion")
    /// </summary>
    [JsonProperty("type_event", Order = 2)]
    public string TypeEvent { get; set; } = "Intrusion";

    /// <summary>
    /// 장치 ID (Create/Update 시 사용, FK → Device)
    /// </summary>
    [JsonProperty("device_id", Order = 3, DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int DeviceId { get; set; }

    /// <summary>
    /// 중첩 Device 객체 (서버 응답에서 반환, 공통 Base 타입)
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>판본마다 모양이 다르다</b>(배포 스웨거 + 실응답 실측 2026-09-18) —
    /// 어느 쪽이 와도 <see cref="BaseDeviceDto"/> 하나로 받는다(관용 수용, 판본 분기 없음):</para>
    /// <list type="bullet">
    ///   <item><b>운영 6.3.2</b> — <b>전문</b>(<c>*NestedResponse</c>):
    ///     <c>{id, number_device, group_device, name_device, type_device, version, status,
    ///     is_enable, controller_id, geolocation, device_groups}</c>.
    ///     <c>type_device</c> 는 <b>필수 키</b>이고 값은 문자열 이름(<c>"SmartSensor2"</c>·<c>"Controller"</c>).</item>
    ///   <item><b>개발 8.0.1</b> — <b>참조 프로필</b>(<c>DeviceReference</c>):
    ///     <c>{id, category_device}</c> <b>두 키뿐</b>이다. 종류축(<c>type_device</c>)이 <b>없고</b>
    ///     카테고리(7값 소문자 <c>controller·sensor·camera·speaker·enclosure·lamp·gate</c>)만 온다.
    ///     상세는 캐시 또는 <c>GET /api/devices/{category_device}s/{id}</c>(설계 §6 · D5).</item>
    /// </list>
    /// <para>장비가 삭제되면 두 판본 모두 <c>null</c> 이고, 목록 축약 응답에는 키 자체가 없을 수 있다 —
    /// 소비자는 <c>null</c> 안전해야 한다.</para>
    /// <para>장비 종류 복원은 <c>Events.Ui/Helpers/DtoToModelHelper.ResolveDeviceType</c> 이
    /// <c>type_device</c> → <c>category_device</c> 순으로 처리한다. 둘 다 없으면 '센서'가 아니라
    /// <b>'알 수 없음'</b> 이다(F-03).</para>
    /// </remarks>
    [JsonProperty("device", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public BaseDeviceDto? Device { get; set; }

    /// <summary>
    /// <b>REST 쓰기 경로에서만</b> <c>device</c> 를 본문에서 뺀다. 기본 <c>false</c>(= 싣는다).
    /// <para>⚠ <b>플래그인 이유</b> — <c>ShouldSerializeDevice() =&gt; false</c> 로 영구히 끄면
    /// <b>NATS 브로커 본문까지 깨진다</b>. ACTION_REPORT 발행이 같은 DTO 를 태우는데
    /// GIS.md v1.5 는 <c>from_event.device</c> 의 <c>device_groups</c>·<c>geolocation</c>·
    /// <c>status</c>·<c>version</c>·<c>controller_id</c> 를 <b>요구</b>한다(회귀 테스트 2건이 잡았다).</para>
    /// <para>레포 선례와 같은 형태다 — <c>BaseDeviceDto.UseAxisWrite</c> 를 API 서비스가 켠다.</para>
    /// </summary>
    [JsonIgnore]
    public bool SuppressDeviceOnRestWrite { get; set; }

    /// <summary>
    /// <b>REST 요청에만</b> <c>device</c> 를 빼고 <b>NATS 발행·읽기에는 그대로 싣는다</b>.
    /// <para><b>왜</b> — <c>device</c> 는 세 판본 모두 <b>응답 전용 키</b>다(8.0.1 은 보내면
    /// <c>warnings[].READ_ONLY_IGNORED</c>). 그런데 7.0+ 의 <c>raise_empty_strings</c> 가 <b>중첩까지</b> 훑어
    /// <c>device.name_device</c> 등의 <c>""</c> 를 <b>무시보다 먼저</b> 422 <c>EMPTY_STRING</c> 으로 끊는다 —
    /// 탐지·장애·연결 생성이 전부 실패했다.</para>
    /// <para><b>무회귀</b> — 6.3.2 POST 스키마엔 <c>device</c> 가 아예 없고(extra=ignore),
    /// 6.3.2 PATCH 는 <c>additionalProperties:false</c> 라 오히려 빼야 통과한다.</para>
    /// </summary>
    public bool ShouldSerializeDevice() => !SuppressDeviceOnRestWrite;

    /// <summary>
    /// 장비 설명 문자열
    /// </summary>
    [JsonProperty("device_description", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public string? DeviceDescription { get; set; }

    /// <summary>
    /// 조치 여부 (EnumTrueFalse: "True", "False") - 서버가 자동 관리
    /// </summary>
    [JsonProperty("action_reported", Order = 6, NullValueHandling = NullValueHandling.Ignore)]
    public string? ActionReported { get; set; }

    /// <summary>
    /// 탐지 결과 (EnumDetectionType: "THERMAL_SENSOR", "PIR_SENSOR", etc.)
    /// </summary>
    [JsonProperty("result", Order = 7)]
    public string Result { get; set; } = string.Empty;

    /// <summary>
    /// 탐지 상세 정보 (JSONB)
    /// </summary>
    [JsonProperty("detail", Order = 8, NullValueHandling = NullValueHandling.Ignore)]
    public DetectionDetailDto? Detail { get; set; }

    /// <summary>
    /// 이벤트 카테고리 판별자 — <c>detection</c>/<c>malfunction</c>/<c>connection</c>/<c>operation</c>.
    /// <b>응답 전용</b>(쓰기 스키마에 없다).
    /// </summary>
    [JsonProperty("category_event", NullValueHandling = NullValueHandling.Ignore)]
    public string? CategoryEvent { get; set; }

    /// <summary>요청에는 싣지 않는다 — 응답 전용 키다.</summary>
    public bool ShouldSerializeCategoryEvent() => false;

    /// <summary>
    /// 소속 부대(API v8.0). <b>응답 전용</b> — 쓰기 스키마에 없어 보내면 <c>422 extra_forbidden</c>.
    /// </summary>
    [JsonProperty("unit_id", NullValueHandling = NullValueHandling.Ignore)]
    public int? UnitId { get; set; }

    /// <summary>요청에는 싣지 않는다 — 쓰기 스키마에 없는 키다.</summary>
    public bool ShouldSerializeUnitId() => false;

}
