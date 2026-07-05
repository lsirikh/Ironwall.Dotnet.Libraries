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
    Task<ApiListResponse<ReportGenerationDto>> GetGenerationsAsync(int page = 1, int limit = 20, string? status = null, CancellationToken token = default);
    Task<ApiResponse<ReportGenerationDto>> GetGenerationByIdAsync(int id, CancellationToken token = default);

    /// <summary>생성 이력 삭제(DELETE /generations/{id}) — DB row + PDF 파일 정리. reports:delete 필요.</summary>
    Task<ApiResponse<object>> DeleteGenerationAsync(int id, CancellationToken token = default);

    /// <summary>생성 취소(POST /generations/{id}/cancel) — PENDING/GENERATING만 CANCELLED. 이미 종료=400. reports:delete 필요.</summary>
    Task<ApiResponse<object>> CancelGenerationAsync(int id, CancellationToken token = default);

    Task<ApiResponse<ReportPreviewDto>> GetPreviewAsync(int id, CancellationToken token = default);

    /// <summary>미리보기 페이지 원시 HTML(자립형: 인라인 CSS/Chart.js) — WebView2 embed용. reports:view 필요. 실패 시 null.</summary>
    Task<string?> GetPreviewHtmlAsync(int id, CancellationToken token = default);

    /// <summary>
    /// PDF 다운로드(FileResponse, 봉투 아님) — COMPLETED 아니면 400, 파일부재 404.
    /// 성공 시 (bytes, fileName), 실패 시 (null, null, error).
    /// </summary>
    Task<ReportPdfResult> DownloadPdfAsync(int id, CancellationToken token = default);

    /// <summary>상세 CSV 다운로드(GET /generations/{id}/detail.csv?type=) — BOM+UTF-8, FileResponse.
    /// type: detection/malfunction/action/system/config/audit/login/session. reports:view 필요. (v6.0 NOTIFY §1-3)</summary>
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
