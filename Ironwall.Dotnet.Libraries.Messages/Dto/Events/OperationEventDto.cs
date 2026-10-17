using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 운영 이벤트 DTO (API 6.3 후속 <c>operation</c> 카테고리 신설 · 조회 전용).
/// <para>서버 <c>OperationEventResponse</c> 12키와 정합. 운영 카테고리의 <c>type_event</c> 는
/// <c>Operation</c> 하나뿐이다(어휘 정본 <c>ALLOWED_TYPE_EVENT_BY_CATEGORY</c>).</para>
/// <para>⚠ <b>생성(POST) 경로를 두지 않는다</b> — 서버가 매니저 보고로 스스로 적재하므로
/// 클라가 또 보내면 같은 사건이 2건이 된다.</para>
/// </summary>
public class OperationEventDto : BaseDto, IDeviceEventDto, IActionReportableEventDto
{
    /// <summary>이벤트 타입. 운영 카테고리는 <c>Operation</c> 고정.</summary>
    [JsonProperty("type_event", Order = 2)]
    public string TypeEvent { get; set; } = "Operation";

    /// <summary>
    /// 장치 ID. ⚠ 서버 <c>OperationEventResponse</c> 에는 이 키가 <b>없다</b>(중첩 <c>device</c> 만 온다) —
    /// <see cref="IDeviceEventDto"/> 계약을 만족시키기 위한 파생값이라 직렬화하지 않는다.
    /// </summary>
    [JsonIgnore]
    public int DeviceId
    {
        get => Device?.Id ?? 0;
        set { /* 서버가 주지 않는 키다. 설정은 무시한다. */ }
    }

    /// <summary>중첩 Device 객체(응답 전용).</summary>
    /// <remarks>
    /// 판본별 모양·종류축 복원 규칙은 <see cref="DetectionEventDto.Device"/> remarks 를 본다 —
    /// 운영 6.3.2 는 <b>전문</b>(<c>type_device</c> 필수), 개발 8.0.1 은 <b>참조</b>
    /// <c>{id, category_device}</c> 두 키뿐이다(실측 2026-09-18).
    /// </remarks>
    [JsonProperty("device", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public BaseDeviceDto? Device { get; set; }

    /// <summary>
    /// <b>REST 쓰기 경로에서만</b> <c>device</c> 를 본문에서 뺀다. 기본 <c>false</c>(= 싣는다).
    /// <para>⚠ <b>플래그인 이유</b> — <c>ShouldSerializeDevice() =&gt; false</c> 로 영구히 끄면
    /// <b>NATS 브로커 본문까지 깨진다</b>(ACTION_REPORT 가 같은 DTO 를 태운다).</para>
    /// </summary>
    [JsonIgnore]
    public bool SuppressDeviceOnRestWrite { get; set; }

    /// <summary><b>REST 요청에만</b> <c>device</c> 를 빼고 NATS 발행·읽기에는 그대로 싣는다.</summary>
    public bool ShouldSerializeDevice() => !SuppressDeviceOnRestWrite;

    [JsonProperty("device_description", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public string? DeviceDescription { get; set; }

    /// <summary>
    /// 조치 여부. ⚠ 서버 판본마다 타입이 다르다(6.3 문자열 / 7.0+ boolean) —
    /// Newtonsoft 가 boolean 을 <b>소문자 "true"/"false"</b> 문자열로 넘기므로
    /// <c>== "True"</c> 같은 동등비교를 쓰면 안 된다. <c>bool.TryParse</c> 로 판정한다.
    /// </summary>
    [JsonProperty("action_reported", Order = 6, NullValueHandling = NullValueHandling.Ignore)]
    public string? ActionReported { get; set; }

    /// <summary>
    /// 발생 사유. 서버 <c>EnumOperationType</c> 어휘(<c>ENCLOSURE_DOOR_OPEN</c>·<c>GATE_OPEN</c> 등 11값) —
    /// <b>문자열로 수용</b>하고 미지 값은 그대로 보존한다.
    /// </summary>
    [JsonProperty("reason", Order = 7)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>심각도(<c>INFO</c>/<c>WARNING</c>/<c>CRITICAL</c>). 미지 값 폴백을 위해 문자열로 수용.</summary>
    [JsonProperty("severity", Order = 8, NullValueHandling = NullValueHandling.Ignore)]
    public string? Severity { get; set; }

    /// <summary>
    /// 상세(JSONB). <b>사유마다 키가 다르다</b> — 강타입 DTO 를 두지 않고 <see cref="JObject"/> 로 보존한다.
    /// <para>실측(로컬 8.0.1, 2026-09-18): 통문 개폐 =
    /// <c>{"previous":"OPEN","gate_status":"CLOSED","device_status":"ACTIVATED"}</c>.
    /// 임계치는 서버 문서상 <c>{field, value, threshold, direction, metric_id}</c> 계열(미실측).
    /// ⚠ 키 이름을 코드에 하드코딩할 때는 <b>반드시 실측으로 확인</b>한다 — 문서와 배포가 어긋난 자리다.</para>
    /// </summary>
    [JsonProperty("detail", Order = 10, NullValueHandling = NullValueHandling.Ignore)]
    public JObject? Detail { get; set; }

    /// <summary>
    /// 이벤트 카테고리 판별자. 운영 이벤트는 <c>operation</c> 고정(서버 required).
    /// <b>응답 전용</b>이라 요청에는 싣지 않는다.
    /// </summary>
    [JsonProperty("category_event", Order = 11, NullValueHandling = NullValueHandling.Ignore)]
    public string? CategoryEvent { get; set; }

    /// <summary>요청에는 싣지 않는다 — 응답 전용 키다.</summary>
    public bool ShouldSerializeCategoryEvent() => false;

    /// <summary>
    /// 소속 부대(API v8.0, 서버 required). <b>응답 전용</b> — 쓰기 스키마에 없어 보내면 <c>422 extra_forbidden</c>.
    /// </summary>
    [JsonProperty("unit_id", Order = 12, NullValueHandling = NullValueHandling.Ignore)]
    public int? UnitId { get; set; }

    /// <summary>요청에는 싣지 않는다 — 쓰기 스키마에 없는 키다.</summary>
    public bool ShouldSerializeUnitId() => false;
}
