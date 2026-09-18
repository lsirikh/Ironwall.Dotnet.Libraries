using System.Globalization;
using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 함체 메트릭 시계열 데이터 DTO (§5.5.9~5.5.11)
/// <para>temperature/humidity/current/voltage는 서버가 string으로 반환</para>
/// </summary>
public class EnclosureMetricDto : BaseDto
{
    [JsonProperty("enclosure_id", Order = 2)]
    public int EnclosureId { get; set; }

    [JsonProperty("temperature", Order = 3)]
    public string? Temperature { get; set; }

    [JsonProperty("humidity", Order = 4)]
    public string? Humidity { get; set; }

    [JsonProperty("current", Order = 5)]
    public string? Current { get; set; }

    [JsonProperty("voltage", Order = 6)]
    public string? Voltage { get; set; }

    [JsonProperty("vibration", Order = 7)]
    public int? Vibration { get; set; }

    [JsonProperty("ups_battery_level", Order = 8)]
    public int? UpsBatteryLevel { get; set; }

    [JsonProperty("ups_charging", Order = 9)]
    public bool? UpsCharging { get; set; }

    [JsonProperty("detail", Order = 10)]
    public JObject? Detail { get; set; }

    /// <summary>
    /// 관측 시각(오프셋 포함 ISO8601) — 7.0 <c>EnclosureMetricCreate.observed_at</c>.
    /// 생략하면 서버가 <b>수신 시각</b>으로 채우고 <c>OBSERVED_AT_DEFAULTED</c> 경고를 준다(A-devices D-36).
    /// </summary>
    /// <remarks>6.3.2 <c>EnclosureMetricCreate</c> 에는 이 키가 <b>없다</b>(실측) — 축 모드에서만 전송한다.</remarks>
    [JsonProperty("observed_at", Order = 11, NullValueHandling = NullValueHandling.Ignore)]
    public string? ObservedAt { get; set; }

    #region - 서버 계약 세대 분기 (FR-09 · A-devices D-23) -
    /// <summary>
    /// 7.0 이상의 <c>EnclosureMetricCreate</c> 스키마로 직렬화할지 여부. <c>false</c>(기본)면 6.3 본문 그대로.
    /// </summary>
    /// <remarks>
    /// 7.0 <c>EnclosureMetricCreate.additionalProperties=false</c> 이고 properties 에
    /// <c>id</c>·<c>created_at</c>·<c>updated_at</c>·<c>enclosure_id</c> 가 <b>없다</b> →
    /// 계측 보고가 최소 2키 위반으로 422 다(<c>created_at</c> 은 <c>BaseDto</c> 생성자가 현재 KST 로
    /// 채워 두기 때문에 <c>NullValueHandling.Ignore</c> 로도 막히지 않는다).
    /// 함체 id 는 <b>경로</b>가 정한다.
    /// </remarks>
    [JsonIgnore]
    public bool UseAxisWrite { get; set; }

    /// <summary>경로가 정하는 값 — 7.0 쓰기 스키마에 없어 실으면 422.</summary>
    public bool ShouldSerializeEnclosureId() => !UseAxisWrite;

    /// <summary>6.3 에는 없는 키.</summary>
    public bool ShouldSerializeObservedAt() => UseAxisWrite && ObservedAt != null;

    /// <summary>
    /// 읽기전용 키(<c>created_at</c>·<c>updated_at</c>)를 축 모드에서 비운다.
    /// </summary>
    /// <remarks>
    /// <b>왜 <c>ShouldSerializeCreatedAt()</c> 이 아니라 값 비우기인가</b> — Newtonsoft 는
    /// <c>ShouldSerializeXxx</c> 를 그 속성의 <b>선언 타입</b>에서만 찾는다. <c>created_at</c>·
    /// <c>updated_at</c> 은 <c>BaseDto</c> 가 선언하므로 파생 클래스에 메서드를 놔도 <b>호출되지 않는다</b>
    /// (실측으로 확인 — 조건 직렬화가 조용히 무시됐다). <c>BaseDto</c> 는 전역 파급이라 손대지 않고
    /// 값 자체를 비운다. <c>created_at</c> 은 <c>BaseDto</c> 생성자가 현재 KST 로 채워 두기 때문에
    /// <c>NullValueHandling.Ignore</c> 만으로는 절대 막히지 않는다.
    /// <para><c>id</c> 는 <c>DefaultValueHandling.Ignore</c> 라 신규 계측(0)에서는 이미 생략된다 —
    /// 응답 DTO 를 재사용해 POST 하면 실릴 수 있으니 그러지 말 것.</para>
    /// </remarks>
    public void ApplyWriteContract(bool useAxisWrite)
    {
        UseAxisWrite = useAxisWrite;
        if (!useAxisWrite) return;
        CreatedAt = null;
        UpdatedAt = null;
    }
    #endregion

    #region - 수치 계측값 (A-devices D-38) -
    /// <summary>
    /// 온도(°C) 수치 — <see cref="Temperature"/> 의 <b>타입 안전한 창구</b>.
    /// </summary>
    /// <remarks>
    /// <para><b>왜 문자열 속성을 남겨 두는가.</b> 서버는 6.3.2 와 8.0.1 <b>양쪽 모두</b>
    /// <c>temperature</c>·<c>humidity</c>·<c>current</c>·<c>voltage</c> 를 <c>number</c> 로 선언한다(실측) —
    /// 우리 <c>string?</c> 이 처음부터 계약과 어긋나 있었다. 그런데 ① 읽기는 Newtonsoft 가
    /// JSON 수치를 문자열 속성으로 변환해 주므로 이미 통하고, ② 쓰기는 서버 pydantic 이
    /// lax 모드(<c>strict</c> 설정 없음)라 <c>"23.5"</c> 를 <c>float</c> 로 받아들여 운영에서 통해 왔다.
    /// 여기서 JSON 타입을 수치로 바꾸면 <b>6.3 요청 본문의 바이트가 달라진다</b> —
    /// 무회귀가 우선이라 전송 형태는 그대로 두고 <b>계산용 창구만</b> 얹었다.</para>
    /// <para>파싱·서식은 <b>InvariantCulture 고정</b>이다. <c>double.Parse(text)</c> 를 호출부에서
    /// 문화권 없이 쓰면 ko-KR 이어도 소수점이 같아 통과하다가, 쉼표를 소수 구분자로 쓰는 문화권에서
    /// <c>23.5</c> 가 <c>235</c> 로 읽힌다 — 임계치 판정이 조용히 뒤집히는 종류의 결함이다.</para>
    /// <para>호출부는 <b>이 속성만</b> 쓰면 판본·로케일과 무관해진다. 문자열 속성은 전송 계약 전용으로 남긴다.</para>
    /// </remarks>
    [JsonIgnore]
    public double? TemperatureValue
    {
        get => ParseNumber(Temperature);
        set => Temperature = FormatNumber(value);
    }

    /// <summary>습도(%) 수치 — <see cref="TemperatureValue"/> 와 같은 계약.</summary>
    [JsonIgnore]
    public double? HumidityValue
    {
        get => ParseNumber(Humidity);
        set => Humidity = FormatNumber(value);
    }

    /// <summary>전류(A) 수치 — <see cref="TemperatureValue"/> 와 같은 계약.</summary>
    [JsonIgnore]
    public double? CurrentValue
    {
        get => ParseNumber(Current);
        set => Current = FormatNumber(value);
    }

    /// <summary>전압(V) 수치 — <see cref="TemperatureValue"/> 와 같은 계약.</summary>
    [JsonIgnore]
    public double? VoltageValue
    {
        get => ParseNumber(Voltage);
        set => Voltage = FormatNumber(value);
    }

    /// <summary>
    /// 관측 시각 — 있으면 <see cref="ObservedAt"/>, 없으면 수신 시각 <see cref="BaseDto.CreatedAt"/>.
    /// </summary>
    /// <remarks>
    /// 6.3.2 <c>EnclosureMetricCreate</c>·응답에는 <c>observed_at</c> 이 <b>없다</b>(실측) —
    /// 그 판에서는 수신 시각이 유일한 시각이었다. 7.0+ 에서 둘이 갈렸으므로 화면은 관측 시각을
    /// 우선하되 없으면 수신 시각으로 낙착한다. <b>둘 다 없으면 <c>null</c></b> 이다.
    /// </remarks>
    [JsonIgnore]
    public string? ObservedAtEffective
        => string.IsNullOrWhiteSpace(ObservedAt) ? CreatedAt : ObservedAt;

    /// <summary>수치 문자열을 InvariantCulture 로만 파싱한다(실패·공백은 <c>null</c>).</summary>
    private static double? ParseNumber(string? text)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    /// <summary>수치를 InvariantCulture 로 직렬화한다(<c>null</c> 은 그대로 <c>null</c>).</summary>
    private static string? FormatNumber(double? value)
        => value?.ToString(CultureInfo.InvariantCulture);
    #endregion
}
