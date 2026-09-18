using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Defines.Apis;

/// <summary>
/// 목록 데이터 API 응답 래퍼 (페이지네이션 포함)
/// <para>
/// ⚠ <b>목록 총계의 자리가 경로마다 다르다</b>(명세 §3.2 · 실측 2026-09-18 로컬 <c>8.0.1</c>):
/// <list type="bullet">
///   <item>표준 = <c>pagination:{page,limit,total,total_pages}</c></item>
///   <item><c>GET /api/grants</c> = <c>pagination</c> <b>와</b> 레거시 최상위 <c>total</c> 을 <b>둘 다</b> 싣는다</item>
///   <item>목록 봉투를 그대로 쓰는 단건·동작 경로는 <c>"pagination": null</c> — <b>null 이면 무시</b>한다</item>
/// </list>
/// 총계가 <b>어디에도 없으면</b> 페이지 루프가 끝을 알 수 없어 목록이 <b>조용히 절단</b>된다 —
/// 그 상태를 감지하는 수단이 <see cref="IsTotalMissing"/>·<see cref="HasMorePages"/> 다.
/// </para>
/// </summary>
/// <typeparam name="T">목록 항목 타입</typeparam>
public class ApiListResponse<T>
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
    /// 응답 데이터 배열 (성공 시)
    /// </summary>
    [JsonProperty("data", Order = 3)]
    public List<T>? Data { get; set; }

    /// <summary>
    /// 페이지네이션 정보
    /// </summary>
    [JsonProperty("pagination", Order = 4)]
    public PaginationDto? Pagination { get; set; }

    /// <summary>
    /// 서버 top-level 전체 건수 (pagination 객체 없이 total 만 주는 엔드포인트용, 예: GET /grants). null=미제공.
    /// <para>(F-1) 서버가 <c>{success,data,total}</c> 형태로 total 을 최상위에 반환하는 경우 이 필드로 수신한다.
    /// pagination 객체를 쓰는 엔드포인트(events 등)는 이 값이 null 이고 <see cref="Pagination"/> 를 사용한다.</para>
    /// </summary>
    [JsonProperty("total", Order = 7)]
    public int? Total { get; set; }

    /// <summary>
    /// 에러 정보 (실패 시)
    /// </summary>
    [JsonProperty("error", Order = 5)]
    public ApiError? Error { get; set; }

    /// <summary>
    /// 메타데이터 (타임스탬프, 요청 ID 등)
    /// </summary>
    [JsonProperty("meta", Order = 6)]
    public MetaDto Meta { get; set; } = new MetaDto();

    /// <summary>
    /// 거부하지 않았지만 알려야 하는 경고 — 봉투 <b>최상위</b> <c>warnings[]</c>(명세 §3.2·§12.1.2, v7.0 신규).
    /// <para>장비·서버·이벤트의 <b>쓰기·보고</b> 응답에만 실린다(그 밖 경로는 키째 없어 <c>null</c>).</para>
    /// <para>⚠ <b>모르는 <c>code</c> 는 무시</b>한다 — 경고는 거부가 아니다.</para>
    /// </summary>
    [JsonProperty("warnings", Order = 8)]
    public List<ResponseWarningDto>? Warnings { get; set; }

    /// <summary>
    /// HTTP 상태 코드 (클라이언트 진단용 — 서버 직렬화/역직렬화 대상 아님)
    /// <para>⚠ <b>성공 응답에도 반드시 채운다</b> — 202(§3.3)·409(§12.2) 분기의 유일한 근거다.</para>
    /// </summary>
    [JsonIgnore]
    public int StatusCode { get; set; }

    #region - 총계·절단 감지 (§3.2) -
    /// <summary>
    /// 실효 총계 — <c>pagination.total</c> 우선, 없으면 레거시 최상위 <c>total</c>. 양쪽 다 없으면 <c>null</c>.
    /// </summary>
    [JsonIgnore]
    public int? EffectiveTotal => Pagination?.Total ?? Total;

    /// <summary>서버가 총계를 실었는가.</summary>
    [JsonIgnore]
    public bool HasTotal => EffectiveTotal.HasValue;

    /// <summary>
    /// <b>총계가 없어 절단을 감지할 수 없는 상태</b> — 성공했는데 <c>pagination</c>·<c>total</c> 둘 다 없고
    /// 데이터가 1건 이상인 경우다. 이때 페이지 루프는 "다음 페이지가 있는지"를 알 수 없어
    /// 첫 페이지만 싣고 끝나거나(조용한 절단) 빈 페이지를 볼 때까지 도는 수밖에 없다.
    /// <para>호출부는 이 값이 <c>true</c> 면 <b>경고 로그</b>를 남기고 "빈 페이지까지 순회" 전략으로 내려가야 한다.</para>
    /// </summary>
    [JsonIgnore]
    public bool IsTotalMissing => Success && !HasTotal && (Data?.Count ?? 0) > 0;

    /// <summary>
    /// 다음 페이지가 있는가. 판정 불가(<c>pagination</c> 없음)면 <c>null</c> —
    /// <c>false</c> 와 구별해야 조용한 절단을 오탐/미탐하지 않는다.
    /// </summary>
    [JsonIgnore]
    public bool? HasMorePages
    {
        get
        {
            var p = Pagination;
            if (p == null || p.Limit <= 0 || p.Page <= 0) return null;
            return (long)p.Page * p.Limit < p.Total;
        }
    }
    #endregion

    /// <summary>
    /// 성공 응답 생성
    /// </summary>
    public static ApiListResponse<T> CreateSuccess(List<T> data, PaginationDto? pagination = null, string? message = null)
    {
        var count = data?.Count ?? 0;
        var msg = message ?? $"{count} items retrieved";

        return new ApiListResponse<T>
        {
            Success = true,
            Message = msg,
            Data = data,
            Pagination = pagination,
            Meta = new MetaDto()
        };
    }

    /// <summary>
    /// 에러 응답 생성
    /// </summary>
    public static ApiListResponse<T> CreateError(string code, string message, string? details = null)
    {
        return new ApiListResponse<T>
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
