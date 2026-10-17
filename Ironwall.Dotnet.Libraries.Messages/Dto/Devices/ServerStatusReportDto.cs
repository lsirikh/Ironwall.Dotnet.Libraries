using Newtonsoft.Json;



namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;



/// <summary>

/// 서버 상태 보고 DTO — <c>PATCH /api/servers/{id}/status</c> (§8.3, API 7.0+, 권한 <c>servers:control</c>).

/// <para><b>왜 별도 DTO 인가</b>: <c>status</c> 는 <b>관측 필드</b>다. <c>PATCH /api/servers/{id}</c> 본문에

/// <c>status</c> 를 실으면 서버가 422(<c>OBSERVED_FIELD</c>)로 거부한다. 상태를 갱신할 유일한 입구가 이 경로이고,

/// 배포 스키마 <c>ServerStatusReport</c> 는 <c>additionalProperties: false</c> 라

/// <c>id</c>·<c>created_at</c> 같은 키가 섞이면 즉시 422 다 — 그래서 <c>BaseDto</c> 를 상속하지 않는다.</para>

/// <para>배포 스키마(7.0.1 실측): <c>required = ["status"]</c>, properties = <c>status</c> · <c>observed_at</c>.

/// 6.3.2 에는 이 경로가 <b>존재하지 않는다</b>.</para>

/// </summary>

public class ServerStatusReportDto

{

    /// <summary>

    /// 관측 상태. <c>NORMAL</c> · <c>WARNING</c> · <c>ERROR</c> 만 유효하다.

    /// <para>⚠ <c>UNKNOWN</c> 은 <b>보고할 수 없다</b> — 서버가 422 로 거부한다(관측되지 않은 상태는 보고 대상이 아니다).

    /// 응답에서는 <c>UNKNOWN</c> 이 올 수 있다.</para>

    /// </summary>

    [JsonProperty("status", Order = 1)]

    public string Status { get; set; } = string.Empty;



    /// <summary>

    /// 관측 시각. <b>UTC 오프셋이 반드시 포함된 ISO8601</b>(예: <c>2026-09-18T10:30:00+09:00</c>) —

    /// 오프셋이 없으면 서버가 <c>CONSTRAINT</c> 422 로 거부한다.

    /// <para>생략(<c>null</c>) 시 서버가 수신 시각으로 채우고 응답 <c>warnings[]</c> 에

    /// <c>OBSERVED_AT_DEFAULTED</c> 를 싣는다(우리 봉투에 <c>warnings</c> 가 없어 현재는 못 읽는다 — S-20).</para>

    /// </summary>

    [JsonProperty("observed_at", Order = 2, NullValueHandling = NullValueHandling.Ignore)]

    public string? ObservedAt { get; set; }



    /// <summary>

    /// 상태가 마지막으로 <b>변한</b> 시각(응답 전용). 같은 값을 재보고하면 서버는 아무것도 쓰지 않고

    /// 이 값도 바뀌지 않는다.

    /// </summary>

    [JsonProperty("status_observed_at", Order = 3, NullValueHandling = NullValueHandling.Ignore)]

    public string? StatusObservedAt { get; set; }

}

