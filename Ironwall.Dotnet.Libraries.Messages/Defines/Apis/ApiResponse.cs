using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Defines.Apis;

/// <summary>
/// 단일 데이터 API 응답 래퍼
/// <para>서버 봉투는 판본·경로마다 키 수가 다르다(명세 §3.2). 그래서 <b>모든 부가 키를 옵션</b>으로 다뤄
/// 2키(<c>{success,data}</c>)·4키·5키·쓰기 봉투(<c>warnings</c> 포함)를 한 타입으로 견딘다.</para>
/// </summary>
/// <typeparam name="T">응답 데이터 타입</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// 요청 성공 여부
    /// </summary>
    [JsonProperty("success", Order = 1)]
    public bool Success { get; set; }

    /// <summary>
    /// 응답 메시지
    /// </summary>
    [JsonProperty("message", Order = 2)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 응답 데이터 (성공 시)
    /// </summary>
    [JsonProperty("data", Order = 3)]
    public T? Data { get; set; }

    /// <summary>
    /// 에러 정보 (실패 시)
    /// </summary>
    [JsonProperty("error", Order = 4)]
    public ApiError? Error { get; set; }

    /// <summary>
    /// 메타데이터 (타임스탬프, 요청 ID 등)
    /// </summary>
    [JsonProperty("meta", Order = 5)]
    public MetaDto Meta { get; set; } = new MetaDto();

    /// <summary>
    /// 거부하지 않았지만 알려야 하는 경고 — 봉투 <b>최상위</b> <c>warnings[]</c>(명세 §3.2·§12.1.2, v7.0 신규).
    /// <para>장비·서버·이벤트의 <b>쓰기·보고</b> 응답에만 실린다(없으면 서버는 <c>[]</c>, 그 밖 경로는 키째 없어 <c>null</c>).
    /// 받을 자리가 없던 동안 <c>MissingMemberHandling.Ignore</c> 가 이 배열을 <b>조용히 버렸다</b>.</para>
    /// <para>⚠ <b>모르는 <c>code</c> 는 무시</b>한다 — 경고는 거부가 아니다.</para>
    /// </summary>
    [JsonProperty("warnings", Order = 6)]
    public List<ResponseWarningDto>? Warnings { get; set; }

    /// <summary>
    /// 억제창에 걸려 <b>생성되지 않았음</b>(명세 §3.3 202 · §6.8.10). 이벤트 생성 <c>POST</c> 전용 키다.
    /// <para>서버는 <c>202</c> + <c>{success, suppressed, message, schedule_id}</c> 만 돌려준다
    /// (<c>data</c>·<c>meta</c> 가 <b>없다</b>). <c>IsSuccessStatusCode</c> 만 보면 <b>201(생성됨)과 구분되지 않는다</b> —
    /// <see cref="StatusCode"/> 가 <c>202</c> 인지, 또는 이 값이 <c>true</c> 인지로 갈라야 한다.</para>
    /// </summary>
    [JsonProperty("suppressed", Order = 7)]
    public bool? Suppressed { get; set; }

    /// <summary>억제한 스케줄 id(202 응답 전용, <c>integer|null</c>). <see cref="Suppressed"/> 와 한 쌍이다.</summary>
    [JsonProperty("schedule_id", Order = 8)]
    public int? ScheduleId { get; set; }

    /// <summary>
    /// HTTP 상태 코드 (클라이언트 진단용 — 서버 직렬화/역직렬화 대상 아님)
    /// <para>⚠ <b>성공 응답에도 반드시 채운다</b> — 201/202 구분(§3.3)과 409 분기(§12.2)의 유일한 근거다.
    /// 종전에는 실패 경로에서만 채워 <c>202</c> 억제 응답이 <c>201</c> 과 똑같이 보였다.</para>
    /// </summary>
    [JsonIgnore]
    public int StatusCode { get; set; }

    /// <summary>
    /// 서버가 준 <c>data</c> 원문(JSON) — 받은 모양 그대로. 채우는 곳은 그것을 <b>그대로 다시 실어야 하는</b> 경로뿐이다
    /// (예: 조치 생성 <c>POST /events/actions</c> 201 → NATS <c>ACTION_REPORT</c> body, 브로커 명세 §6.4 "data 그대로 · 장비 블록 조립 금지").
    /// DTO 를 다시 직렬화하면 모르는 키가 빠지고 <c>ShouldSerialize*</c> 가 키를 지운다 — 그래서 원문을 따로 든다. 비직렬화 대상.
    /// </summary>
    [JsonIgnore]
    public Newtonsoft.Json.Linq.JToken? RawData { get; set; }

    /// <summary>
    /// 성공 응답 생성
    /// </summary>
    public static ApiResponse<T> CreateSuccess(T data, string message = "Operation completed successfully")
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data,
            Meta = new MetaDto()
        };
    }

    /// <summary>
    /// 에러 응답 생성
    /// </summary>
    public static ApiResponse<T> CreateError(string code, string message, string? details = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Error = new ApiError
            {
                Code = code,
                Message = message,
                Details = details
            },
            Meta = new MetaDto()
        };
    }
}
