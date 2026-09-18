using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Bases;

/// <summary>
/// 서버 오류 봉투의 <c>error.details[]</c> 한 항목 — v7.0 계약은 <b>4키</b> <c>{field, code, moved_to?, message}</c>.
/// <para>
/// 지금까지 이 배열은 <see cref="Defines.Apis.ApiError.DetailsToken"/> 안의 날 JSON 으로만 존재했고,
/// 소비처가 <c>ToString(Formatting.None)</c> 문자열을 운영자 팝업에 그대로 실었다
/// (예: <c>[{"field":"query.status","code":"VALUE_NOT_ALLOWED","message":"…"}]</c>).
/// 사람이 읽는 문구는 <see cref="Helpers.ApiErrorTextHelper"/>, 기계 분기는 <see cref="Code"/>·<see cref="MovedTo"/> 로 한다.
/// </para>
/// <para>
/// <see cref="Code"/> 는 서버 <b>닫힌 9종</b>: <c>REMOVED_FIELD</c>·<c>UNKNOWN_FIELD</c>·<c>OBSERVED_FIELD</c>·
/// <c>IMMUTABLE_FIELD</c>·<c>MISSING_FIELD</c>·<c>NULL_NOT_ALLOWED</c>·<c>EMPTY_STRING</c>·
/// <c>VALUE_NOT_ALLOWED</c>·<c>CONSTRAINT</c>. 어휘 밖 값이 와도 문자열로 그대로 받는다(거부하지 않는다).
/// </para>
/// </summary>
public class ApiFieldErrorDto
{
    /// <summary>위반한 실제 경로. 본문은 <c>connection.ip_port</c> 처럼 점 경로, 쿼리는 <c>query.{이름}</c>, 특정 불가면 <c>unknown</c>.</summary>
    [JsonProperty("field", Order = 1)]
    public string? Field { get; set; }

    /// <summary>details 하위 코드(닫힌 9종). 기계 분기용.</summary>
    [JsonProperty("code", Order = 2)]
    public string? Code { get; set; }

    /// <summary>대체 안내(제거된 필드의 이사 경로). 대체가 없으면 null.</summary>
    [JsonProperty("moved_to", Order = 3)]
    public string? MovedTo { get; set; }

    /// <summary>사람이 읽는 설명 — 운영자 화면에 그대로 보여도 되는 문장(서버 계약 S-7).</summary>
    [JsonProperty("message", Order = 4)]
    public string? Message { get; set; }
}
