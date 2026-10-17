using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;

namespace Ironwall.Dotnet.Libraries.Messages.Helpers;

/// <summary>
/// 응답 <b>상태·코드 분기</b>의 단일 지점(명세 §3.3 상태코드 · §12.2 에러코드 · §12.1.1 details 코드).
/// <para>
/// <b>왜 한 곳인가</b> — 호출부가 <c>(int)res.StatusCode == 409</c> 같은 숫자 비교를 흩뿌리면
/// ① 서버가 같은 상태에 <b>두 코드</b>를 보내는 자리(401·410)를 반쪽만 처리하고
/// ② 봉투가 없는 응답(프록시 502·우리 쪽 타임아웃)에서 코드가 비어 분기가 조용히 무너진다.
/// 실측 사고: 로그인 패널이 <c>CONFLICT</c> 를 <c>_ =&gt;</c> 로 흘려
/// "아이디 또는 비밀번호가 일치하지 않습니다."로 <b>오표시</b>했다.
/// </para>
/// <para><b>판정 규칙</b> — HTTP 상태와 <c>error.code</c> 를 <b>OR</b> 로 본다.
/// 봉투가 있으면 코드가, 없으면 상태가 근거가 되도록 한쪽만 맞아도 성립시킨다.
/// 표시 문구는 각 도메인의 몫이고 여기서는 <b>판정</b>만 한다.</para>
/// </summary>
public static class ApiStatusHelper
{
    #region - 핵심(코어) 판정 -
    /// <summary><paramref name="error"/> 의 코드가 <paramref name="code"/> 인가(대소문자 무시).</summary>
    public static bool HasCode(ApiError? error, string code)
        => error != null && !string.IsNullOrWhiteSpace(error.Code)
           && string.Equals(error.Code.Trim(), code, StringComparison.OrdinalIgnoreCase);

    /// <summary>상태코드 또는 <c>error.code</c> 중 하나라도 맞으면 성립.</summary>
    public static bool Is(int statusCode, ApiError? error, int httpStatus, string code)
        => statusCode == httpStatus || HasCode(error, code);

    /// <summary>
    /// <c>202 Accepted</c> — 비동기 접수(보고서 생성) 또는 <b>억제창에 걸린 이벤트 생성</b>(§3.3·§6.8.10).
    /// <para>⚠ <c>201</c>(생성됨)과 <b>반드시 갈라야</b> 한다 — 202 는 리소스가 만들어지지 <b>않았다</b>.</para>
    /// </summary>
    public static bool IsAccepted(int statusCode) => statusCode == 202;

    /// <summary><c>409 CONFLICT</c> — 중복 생성 · 참조가 남은 리소스 삭제 · 이미 확인된 이벤트 재확인.</summary>
    public static bool IsConflict(int statusCode, ApiError? error)
        => Is(statusCode, error, 409, ApiErrorCodes.Conflict);

    /// <summary>
    /// <c>410</c> 중 <b>제거된 경로</b>(<c>ENDPOINT_REMOVED</c>) — <b>클라가 구버전</b>이라는 뜻이다.
    /// 리소스가 지워진 것이 아니므로 사용자에게 "삭제됨"으로 알리면 오진이다.
    /// <c>error.message</c> 가 새 자리를 지목하므로 <b>개발자 경보</b>로 남겨야 한다.
    /// </summary>
    public static bool IsEndpointRemoved(int statusCode, ApiError? error)
        => HasCode(error, ApiErrorCodes.EndpointRemoved)
           || (statusCode == 410 && !HasCode(error, ApiErrorCodes.Gone));

    /// <summary><c>410</c> 중 <b>실체 소실</b>(<c>GONE</c>) — 보고서 PDF 소실. 사유는 <c>error.details.error_code</c>.</summary>
    public static bool IsGone(int statusCode, ApiError? error)
        => HasCode(error, ApiErrorCodes.Gone);

    /// <summary><c>422 VALIDATION_ERROR</c> — 요청 검증 실패 또는 라우터 판정 규칙 위반.</summary>
    public static bool IsValidationError(int statusCode, ApiError? error)
        => Is(statusCode, error, 422, ApiErrorCodes.ValidationError);

    /// <summary>
    /// <c>400 BAD_REQUEST</c> — 라우터가 판정한 잘못된 요청 · 본문 바이트 UTF-8 디코딩 실패(v8.0).
    /// <para>⚠ <b>422 와 다르다</b> — JSON <b>문법</b>만 깨진 경우와 본문·쿼리 <b>검증</b> 실패는 422 다(§3.3).
    /// 우리는 요청 본문을 늘 <c>Encoding.UTF8</c> 로 만들므로 ②(디코딩 실패) 400 은 발생하지 않는다.</para>
    /// </summary>
    public static bool IsBadRequest(int statusCode, ApiError? error)
        => Is(statusCode, error, 400, ApiErrorCodes.BadRequest);

    /// <summary><c>401 UNAUTHORIZED</c> — 재인증하면 된다.</summary>
    public static bool IsUnauthorized(int statusCode, ApiError? error)
        => HasCode(error, ApiErrorCodes.Unauthorized)
           || (statusCode == 401 && !HasCode(error, ApiErrorCodes.SessionRevoked));

    /// <summary><c>401 SESSION_REVOKED</c> — 토큰이 폐기됐다. <b>토큰을 버리고 재로그인</b>해야 한다(재시도 무의미).</summary>
    public static bool IsSessionRevoked(int statusCode, ApiError? error)
        => HasCode(error, ApiErrorCodes.SessionRevoked);

    /// <summary><c>403 FORBIDDEN</c> — 권한 없음.</summary>
    public static bool IsForbidden(int statusCode, ApiError? error)
        => Is(statusCode, error, 403, ApiErrorCodes.Forbidden);

    /// <summary><c>404 NOT_FOUND</c>.</summary>
    public static bool IsNotFound(int statusCode, ApiError? error)
        => Is(statusCode, error, 404, ApiErrorCodes.NotFound);

    /// <summary><c>405 METHOD_NOT_ALLOWED</c>.</summary>
    public static bool IsMethodNotAllowed(int statusCode, ApiError? error)
        => Is(statusCode, error, 405, ApiErrorCodes.MethodNotAllowed);

    /// <summary><c>413 PAYLOAD_TOO_LARGE</c> — 업로드 크기 초과.</summary>
    public static bool IsPayloadTooLarge(int statusCode, ApiError? error)
        => Is(statusCode, error, 413, ApiErrorCodes.PayloadTooLarge);

    /// <summary><c>429 TOO_MANY_REQUESTS</c> — <c>Retry-After</c> 를 존중해야 한다.</summary>
    public static bool IsTooManyRequests(int statusCode, ApiError? error)
        => Is(statusCode, error, 429, ApiErrorCodes.TooManyRequests);

    /// <summary><c>503</c>/<c>502</c> — 서버·게이트웨이가 응답하지 못했다(재시도 가치 있음).</summary>
    public static bool IsServerUnavailable(int statusCode, ApiError? error)
        => statusCode == 502 || statusCode == 503
           || HasCode(error, ApiErrorCodes.BadGateway) || HasCode(error, ApiErrorCodes.ServiceUnavailable);

    /// <summary>
    /// <b>우리 쪽</b> 요청 타임아웃(합성 <c>504</c> — <c>ApiService.BuildExceptionResponse</c>).
    /// 서버 어휘가 아니라 클라 로컬 코드다(<see cref="ApiErrorCodes.ClientTimeout"/>).
    /// </summary>
    public static bool IsTimeout(int statusCode, ApiError? error)
        => statusCode == 504 || HasCode(error, ApiErrorCodes.ClientTimeout);
    #endregion

    #region - details[] 기계 분기 (§12.1.1) -
    /// <summary><c>error.details[]</c> 에 주어진 코드(닫힌 9종)가 하나라도 있는가.</summary>
    public static bool HasFieldCode(ApiError? error, string code)
    {
        if (error is null || string.IsNullOrWhiteSpace(code)) return false;
        foreach (var fe in error.FieldErrors)
            if (string.Equals(fe.Code?.Trim(), code, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// 서버가 알려 준 <b>이사 안내</b> 첫 건(<c>REMOVED_FIELD</c> 의 <c>moved_to</c>). 없으면 <c>null</c>.
    /// <para><paramref name="field"/> 를 주면 그 경로(<c>query.include_sensors</c> 등)의 안내만 찾는다.</para>
    /// <para>⚠ <c>REMOVED_FIELD</c> 인데 <c>moved_to</c> 가 <c>null</c> 이면 <b>대체 자리가 없다</b>는 뜻이다 —
    /// "안내가 없다"와 구별해야 한다(<see cref="HasRemovedField"/> 로 먼저 확인).</para>
    /// </summary>
    public static string? MovedTo(ApiError? error, string? field = null)
    {
        if (error is null) return null;
        foreach (var fe in error.FieldErrors)
        {
            if (!string.Equals(fe.Code?.Trim(), ApiFieldErrorCodes.RemovedField, StringComparison.OrdinalIgnoreCase))
                continue;
            if (field != null && !string.Equals(fe.Field?.Trim(), field, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrWhiteSpace(fe.MovedTo)) return fe.MovedTo!.Trim();
        }
        return null;
    }

    /// <summary>제거된 키·쿼리를 보냈는가(<c>REMOVED_FIELD</c>) — <b>우리 코드가 구버전</b>이라는 신호다.</summary>
    public static bool HasRemovedField(ApiError? error)
        => HasFieldCode(error, ApiFieldErrorCodes.RemovedField);

    /// <summary>검증 오류 전건(없으면 빈 목록). 기계 분기는 <c>code</c>·<c>field</c>·<c>moved_to</c> 로 한다.</summary>
    public static IReadOnlyList<ApiFieldErrorDto> FieldErrors(ApiError? error)
        => error?.FieldErrors ?? Array.Empty<ApiFieldErrorDto>();
    #endregion

    #region - warnings[] 조회 (§12.1.2) -
    /// <summary>주어진 경고 코드가 실렸는가. <c>warnings</c> 가 없으면 <c>false</c>.</summary>
    public static bool HasWarning(IReadOnlyList<ResponseWarningDto>? warnings, string code)
    {
        if (warnings == null || string.IsNullOrWhiteSpace(code)) return false;
        foreach (var w in warnings)
            if (string.Equals(w.Code?.Trim(), code, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>경고를 사람이 읽는 여러 줄로. 없으면 <c>null</c>(운영자 안내 본문에 쓴다).</summary>
    public static string? WarningText(IReadOnlyList<ResponseWarningDto>? warnings)
    {
        if (warnings == null || warnings.Count == 0) return null;

        var lines = new List<string>();
        foreach (var w in warnings)
        {
            var text = string.IsNullOrWhiteSpace(w.Message) ? null : w.Message!.Trim();
            if (text is null)
            {
                var field = string.IsNullOrWhiteSpace(w.Field) ? null : w.Field!.Trim();
                var code = string.IsNullOrWhiteSpace(w.Code) ? null : w.Code!.Trim();
                if (field is null && code is null) continue;
                text = field is null ? code : (code is null ? field : $"{field}: {code}");
            }
            lines.Add(text!);
        }

        return lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
    }
    #endregion

    #region - 확장(호출부 표준 진입점) — 단건 -
    /// <summary>202 접수(억제·비동기). 201 과 갈라야 하는 자리에서 쓴다.</summary>
    public static bool IsAccepted<T>(this ApiResponse<T>? res)
        => res != null && (IsAccepted(res.StatusCode) || res.Suppressed == true);

    /// <summary>409 충돌.</summary>
    public static bool IsConflict<T>(this ApiResponse<T>? res)
        => res != null && IsConflict(res.StatusCode, res.Error);

    /// <summary>410 — 제거된 경로(클라 구버전).</summary>
    public static bool IsEndpointRemoved<T>(this ApiResponse<T>? res)
        => res != null && IsEndpointRemoved(res.StatusCode, res.Error);

    /// <summary>410 — 실체 소실(보고서 PDF 등).</summary>
    public static bool IsGone<T>(this ApiResponse<T>? res)
        => res != null && IsGone(res.StatusCode, res.Error);

    /// <summary>422 검증 실패.</summary>
    public static bool IsValidationError<T>(this ApiResponse<T>? res)
        => res != null && IsValidationError(res.StatusCode, res.Error);

    /// <summary>400 잘못된 요청(422 와 구분).</summary>
    public static bool IsBadRequest<T>(this ApiResponse<T>? res)
        => res != null && IsBadRequest(res.StatusCode, res.Error);

    /// <summary>401 — 재인증.</summary>
    public static bool IsUnauthorized<T>(this ApiResponse<T>? res)
        => res != null && IsUnauthorized(res.StatusCode, res.Error);

    /// <summary>401 — 세션 폐기(토큰 버리고 재로그인).</summary>
    public static bool IsSessionRevoked<T>(this ApiResponse<T>? res)
        => res != null && IsSessionRevoked(res.StatusCode, res.Error);

    /// <summary>403 권한 없음.</summary>
    public static bool IsForbidden<T>(this ApiResponse<T>? res)
        => res != null && IsForbidden(res.StatusCode, res.Error);

    /// <summary>404 없음.</summary>
    public static bool IsNotFound<T>(this ApiResponse<T>? res)
        => res != null && IsNotFound(res.StatusCode, res.Error);

    /// <summary>413 업로드 크기 초과.</summary>
    public static bool IsPayloadTooLarge<T>(this ApiResponse<T>? res)
        => res != null && IsPayloadTooLarge(res.StatusCode, res.Error);

    /// <summary>429 요청 과다.</summary>
    public static bool IsTooManyRequests<T>(this ApiResponse<T>? res)
        => res != null && IsTooManyRequests(res.StatusCode, res.Error);

    /// <summary>우리 쪽 타임아웃(합성 504).</summary>
    public static bool IsTimeout<T>(this ApiResponse<T>? res)
        => res != null && IsTimeout(res.StatusCode, res.Error);

    /// <summary><c>details[]</c> 에 해당 코드가 있는가.</summary>
    public static bool HasFieldCode<T>(this ApiResponse<T>? res, string code)
        => HasFieldCode(res?.Error, code);

    /// <summary>서버가 알려 준 이사 안내(<c>moved_to</c>).</summary>
    public static string? MovedTo<T>(this ApiResponse<T>? res, string? field = null)
        => MovedTo(res?.Error, field);

    /// <summary>경고 코드 보유 여부.</summary>
    public static bool HasWarning<T>(this ApiResponse<T>? res, string code)
        => HasWarning(res?.Warnings, code);

    /// <summary>경고 문구(여러 줄). 없으면 <c>null</c>.</summary>
    public static string? WarningText<T>(this ApiResponse<T>? res)
        => WarningText(res?.Warnings);
    #endregion

    #region - 확장(호출부 표준 진입점) — 목록 -
    /// <summary>202 접수.</summary>
    public static bool IsAccepted<T>(this ApiListResponse<T>? res)
        => res != null && IsAccepted(res.StatusCode);

    /// <summary>409 충돌.</summary>
    public static bool IsConflict<T>(this ApiListResponse<T>? res)
        => res != null && IsConflict(res.StatusCode, res.Error);

    /// <summary>410 — 제거된 경로(클라 구버전).</summary>
    public static bool IsEndpointRemoved<T>(this ApiListResponse<T>? res)
        => res != null && IsEndpointRemoved(res.StatusCode, res.Error);

    /// <summary>410 — 실체 소실.</summary>
    public static bool IsGone<T>(this ApiListResponse<T>? res)
        => res != null && IsGone(res.StatusCode, res.Error);

    /// <summary>422 검증 실패.</summary>
    public static bool IsValidationError<T>(this ApiListResponse<T>? res)
        => res != null && IsValidationError(res.StatusCode, res.Error);

    /// <summary>400 잘못된 요청(422 와 구분).</summary>
    public static bool IsBadRequest<T>(this ApiListResponse<T>? res)
        => res != null && IsBadRequest(res.StatusCode, res.Error);

    /// <summary>401 — 재인증.</summary>
    public static bool IsUnauthorized<T>(this ApiListResponse<T>? res)
        => res != null && IsUnauthorized(res.StatusCode, res.Error);

    /// <summary>401 — 세션 폐기.</summary>
    public static bool IsSessionRevoked<T>(this ApiListResponse<T>? res)
        => res != null && IsSessionRevoked(res.StatusCode, res.Error);

    /// <summary>403 권한 없음.</summary>
    public static bool IsForbidden<T>(this ApiListResponse<T>? res)
        => res != null && IsForbidden(res.StatusCode, res.Error);

    /// <summary>404 없음.</summary>
    public static bool IsNotFound<T>(this ApiListResponse<T>? res)
        => res != null && IsNotFound(res.StatusCode, res.Error);

    /// <summary>429 요청 과다.</summary>
    public static bool IsTooManyRequests<T>(this ApiListResponse<T>? res)
        => res != null && IsTooManyRequests(res.StatusCode, res.Error);

    /// <summary>우리 쪽 타임아웃(합성 504).</summary>
    public static bool IsTimeout<T>(this ApiListResponse<T>? res)
        => res != null && IsTimeout(res.StatusCode, res.Error);

    /// <summary><c>details[]</c> 에 해당 코드가 있는가.</summary>
    public static bool HasFieldCode<T>(this ApiListResponse<T>? res, string code)
        => HasFieldCode(res?.Error, code);

    /// <summary>서버가 알려 준 이사 안내(<c>moved_to</c>).</summary>
    public static string? MovedTo<T>(this ApiListResponse<T>? res, string? field = null)
        => MovedTo(res?.Error, field);

    /// <summary>경고 코드 보유 여부.</summary>
    public static bool HasWarning<T>(this ApiListResponse<T>? res, string code)
        => HasWarning(res?.Warnings, code);

    /// <summary>경고 문구(여러 줄). 없으면 <c>null</c>.</summary>
    public static string? WarningText<T>(this ApiListResponse<T>? res)
        => WarningText(res?.Warnings);
    #endregion
}
