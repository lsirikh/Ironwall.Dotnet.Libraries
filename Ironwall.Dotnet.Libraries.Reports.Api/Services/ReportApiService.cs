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
            if (!string.IsNullOrEmpty(status)) p["status"] = status;
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

    public async Task<ApiResponse<object>> CancelGenerationAsync(int id, CancellationToken token = default)
    {
        try
        {
            var res = await _apiService.PostRequestAsync($"{_setupModel.Url}/reports/generations/{id}/cancel", new { });
            return await res.ToApiResponseAsync<object>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CancelGenerationAsync)}] {ex.Message}");
            return ApiResponse<object>.CreateError("INTERNAL_ERROR", $"생성 취소 실패(id={id})", ex.Message);
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
                return ReportPdfResult.Fail($"다운로드 실패(HTTP {(int)res.StatusCode}) — 생성 완료 상태인지 확인");

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
    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly IApiService _apiService;
    private readonly ApiSetupModel _setupModel;
    #endregion
}
