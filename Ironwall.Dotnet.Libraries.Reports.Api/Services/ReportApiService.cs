using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Messages.Helpers;

namespace Ironwall.Dotnet.Libraries.Reports.Api.Services;

/// <summary>
/// GOP 보고서 API 서비스 구현 — IApiService(HTTP 래퍼, Bearer 자동부착)로 /api/reports/* 호출.
/// EventApiService 패턴(ToApiResponseAsync 봉투 파싱, 예외→CreateError) 복제.
/// </summary>
public class ReportApiService : IReportApiService
{
    #region - Ctors -
    public ReportApiService(ILogService? log, IApiService apiService, ApiSetupModel setupModel)
    {
        _log = log;
        _apiService = apiService;
        _setupModel = setupModel;
    }
    #endregion

    #region - IService -
    public Task ExecuteAsync(CancellationToken token = default)
    {
        _apiService.Initialize();
        _log?.Info($"[{nameof(ReportApiService)}] Initialized with BaseUrl: {_setupModel.Url}");
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
    #endregion

    #region - 카탈로그 / 상태 -
    public async Task<ApiResponse<List<ReportComponentCategoryDto>>> GetComponentsAsync(CancellationToken token = default)
    {
        try
        {
            var res = await _apiService.GetRequestAsync($"{_setupModel.Url}/reports/components");
            return await res.ToApiResponseAsync<List<ReportComponentCategoryDto>>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetComponentsAsync)}] {ex.Message}");
            return ApiResponse<List<ReportComponentCategoryDto>>.CreateError("INTERNAL_ERROR", "컴포넌트 카탈로그 조회 실패", ex.Message);
        }
    }

    public async Task<ApiResponse<ReportStatusDto>> GetStatusAsync(CancellationToken token = default)
    {
        try
        {
            var res = await _apiService.GetRequestAsync($"{_setupModel.Url}/reports/status");
            return await res.ToApiResponseAsync<ReportStatusDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetStatusAsync)}] {ex.Message}");
            return ApiResponse<ReportStatusDto>.CreateError("INTERNAL_ERROR", "엔진 상태 조회 실패", ex.Message);
        }
    }
    #endregion

    #region - 템플릿 CRUD -
    public async Task<ApiListResponse<ReportTemplateDto>> GetTemplatesAsync(int page = 1, int limit = 20, CancellationToken token = default)
    {
        try
        {
            var p = new Dictionary<string, string> { ["page"] = page.ToString(), ["limit"] = limit.ToString() };
            var res = await _apiService.GetRequestAsync($"{_setupModel.Url}/reports/templates", p);
            return await res.ToApiListResponseAsync<ReportTemplateDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetTemplatesAsync)}] {ex.Message}");
            return ApiListResponse<ReportTemplateDto>.CreateError("INTERNAL_ERROR", "템플릿 목록 조회 실패", ex.Message);
        }
    }

    public async Task<ApiResponse<ReportTemplateDto>> GetTemplateByIdAsync(int id, CancellationToken token = default)
    {
        try
        {
            var res = await _apiService.GetRequestAsync($"{_setupModel.Url}/reports/templates/{id}");
            return await res.ToApiResponseAsync<ReportTemplateDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetTemplateByIdAsync)}] {ex.Message}");
            return ApiResponse<ReportTemplateDto>.CreateError("INTERNAL_ERROR", $"템플릿 {id} 조회 실패", ex.Message);
        }
    }

    public async Task<ApiResponse<ReportTemplateDto>> CreateTemplateAsync(ReportTemplateCreateDto dto, CancellationToken token = default)
    {
        try
        {
            var res = await _apiService.PostRequestAsync($"{_setupModel.Url}/reports/templates", dto);
            return await res.ToApiResponseAsync<ReportTemplateDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateTemplateAsync)}] {ex.Message}");
            return ApiResponse<ReportTemplateDto>.CreateError("INTERNAL_ERROR", "템플릿 생성 실패", ex.Message);
        }
    }

    public async Task<ApiResponse<ReportTemplateDto>> UpdateTemplateAsync(int id, ReportTemplateUpdateDto dto, CancellationToken token = default)
    {
        try
        {
            var res = await _apiService.PatchRequestAsync($"{_setupModel.Url}/reports/templates/{id}", dto);
            return await res.ToApiResponseAsync<ReportTemplateDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateTemplateAsync)}] {ex.Message}");
            return ApiResponse<ReportTemplateDto>.CreateError("INTERNAL_ERROR", $"템플릿 {id} 수정 실패", ex.Message);
        }
    }

    public async Task<ApiResponse<object>> DeleteTemplateAsync(int id, CancellationToken token = default)
    {
        try
        {
            var res = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/reports/templates/{id}");
            return await res.ToApiResponseAsync<object>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteTemplateAsync)}] {ex.Message}");
            return ApiResponse<object>.CreateError("INTERNAL_ERROR", $"템플릿 {id} 삭제 실패", ex.Message);
        }
    }
    #endregion

    #region - 생성 / 이력 -
    public async Task<ApiResponse<ReportGenerationDto>> GenerateAsync(ReportGenerateRequestDto dto, CancellationToken token = default)
    {
        try
        {
            var res = await _apiService.PostRequestAsync($"{_setupModel.Url}/reports/generate", dto);
            return await res.ToApiResponseAsync<ReportGenerationDto>();   // 202 + data.id
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GenerateAsync)}] {ex.Message}");
            return ApiResponse<ReportGenerationDto>.CreateError("INTERNAL_ERROR", "보고서 생성 요청 실패", ex.Message);
        }
    }

    public async Task<ApiListResponse<ReportGenerationDto>> GetGenerationsAsync(int page = 1, int limit = 20, string? status = null, CancellationToken token = default)
    {
        try
        {
            var p = new Dictionary<string, string> { ["page"] = page.ToString(), ["limit"] = limit.ToString() };

            // status 는 v8.0 부터 닫힌 어휘 5종이다(실측 8.0.1: ?status= · ?status=completed · ?status=BOGUS 전부 422).
            //   ① 빈 값·null 은 "필터 없음" → 파라미터를 붙이지 않는다(빈 문자열은 필터 없음이 아니라 422 다)
            //   ② 소문자는 대문자로 올려 보낸다(서버는 대소문자를 관용하지 않는다)
            //   ③ 어휘 밖이면 왕복 없이 실패로 돌려준다 — 조용히 전체 목록을 주면 "필터가 걸린 줄" 알고 오판한다
            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalized = ReportGenerationStatus.Normalize(status);
                if (normalized is null)
                {
                    _log?.Warning($"[{nameof(GetGenerationsAsync)}] 허용되지 않는 status='{status}' — 요청을 보내지 않았다(허용: {string.Join(", ", ReportGenerationStatus.All)}).");
                    return ApiListResponse<ReportGenerationDto>.CreateError(
                        "VALUE_NOT_ALLOWED",
                        $"상태 필터 값이 올바르지 않습니다(허용: {string.Join(", ", ReportGenerationStatus.All)}).",
                        $"status='{status}'");
                }
                p["status"] = normalized;
            }

            var res = await _apiService.GetRequestAsync($"{_setupModel.Url}/reports/generations", p);
            return await res.ToApiListResponseAsync<ReportGenerationDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetGenerationsAsync)}] {ex.Message}");
            return ApiListResponse<ReportGenerationDto>.CreateError("INTERNAL_ERROR", "생성 이력 조회 실패", ex.Message);
        }
    }

    public async Task<ApiResponse<ReportGenerationDto>> GetGenerationByIdAsync(int id, CancellationToken token = default)
    {
        try
        {
            var res = await _apiService.GetRequestAsync($"{_setupModel.Url}/reports/generations/{id}");
            return await res.ToApiResponseAsync<ReportGenerationDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetGenerationByIdAsync)}] {ex.Message}");
            return ApiResponse<ReportGenerationDto>.CreateError("INTERNAL_ERROR", $"생성 이력 {id} 조회 실패", ex.Message);
        }
    }

    public async Task<ApiResponse<object>> DeleteGenerationAsync(int id, CancellationToken token = default)
    {
        try
        {
            var res = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/reports/generations/{id}");
            return await res.ToApiResponseAsync<object>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteGenerationAsync)}] {ex.Message}");
            return ApiResponse<object>.CreateError("INTERNAL_ERROR", $"생성 이력 {id} 삭제 실패", ex.Message);
        }
    }

    public async Task<ApiResponse<ReportPreviewDto>> GetPreviewAsync(int id, CancellationToken token = default)
    {
        try
        {
            var res = await _apiService.GetRequestAsync($"{_setupModel.Url}/reports/generations/{id}/preview");
            return await res.ToApiResponseAsync<ReportPreviewDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetPreviewAsync)}] {ex.Message}");
            return ApiResponse<ReportPreviewDto>.CreateError("INTERNAL_ERROR", $"미리보기 {id} 조회 실패", ex.Message);
        }
    }

    public async Task<ApiResponse<ReportCancelResultDto>> CancelGenerationAsync(int id, CancellationToken token = default)
    {
        try
        {
            var res = await _apiService.PostRequestAsync($"{_setupModel.Url}/reports/generations/{id}/cancel", new { });
            var parsed = await res.ToApiResponseAsync<ReportCancelResultDto>();

            // 400 = 이미 종결된 생성(COMPLETED/FAILED/CANCELLED)을 취소하려 한 경우(§10.4.7).
            //   서버 사유가 error.message 에만 실리므로 문구는 ApiErrorTextHelper 가 결정한다 — 여기서는 로그만 분화.
            if (!parsed.Success && (int)res.StatusCode == 400)
                _log?.Warning($"[{nameof(CancelGenerationAsync)}] 400 — 이미 종료된 생성이라 취소할 수 없다(id={id}): {parsed.ErrorText()}");

            return parsed;
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CancelGenerationAsync)}] {ex.Message}");
            return ApiResponse<ReportCancelResultDto>.CreateError("INTERNAL_ERROR", $"생성 취소 실패(id={id})", ex.Message);
        }
    }

    public async Task<string?> GetPreviewHtmlAsync(int id, CancellationToken token = default)
    {
        try
        {
            // /api/reports/preview/{id} 는 text/html(봉투 아님) — Bearer는 파이프라인 자동부착.
            var res = await _apiService.GetRequestAsync($"{_setupModel.Url}/reports/preview/{id}");
            if (!res.IsSuccessStatusCode)
            {
                _log?.Warning($"[{nameof(GetPreviewHtmlAsync)}] HTTP {(int)res.StatusCode} — 미리보기 HTML 조회 실패(id={id})");
                return null;
            }
            return await res.Content.ReadAsStringAsync(token);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetPreviewHtmlAsync)}] {ex.Message}");
            return null;
        }
    }

    public async Task<ReportPdfResult> DownloadPdfAsync(int id, CancellationToken token = default)
    {
        try
        {
            var res = await _apiService.GetRequestAsync($"{_setupModel.Url}/reports/generations/{id}/download");
            if (!res.IsSuccessStatusCode)
            {
                // 파일 엔드포인트지만 실패 본문은 표준 오류 봉투다 → 파싱해서 사유를 살린다.
                var err = await res.ToApiResponseAsync<object>();
                var code = (int)res.StatusCode;

                // 410 은 "레코드는 있는데 PDF 파일만 사라짐" — 재생성이 유일한 해결이다.
                //   기계 판독은 error.details.error_code(v8.0) 또는 error.message.error_code(v7.0 객체 판본).
                //   ApiError.DetailsErrorCode 가 그 두 자리를 순서대로 본다 → 판본 무관 분기.
                var subCode = err.Error?.DetailsErrorCode;
                if (code == 410 || string.Equals(subCode, ReportErrorCode.PdfFileMissing, StringComparison.Ordinal))
                {
                    _log?.Warning($"[{nameof(DownloadPdfAsync)}] {code} {ReportErrorCode.PdfFileMissing} — PDF 파일 소실(id={id}): {err.ErrorText()}");
                    return ReportPdfResult.Fail("PDF 파일이 서버 저장소에서 사라졌습니다. 보고서를 다시 생성해 주세요.");
                }

                // v6.0: 404 없음 / 400 미완료 분화 (NOTIFY §1-2). 404 는 두 사유(레코드 없음 / PDF 경로 없음)를
                //   서버가 문장으로 구분하므로 서버 문구를 우선 노출한다.
                var fallback = code switch
                {
                    404 => "보고서를 찾을 수 없거나 PDF 파일이 없습니다. 목록을 갱신하거나 다시 생성해 주세요.",
                    400 => "아직 생성이 완료되지 않았습니다. 완료 후 다시 시도하세요.",
                    403 => "다운로드 권한이 없습니다(reports:view).",
                    _ => $"다운로드 실패(HTTP {code})."
                };
                return ReportPdfResult.Fail(err.ErrorText(fallback));
            }

            var bytes = await res.Content.ReadAsByteArrayAsync(token);
            // Content-Disposition: attachment; filename*=UTF-8''{title}.pdf
            var cd = res.Content.Headers.ContentDisposition;
            var name = (cd?.FileNameStar ?? cd?.FileName)?.Trim('"');
            if (string.IsNullOrWhiteSpace(name)) name = $"report_{id}.pdf";
            return ReportPdfResult.Ok(bytes, name);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DownloadPdfAsync)}] {ex.Message}");
            return ReportPdfResult.Fail(ex.Message);
        }
    }

    public async Task<ReportPdfResult> DownloadDetailCsvAsync(int id, string type, CancellationToken token = default)
    {
        // 어휘 8종 선검증 — 어휘 밖이면 서버가 400 을 주지만, 왕복할 이유가 없다.
        var csvType = ReportDetailCsvType.Normalize(type);
        if (csvType is null)
        {
            _log?.Warning($"[{nameof(DownloadDetailCsvAsync)}] 지원하지 않는 CSV 유형 '{type}' — 요청을 보내지 않았다(허용: {string.Join(", ", ReportDetailCsvType.All)}).");
            return ReportPdfResult.Fail("지원하지 않는 CSV 유형입니다.");
        }

        try
        {
            // 쿼리는 문자열 보간이 아니라 파라미터 사전으로 넘긴다(인코딩은 ApiService 가 한다 — §2.4).
            var p = new Dictionary<string, string> { ["type"] = csvType };
            var res = await _apiService.GetRequestAsync($"{_setupModel.Url}/reports/generations/{id}/detail.csv", p);
            if (!res.IsSuccessStatusCode)
            {
                var err = await res.ToApiResponseAsync<object>();
                // 실측(8.0.1): 잘못된 type → 400 BAD_REQUEST("Unknown type 'x'. Valid: [...]") ·
                //              type 누락 → 422 MISSING_FIELD(query.type) · 미완료 → 400("Report is not COMPLETED yet").
                //   400 의 두 사유는 서버 문장으로만 구분되므로 서버 문구를 그대로 노출한다(종전 "생성 미완료" 고정 오안내 제거).
                var fallback = (int)res.StatusCode switch
                {
                    400 => "CSV 를 내려받을 수 없습니다. 보고서 상태와 유형을 확인하세요.",
                    404 => "보고서를 찾을 수 없습니다.",
                    422 => "요청 파라미터가 누락됐습니다(CSV 유형).",
                    403 => "CSV 다운로드 권한이 없습니다(reports:view).",
                    _ => $"CSV 다운로드 실패(HTTP {(int)res.StatusCode})."
                };
                return ReportPdfResult.Fail(err.ErrorText(fallback));
            }
            var bytes = await res.Content.ReadAsByteArrayAsync(token);
            var cd = res.Content.Headers.ContentDisposition;
            var name = (cd?.FileNameStar ?? cd?.FileName)?.Trim('"');
            if (string.IsNullOrWhiteSpace(name)) name = $"report_{id}_{csvType}.csv";
            return ReportPdfResult.Ok(bytes, name);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DownloadDetailCsvAsync)}] {ex.Message}");
            return ReportPdfResult.Fail(ex.Message);
        }
    }
    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly IApiService _apiService;
    private readonly ApiSetupModel _setupModel;
    #endregion
}
