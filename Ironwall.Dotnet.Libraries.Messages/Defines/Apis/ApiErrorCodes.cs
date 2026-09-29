namespace Ironwall.Dotnet.Libraries.Messages.Defines.Apis;

/// <summary>
/// 서버 오류 봉투 <c>error.code</c> 의 <b>닫힌 19종</b>(명세 §12.2) — 문자열 리터럴을 흩뿌리지 않기 위한 단일 정본.
/// <para>
/// 배포 실측(2026-09-18, 로컬 개발 <c>8.0.1</c>): Swagger <c>ApiErrorResponse.error.code.enum</c> 이 16개였고,
/// <b>서버 v8.0.4 에서 19종</b>이 됐다(412 <see cref="PreconditionFailed"/> · 412 <see cref="VersionConflict"/> · 428 <see cref="PreconditionRequired"/> —
/// 회신 2026-09-29 §3, 8.0.4 검증 컨테이너 openapi 실측). 서버는 이 밖의 코드를 내지 않는다.
/// </para>
/// <para>
/// <see cref="ApiError.Code"/> 는 enum 이 아니라 <b>문자열</b>이다 — 서버가 어휘를 늘려도 역직렬화가 실패하지 않는다(모르는 값은 일반 사유로).
/// </para>
/// <para>
/// ⚠ <b>같은 상태에 두 코드가 오는 자리가 셋</b> 있다(명세 §12.2) — 한쪽만 처리하면 안 된다.
/// <list type="bullet">
///   <item>401 = <see cref="Unauthorized"/>(재인증) · <see cref="SessionRevoked"/>(토큰 폐기 → 재로그인)</item>
///   <item>410 = <see cref="EndpointRemoved"/>(제거된 경로 = 클라가 구버전) · <see cref="Gone"/>(보고서 PDF 소실)</item>
///   <item>412 = <see cref="VersionConflict"/>(부대 배치 — 지금 나가는 코드) · <see cref="PreconditionFailed"/>(매핑 폴백). 분기는 상태(412)로 한다</item>
/// </list>
/// </para>
/// <para>
/// ⚠ <b>서버가 내지 않는 코드</b>(명세 §12.2 "코드가 내지 않는 것"): <c>UNPROCESSABLE_ENTITY</c> ·
/// <c>DB_ERROR</c> · <c>TIMEOUT</c> · 400 의 <c>VALIDATION_ERROR</c>.
/// 종전 <see cref="Helpers.ApiMessageHelper"/> 폴백이 <c>UNPROCESSABLE_ENTITY</c>·<c>INTERNAL_SERVER_ERROR</c> 를
/// <b>조어</b>해 어휘를 이탈했다 — 이제 아래 상수만 쓴다.
/// </para>
/// </summary>
public static class ApiErrorCodes
{
    #region - 서버 닫힌 19종 (명세 §12.2 · v8.0.4) -
    /// <summary>400 — 라우터가 판정한 잘못된 요청 · 본문 바이트가 UTF-8 디코딩 실패(v8.0). <b>JSON 문법만 깨진 경우는 422</b>.</summary>
    public const string BadRequest = "BAD_REQUEST";

    /// <summary>401 — 토큰 없음·만료.</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>401 — 강제로그아웃·블랙리스트·중복로그인으로 폐기된 토큰. 403(권한부족)과 구분. 클라는 즉시 재로그인.</summary>
    public const string SessionRevoked = "SESSION_REVOKED";

    /// <summary>403 — 경로의 <c>module:verb</c> 권한 없음.</summary>
    public const string Forbidden = "FORBIDDEN";

    /// <summary>404 — 리소스·경로 없음.</summary>
    public const string NotFound = "NOT_FOUND";

    /// <summary>405 — 경로는 있으나 메서드 미지원.</summary>
    public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";

    /// <summary>409 — 중복 생성 · 참조가 남은 리소스 삭제 · 이미 확인된 시스템 이벤트 재확인.</summary>
    public const string Conflict = "CONFLICT";

    /// <summary>410 — <b>v7.0 에서 제거된 경로</b>(묘비 11경로). <c>error.message</c> 가 새 자리를 지목한다.</summary>
    public const string EndpointRemoved = "ENDPOINT_REMOVED";

    /// <summary>410 — 리소스는 있으나 실체가 사라짐(보고서 PDF 소실). 사유는 <c>error.details.error_code</c>.</summary>
    public const string Gone = "GONE";

    /// <summary>412 — 전제 조건 불일치(매핑 폴백). v8.0.4 현재 나가는 자리는 없고 <see cref="VersionConflict"/> 가 대신 나간다.</summary>
    public const string PreconditionFailed = "PRECONDITION_FAILED";

    /// <summary>412 — 문서 판이 바뀌었다(<c>PATCH /api/units/layout</c> 의 <c>If-Match</c> ≠ 현재 판). <c>error.details.current_version</c> · 응답 헤더 <c>ETag</c>. 쓰지 않았다(v8.0.4).</summary>
    public const string VersionConflict = "VERSION_CONFLICT";

    /// <summary>413 — 업로드가 <c>THUMBNAIL_MAX_BYTES</c>(기본 10MB) 초과.</summary>
    public const string PayloadTooLarge = "PAYLOAD_TOO_LARGE";

    /// <summary>422 — 요청 검증 실패 · 라우터 판정 규칙 위반. 세부는 <c>details[].code</c>(<see cref="ApiFieldErrorCodes"/>).</summary>
    public const string ValidationError = "VALIDATION_ERROR";

    /// <summary>428 — 전제 조건 필요(<c>PATCH /api/units/layout</c> 에 <c>If-Match</c> 없음, v8.0.4).</summary>
    public const string PreconditionRequired = "PRECONDITION_REQUIRED";

    /// <summary>429 — 로그인 실패 누적 IP 제한(<c>Retry-After</c> 헤더).</summary>
    public const string TooManyRequests = "TOO_MANY_REQUESTS";

    /// <summary>500 — 서버 내부 오류. <c>error.message</c> 는 고정 문구다(내부 정보 미노출).</summary>
    public const string InternalError = "INTERNAL_ERROR";

    /// <summary>502 — 게이트웨이 오류. 앞단 프록시가 내면 <b>봉투가 아닐 수 있다</b>.</summary>
    public const string BadGateway = "BAD_GATEWAY";

    /// <summary>503 — 점검·과부하. <c>/health</c>·<c>/api/tracking/health</c> 의 503 은 봉투가 아니라 평문이다.</summary>
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";

    /// <summary>매핑 밖 상태의 폴백. 서버 도달 0 이지만 이름이 공표돼 있어 클라가 "모르는 코드"로 분기할 수 있다.</summary>
    public const string UnknownError = "UNKNOWN_ERROR";
    #endregion

    #region - 클라 로컬 의사(擬似) 코드 — 서버 어휘가 아니다 -
    /// <summary>
    /// <b>클라 로컬</b> — 응답 본문을 역직렬화하지 못했다. 서버 어휘가 아니다.
    /// </summary>
    public const string ParseError = "PARSE_ERROR";

    /// <summary>
    /// <b>클라 로컬</b> — 우리 쪽 요청 타임아웃. <see cref="System.Net.HttpStatusCode.GatewayTimeout"/>(504)는
    /// 서버가 아니라 <c>ApiService.BuildExceptionResponse</c> 가 <see cref="System.Threading.Tasks.TaskCanceledException"/> 에
    /// 붙이는 합성 상태다.
    /// <para>⚠ 서버 닫힌 19종에 <b>없는</b> 코드다. 그래도 유지하는 이유:
    /// 로그인 패널이 이 코드로 "요청 시간이 초과되었습니다."를 띄운다
    /// (<c>Accounts.Ui\ViewModels\Panels\LoginPanelViewModel.cs</c>의 사유 매핑).
    /// 지우면 타임아웃이 "아이디 또는 비밀번호가 일치하지 않습니다."로 오표시된다.</para>
    /// </summary>
    public const string ClientTimeout = "GATEWAY_TIMEOUT";
    #endregion

    #region - 안정 sub-code (error.details.error_code) -
    /// <summary>410 <see cref="Gone"/> 의 사유 — 보고서 레코드는 있으나 PDF 파일이 저장소에서 사라졌다(명세 §10.4.4).</summary>
    public const string PdfFileMissing = "PDF_FILE_MISSING";
    #endregion
}

/// <summary>
/// <c>error.details[].code</c> 의 <b>닫힌 9종</b>(명세 §12.1.1) — 422 의 기계 분기 어휘.
/// <para>배포 8.0.1 Swagger <c>ValidationFieldError.code.enum</c> 과 일치(실측 2026-09-18).</para>
/// <para>스키마 별칭 2개는 서버가 접어서 보낸다 — <c>AXIS_NULL</c> → <see cref="NullNotAllowed"/>,
/// <c>INVALID_QUERY</c> → <see cref="ValueNotAllowed"/>.</para>
/// </summary>
public static class ApiFieldErrorCodes
{
    /// <summary>없어진 키·쿼리. <b>이 코드에만</b> <c>moved_to</c> 가 함께 온다(값이 <c>null</c> 이면 대체 자리가 없다는 뜻).</summary>
    public const string RemovedField = "REMOVED_FIELD";

    /// <summary>이 요청이 받지 않는 키(오타 포함).</summary>
    public const string UnknownField = "UNKNOWN_FIELD";

    /// <summary>관측값이라 운영자가 쓸 수 없는 필드 — 문구가 정본 입구를 지목한다.</summary>
    public const string ObservedField = "OBSERVED_FIELD";

    /// <summary>등록 뒤 바꿀 수 없는 필드.</summary>
    public const string ImmutableField = "IMMUTABLE_FIELD";

    /// <summary>필수 키 누락.</summary>
    public const string MissingField = "MISSING_FIELD";

    /// <summary><c>null</c> 을 받지 않는 자리(축 전체를 <c>null</c> 로 지우려는 경우 포함).</summary>
    public const string NullNotAllowed = "NULL_NOT_ALLOWED";

    /// <summary><c>""</c> 은 값이 아니다 — 값이 없으면 <b>키를 빼라</b>(지우려면 <c>null</c>).</summary>
    public const string EmptyString = "EMPTY_STRING";

    /// <summary>어휘 밖 값(enum · <c>?view=</c> · <c>?include=</c> 토큰 등).</summary>
    public const string ValueNotAllowed = "VALUE_NOT_ALLOWED";

    /// <summary>그 밖의 규칙 위반(범위·길이·두 필드 관계·존재하지 않는 참조).</summary>
    public const string Constraint = "CONSTRAINT";
}

/// <summary>
/// 성공 응답 <c>warnings[].code</c> 의 <b>닫힌 4종</b>(명세 §3.2·§12.1.2).
/// <para>배포 8.0.1 Swagger <c>ResponseWarning.code.enum</c> 과 일치(실측 2026-09-18).</para>
/// <para>⚠ <b>모르는 코드는 무시한다</b> — 경고는 거부가 아니다(명세 §12.1.2).</para>
/// </summary>
public static class ApiWarningCodes
{
    /// <summary>응답 전용 키를 요청에 넣어 무시했다. 닫힌 목록은 리소스마다 다르고, <b>목록 밖 키는 422 <c>UNKNOWN_FIELD</c></b> 다.</summary>
    public const string ReadOnlyIgnored = "READ_ONLY_IGNORED";

    /// <summary>관측 시각이 없어 <b>수신 시각</b>을 관측 시각으로 저장했다.</summary>
    public const string ObservedAtDefaulted = "OBSERVED_AT_DEFAULTED";

    /// <summary>어느 부품 메트릭에도 걸리지 않는 임계치를 설정했다(저장은 한다).</summary>
    public const string UnmatchedThreshold = "UNMATCHED_THRESHOLD";

    /// <summary><c>connection.type=SERVER_MANAGED</c> 스피커인데 <c>server_id</c> 가 없다(기한부 예외 E-40 — 한 대라도 등록되면 422).</summary>
    public const string ServerIdMissing = "SERVER_ID_MISSING";
}
