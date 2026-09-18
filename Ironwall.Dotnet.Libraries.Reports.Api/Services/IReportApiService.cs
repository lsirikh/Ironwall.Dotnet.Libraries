using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;

namespace Ironwall.Dotnet.Libraries.Reports.Api.Services;

/// <summary>
/// GOP 보고서 API 서비스 — /api/reports/* 14 엔드포인트 래핑.
/// 모든 호출은 Bearer 자동부착(파이프라인). 반환은 ApiResponse/ApiListResponse 봉투(다운로드 제외).
/// </summary>
public interface IReportApiService : IService
{
    // ── 카탈로그 / 상태 ──
    Task<ApiResponse<List<ReportComponentCategoryDto>>> GetComponentsAsync(CancellationToken token = default);
    Task<ApiResponse<ReportStatusDto>> GetStatusAsync(CancellationToken token = default);

    // ── 템플릿 CRUD ──
    Task<ApiListResponse<ReportTemplateDto>> GetTemplatesAsync(int page = 1, int limit = 20, CancellationToken token = default);
    Task<ApiResponse<ReportTemplateDto>> GetTemplateByIdAsync(int id, CancellationToken token = default);
    Task<ApiResponse<ReportTemplateDto>> CreateTemplateAsync(ReportTemplateCreateDto dto, CancellationToken token = default);
    Task<ApiResponse<ReportTemplateDto>> UpdateTemplateAsync(int id, ReportTemplateUpdateDto dto, CancellationToken token = default);
    Task<ApiResponse<object>> DeleteTemplateAsync(int id, CancellationToken token = default);

    // ── 생성 / 이력 ──
    Task<ApiResponse<ReportGenerationDto>> GenerateAsync(ReportGenerateRequestDto dto, CancellationToken token = default);

    /// <summary>
    /// 생성 이력 목록(GET /generations).
    /// <para><paramref name="status"/> 는 <b>닫힌 어휘 5종</b>(<see cref="ReportGenerationStatus"/>)이다 —
    /// null·빈 문자열이면 <b>파라미터를 붙이지 않고</b>(빈 값은 "필터 없음"이 아니라 422),
    /// 소문자는 대문자로 정규화하고, 어휘 밖이면 <b>서버에 보내지 않고</b> <c>VALUE_NOT_ALLOWED</c> 로 실패시킨다.</para>
    /// <para><c>pagination</c>(page·limit·total·total_pages)은 v8.0 배포본에서 <b>실제 총계</b>로 채워진다(실측).
    /// 구 판본은 <c>null</c> 이므로 총계 표시는 null 가드가 필요하다.</para>
    /// </summary>
    Task<ApiListResponse<ReportGenerationDto>> GetGenerationsAsync(int page = 1, int limit = 20, string? status = null, CancellationToken token = default);
    Task<ApiResponse<ReportGenerationDto>> GetGenerationByIdAsync(int id, CancellationToken token = default);

    /// <summary>생성 이력 삭제(DELETE /generations/{id}) — DB row + PDF 파일 정리. reports:delete 필요.</summary>
    Task<ApiResponse<object>> DeleteGenerationAsync(int id, CancellationToken token = default);

    /// <summary>
    /// 생성 취소(POST /generations/{id}/cancel) — PENDING/GENERATING만 CANCELLED. reports:delete 필요.
    /// <para>이미 종결된 생성(COMPLETED/FAILED/CANCELLED)은 <b>400</b>, 없는 id 는 404(§10.4.7).</para>
    /// <para>응답 <c>data.task_cancelled</c> 가 <b>실제로 진행 태스크를 끊었는지</b>를 말한다 —
    /// <c>false</c> 면 DB 마킹만 된 것이므로 "취소했습니다"를 단정하지 말고 상태를 재조회해 안내한다.</para>
    /// </summary>
    Task<ApiResponse<ReportCancelResultDto>> CancelGenerationAsync(int id, CancellationToken token = default);

    /// <summary>
    /// 구조화 미리보기(GET /generations/{id}/preview) — 네이티브 렌더용 JSON.
    /// <para><c>COMPLETED</c> 가 아니면 <b>400</b>, 없는 id 는 404(§10.4.5). 차트 값은 <c>data</c> 중첩 안에 있다.</para>
    /// </summary>
    Task<ApiResponse<ReportPreviewDto>> GetPreviewAsync(int id, CancellationToken token = default);

    /// <summary>미리보기 페이지 원시 HTML(자립형: 인라인 CSS/Chart.js) — WebView2 embed용. reports:view 필요. 실패 시 null.</summary>
    Task<string?> GetPreviewHtmlAsync(int id, CancellationToken token = default);

    /// <summary>
    /// PDF 다운로드(FileResponse, 봉투 아님) — COMPLETED 아니면 400, 레코드/파일 부재 404.
    /// 성공 시 (bytes, fileName), 실패 시 (null, null, error).
    /// <para><b>410 GONE + <c>PDF_FILE_MISSING</c></b>(레코드는 있으나 파일만 소실)은 전용 안내로 분기한다 —
    /// sub-code 는 <c>error.details.error_code</c>(v8.0) 와 <c>error.message.error_code</c>(v7.0 객체 판본)
    /// 양쪽에서 읽는다(<c>ApiError.DetailsErrorCode</c>). 실패 문구는 서버 <c>error.message</c> 를 우선한다.</para>
    /// </summary>
    Task<ReportPdfResult> DownloadPdfAsync(int id, CancellationToken token = default);

    /// <summary>상세 CSV 다운로드(GET /generations/{id}/detail.csv?type=) — BOM+UTF-8, FileResponse.
    /// <para><paramref name="type"/> 는 <b>닫힌 어휘 8종</b>(<see cref="ReportDetailCsvType"/>) —
    /// 어휘 밖이면 왕복 없이 실패시킨다(서버는 400 으로 거부한다). 파라미터 누락은 서버 422 다.
    /// reports:view 필요. (v6.0 NOTIFY §1-3 · §10.4.8)</para></summary>
    Task<ReportPdfResult> DownloadDetailCsvAsync(int id, string type, CancellationToken token = default);
}

/// <summary>PDF 다운로드 결과 — 봉투 밖 원시 바이트 + 파일명(Content-Disposition) 또는 에러.</summary>
public sealed class ReportPdfResult
{
    public byte[]? Bytes { get; init; }
    public string? FileName { get; init; }
    public string? Error { get; init; }
    public bool Success => Bytes is { Length: > 0 } && Error is null;

    public static ReportPdfResult Ok(byte[] bytes, string? fileName) => new() { Bytes = bytes, FileName = fileName };
    public static ReportPdfResult Fail(string error) => new() { Error = error };
}
