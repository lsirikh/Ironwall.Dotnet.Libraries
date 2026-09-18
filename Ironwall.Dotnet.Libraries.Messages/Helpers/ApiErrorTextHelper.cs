using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;

namespace Ironwall.Dotnet.Libraries.Messages.Helpers;

/// <summary>
/// 서버 오류 봉투 → <b>사용자에게 보여줄 한 줄 문구</b> 결정의 단일 지점.
/// <para>
/// 왜 한 곳인가: 호출부마다 <c>res.Message ?? "기본문구"</c> 를 흩뿌리면
/// ① <b>빈 문자열은 null 이 아니라서</b> <c>??</c> 가 발동하지 않아 팝업이 <b>설명 없이</b> 뜨고
/// ② 서버가 사유를 <c>error.message</c>·<c>error.details[]</c> 로 옮기면 전 호출부가 동시에 침묵한다.
/// 배포 7.0.1 실측: 400·404 봉투에 <b>top-level <c>message</c> 가 없다</b>
/// (<c>{"success":false,"error":{"code":"BAD_REQUEST","message":"Unknown type 'bogus'…"},"meta":{…}}</c>).
/// </para>
/// <para><b>결정 순서</b>(각 단계는 공백이 아닐 때만 채택 — <see cref="string.IsNullOrWhiteSpace"/> 기준):</para>
/// <list type="number">
///   <item><c>error.details[].message</c> (가장 구체적. 여러 건이면 첫 건 + "외 N건")</item>
///   <item><c>error.message</c> (문자열·객체 양쪽 수용 — <see cref="ApiError.Message"/>)</item>
///   <item>봉투 top-level <c>message</c></item>
///   <item>호출부가 준 기본 문구</item>
/// </list>
/// </summary>
public static class ApiErrorTextHelper
{
    /// <summary>호출부가 기본 문구를 주지 않았을 때의 최후 문구.</summary>
    public const string DefaultFallback = "요청을 처리하지 못했습니다.";

    #region - 핵심 -
    /// <summary>
    /// 오류 문구 결정(핵심 구현). 호출부는 보통 <see cref="ErrorText{T}(ApiResponse{T}, string?)"/> 확장을 쓴다.
    /// </summary>
    /// <param name="error">봉투의 <c>error</c>(없을 수 있다).</param>
    /// <param name="envelopeMessage">봉투 top-level <c>message</c>(배포본 400·404 에는 없다).</param>
    /// <param name="fallback">전부 비었을 때 쓸 기본 문구. 비우면 <see cref="DefaultFallback"/>.</param>
    public static string Resolve(ApiError? error, string? envelopeMessage, string? fallback = null)
    {
        if (error != null)
        {
            var fromDetails = FromFieldErrors(error);
            if (!string.IsNullOrWhiteSpace(fromDetails)) return fromDetails!;

            if (!string.IsNullOrWhiteSpace(error.Message)) return error.Message.Trim();
        }

        if (!string.IsNullOrWhiteSpace(envelopeMessage)) return envelopeMessage!.Trim();

        return string.IsNullOrWhiteSpace(fallback) ? DefaultFallback : fallback!.Trim();
    }

    /// <summary>
    /// <c>error.details[]</c> 를 사람이 읽는 <b>한 줄</b>로 요약한다.
    /// 1건이면 그 문장, 여러 건이면 첫 문장 + <c>" (외 N건)"</c>. 쓸 문장이 없으면 null.
    /// </summary>
    public static string? FromFieldErrors(ApiError? error)
    {
        if (error is null) return null;

        var messages = new List<string>();
        foreach (var fe in error.FieldErrors)
        {
            var line = LineOf(fe);
            if (!string.IsNullOrWhiteSpace(line)) messages.Add(line!);
        }

        if (messages.Count == 0) return null;
        if (messages.Count == 1) return messages[0];
        return $"{messages[0]} (외 {messages.Count - 1}건)";
    }

    /// <summary>
    /// <c>error.details[]</c> 전건을 줄바꿈으로 결합한다(여러 필드가 동시에 틀린 422 안내용).
    /// 쓸 문장이 없으면 null. <b>날 JSON 대신</b> 이 값을 팝업 본문에 싣는다.
    /// </summary>
    public static string? FromFieldErrorsMultiline(ApiError? error)
    {
        if (error is null) return null;

        var messages = new List<string>();
        foreach (var fe in error.FieldErrors)
        {
            var line = LineOf(fe);
            if (!string.IsNullOrWhiteSpace(line)) messages.Add(line!);
        }

        return messages.Count == 0 ? null : string.Join(Environment.NewLine, messages);
    }
    #endregion

    #region - 확장(호출부 표준 진입점) -
    /// <summary>단건 응답의 실패 사유 문구. 예: <c>res.ErrorText("취소하지 못했습니다.")</c></summary>
    public static string ErrorText<T>(this ApiResponse<T>? res, string? fallback = null)
        => res is null ? (string.IsNullOrWhiteSpace(fallback) ? DefaultFallback : fallback!)
                       : Resolve(res.Error, res.Message, fallback);

    /// <summary>목록 응답의 실패 사유 문구.</summary>
    public static string ErrorText<T>(this ApiListResponse<T>? res, string? fallback = null)
        => res is null ? (string.IsNullOrWhiteSpace(fallback) ? DefaultFallback : fallback!)
                       : Resolve(res.Error, res.Message, fallback);

    /// <summary>
    /// 봉투가 아닌 <b>평문 사유</b>(파일 다운로드 결과 등)에 쓰는 최소 규칙 —
    /// <c>?? </c> 가 아니라 <b>공백도 없는 것으로</b> 취급한다. 빈 문자열이 <c>??</c> 를 통과해
    /// 설명 없는 팝업이 뜨던 문제(FR-06 ①)와 같은 원인이다.
    /// </summary>
    public static string Or(string? text, string fallback)
        => string.IsNullOrWhiteSpace(text) ? fallback : text!.Trim();
    #endregion

    #region - 내부 -
    /// <summary>
    /// 검증 오류 1건 → 한 줄. 서버 <c>message</c> 를 그대로 쓰고(계약 S-7),
    /// 대체 안내(<c>moved_to</c>)가 있으면 덧붙인다. <c>message</c> 가 비면 <c>field</c>·<c>code</c> 로 최소 문장을 만든다.
    /// </summary>
    private static string? LineOf(Dto.Bases.ApiFieldErrorDto fe)
    {
        var text = string.IsNullOrWhiteSpace(fe.Message) ? null : fe.Message!.Trim();

        if (text is null)
        {
            var field = string.IsNullOrWhiteSpace(fe.Field) ? null : fe.Field!.Trim();
            var code = string.IsNullOrWhiteSpace(fe.Code) ? null : fe.Code!.Trim();
            if (field is null && code is null) return null;
            text = field is null ? code : (code is null ? field : $"{field}: {code}");
        }

        if (!string.IsNullOrWhiteSpace(fe.MovedTo))
            text = $"{text} (대체: {fe.MovedTo!.Trim()})";

        return text;
    }
    #endregion
}
