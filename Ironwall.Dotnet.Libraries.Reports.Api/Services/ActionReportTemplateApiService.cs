using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace Ironwall.Dotnet.Libraries.Reports.Api.Services;

/// <summary>
/// <see cref="IActionReportTemplateApiService"/> 구현 — <c>/api/events/action-report-templates</c> 7경로.
/// <para>계약 근거: 명세 §6.4.8 + 배포 openapi(8.0.1) + live GET 실측(2026-09-18).
/// 운영 6.3.2 에는 라우터가 <b>없다</b> — 목록 404 를 <c>NOT_SUPPORTED</c> 로 번역해 호출부가 기능을 감출 수 있게 한다.</para>
/// <para><b>클라 선검증</b>: 서버 스키마가 <c>additionalProperties:false</c> + 길이·범위 제약이라
/// 어기면 422 왕복이 된다. 길이(1~500)·<c>display_order</c>(≥0)·reorder 건수(1~500)·id 중복은
/// <b>보내기 전에</b> 막는다(§10 원칙: 닫힌 어휘·범위는 사전 검증).</para>
/// </summary>
public class ActionReportTemplateApiService : IActionReportTemplateApiService
{
    #region - Ctors -
    /// <param name="contractProbe">
    /// 서버 계약 세대 프로브(<b>선택 주입</b>). 판정에 쓰지 않고 <b>진단 문구</b>에만 쓴다 —
    /// 프로브가 미확보면 보수적 폴백(<c>V6_3</c>)이 나오므로 그것으로 기능을 끄면
    /// 8.0 서버에서도 문구 관리가 사라질 수 있다. 지원 여부의 권위는 <b>실제 404</b> 다.
    /// </param>
    public ActionReportTemplateApiService(ILogService? log,
                                          IApiService apiService,
                                          ApiSetupModel setupModel,
                                          IServerContractProbe? contractProbe = null)
    {
        _log = log;
        _apiService = apiService;
        _setupModel = setupModel;
        _contractProbe = contractProbe;
    }
    #endregion

    #region - IService -
    public Task ExecuteAsync(CancellationToken token = default)
    {
        _apiService.Initialize();
        _log?.Info($"[{nameof(ActionReportTemplateApiService)}] Initialized with BaseUrl: {_setupModel.Url}");
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
    #endregion

    #region - Properties -
    public bool? IsSupported => _isSupported;
    #endregion

    #region - 읽기(events:view) -
    public async Task<ApiListResponse<ActionReportTemplateDto>> GetTemplatesAsync(CancellationToken token = default)
    {
        try
        {
            var res = await _apiService.GetRequestAsync(BaseUrl);
            // 목록 경로에는 path 파라미터가 없다 → 404 는 "없는 id"가 아니라 라우터 자체의 부재다(구 서버).
            if ((int)res.StatusCode == 404)
            {
                _isSupported = false;
                _log?.Warning($"[{nameof(GetTemplatesAsync)}] 서버가 조치보고 문구 템플릿 API 를 제공하지 않는다{VersionSuffix}.");
                return ApiListResponse<ActionReportTemplateDto>.CreateError(
                    NotSupportedCode, NotSupportedText, $"GET {BaseUrl} → 404");
            }

            var parsed = await res.ToApiListResponseAsync<ActionReportTemplateDto>();
            if (parsed.Success) _isSupported = true;
            return parsed;
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetTemplatesAsync)}] {ex.Message}");
            return ApiListResponse<ActionReportTemplateDto>.CreateError("INTERNAL_ERROR", "조치보고 문구 목록 조회 실패", ex.Message);
        }
    }

    public async Task<ApiResponse<ActionReportTemplateDto>> GetTemplateByIdAsync(int id, CancellationToken token = default)
    {
        try
        {
            var res = await _apiService.GetRequestAsync($"{BaseUrl}/{id}");
            return await res.ToApiResponseAsync<ActionReportTemplateDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetTemplateByIdAsync)}] {ex.Message}");
            return ApiResponse<ActionReportTemplateDto>.CreateError("INTERNAL_ERROR", $"조치보고 문구 {id} 조회 실패", ex.Message);
        }
    }
    #endregion

    #region - 쓰기(action_report_templates:edit / :delete) -
    public async Task<ApiResponse<ActionReportTemplateDto>> CreateTemplateAsync(ActionReportTemplateCreateDto dto, CancellationToken token = default)
    {
        var invalid = ValidateContent(dto?.Content) ?? ValidateOrder(dto?.DisplayOrder);
        if (invalid != null) return ApiResponse<ActionReportTemplateDto>.CreateError(ClientInvalidCode, invalid, "client-side validation");

        try
        {
            var res = await _apiService.PostRequestAsync(BaseUrl, dto!);
            return await ToWriteResponseAsync(res, "문구를 추가하지 못했습니다.");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateTemplateAsync)}] {ex.Message}");
            return ApiResponse<ActionReportTemplateDto>.CreateError("INTERNAL_ERROR", "조치보고 문구 추가 실패", ex.Message);
        }
    }

    public async Task<ApiResponse<ActionReportTemplateDto>> UpdateTemplateAsync(int id, ActionReportTemplateUpdateDto dto, CancellationToken token = default)
    {
        if (dto is null || (dto.Content is null && dto.DisplayOrder is null))
            return ApiResponse<ActionReportTemplateDto>.CreateError(ClientInvalidCode, "수정할 내용이 없습니다.", "empty PATCH body");

        var invalid = (dto.Content is null ? null : ValidateContent(dto.Content)) ?? ValidateOrder(dto.DisplayOrder);
        if (invalid != null) return ApiResponse<ActionReportTemplateDto>.CreateError(ClientInvalidCode, invalid, "client-side validation");

        try
        {
            var res = await _apiService.PatchRequestAsync($"{BaseUrl}/{id}", dto);
            return await ToWriteResponseAsync(res, "문구를 수정하지 못했습니다.");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateTemplateAsync)}] {ex.Message}");
            return ApiResponse<ActionReportTemplateDto>.CreateError("INTERNAL_ERROR", $"조치보고 문구 {id} 수정 실패", ex.Message);
        }
    }

    public async Task<ApiResponse<ActionReportTemplateDto>> ReplaceTemplateAsync(int id, ActionReportTemplateReplaceDto dto, CancellationToken token = default)
    {
        var invalid = ValidateContent(dto?.Content) ?? ValidateOrder(dto?.DisplayOrder);
        if (invalid != null) return ApiResponse<ActionReportTemplateDto>.CreateError(ClientInvalidCode, invalid, "client-side validation");

        try
        {
            var res = await _apiService.PutRequestAsync($"{BaseUrl}/{id}", dto!);
            return await ToWriteResponseAsync(res, "문구를 교체하지 못했습니다.");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(ReplaceTemplateAsync)}] {ex.Message}");
            return ApiResponse<ActionReportTemplateDto>.CreateError("INTERNAL_ERROR", $"조치보고 문구 {id} 교체 실패", ex.Message);
        }
    }

    public async Task<ApiResponse<object>> DeleteTemplateAsync(int id, CancellationToken token = default)
    {
        try
        {
            var res = await _apiService.DeleteRequestAsync($"{BaseUrl}/{id}");
            return await res.ToApiResponseAsync<object>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteTemplateAsync)}] {ex.Message}");
            return ApiResponse<object>.CreateError("INTERNAL_ERROR", $"조치보고 문구 {id} 삭제 실패", ex.Message);
        }
    }

    public async Task<ApiListResponse<ActionReportTemplateDto>> ReorderTemplatesAsync(IEnumerable<ActionReportTemplateReorderItemDto> items,
                                                                                     CancellationToken token = default)
    {
        var list = items?.ToList() ?? new List<ActionReportTemplateReorderItemDto>();

        // 서버 제약을 그대로 선검증한다 — 1~500건 · id≥1 · display_order≥0 · id 중복 금지(전부 422 사유).
        if (list.Count == 0)
            return ApiListResponse<ActionReportTemplateDto>.CreateError(ClientInvalidCode, "재정렬할 항목이 없습니다.", "items empty");
        if (list.Count > MaxReorderItems)
            return ApiListResponse<ActionReportTemplateDto>.CreateError(ClientInvalidCode, $"한 번에 재정렬할 수 있는 문구는 {MaxReorderItems}건까지입니다.", $"items={list.Count}");
        if (list.Any(x => x.Id < 1))
            return ApiListResponse<ActionReportTemplateDto>.CreateError(ClientInvalidCode, "재정렬 항목의 아이디가 올바르지 않습니다.", "id < 1");
        if (list.Any(x => x.DisplayOrder < 0))
            return ApiListResponse<ActionReportTemplateDto>.CreateError(ClientInvalidCode, "표시 순서는 0 이상이어야 합니다.", "display_order < 0");
        if (list.Select(x => x.Id).Distinct().Count() != list.Count)
            return ApiListResponse<ActionReportTemplateDto>.CreateError(ClientInvalidCode, "재정렬 항목에 같은 문구가 두 번 들어 있습니다.", "duplicate id");

        try
        {
            var body = new ActionReportTemplateReorderRequestDto { Items = list };
            var res = await _apiService.PostRequestAsync($"{BaseUrl}/reorder", body);
            var parsed = await res.ToApiListResponseAsync<ActionReportTemplateDto>();
            if (!parsed.Success && (int)res.StatusCode == 404)
            {
                // 전건 롤백이 계약이다 — 화면 순서를 서버 재조회로 되돌려야 한다.
                _log?.Warning($"[{nameof(ReorderTemplatesAsync)}] 404 — 요청 id 중 서버에 없는 것이 있어 아무것도 변경되지 않았다.");
            }
            return parsed;
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(ReorderTemplatesAsync)}] {ex.Message}");
            return ApiListResponse<ActionReportTemplateDto>.CreateError("INTERNAL_ERROR", "조치보고 문구 순서 저장 실패", ex.Message);
        }
    }
    #endregion

    #region - 내부 -
    /// <summary>
    /// 쓰기 응답 파싱 + <b>409(문구 중복)</b> 전용 문구. 서버 스웨거가 409 를 선언하지 않는 경로도 있어
    /// 상태코드로 직접 판정한다. 그 밖의 실패는 <see cref="ApiErrorTextHelper"/> 결정 순서를 따른다.
    /// </summary>
    private async Task<ApiResponse<ActionReportTemplateDto>> ToWriteResponseAsync(HttpResponseMessage res, string fallback)
    {
        var parsed = await res.ToApiResponseAsync<ActionReportTemplateDto>();
        if (parsed.Success) { _isSupported = true; return parsed; }

        var code = (int)res.StatusCode;
        if (code == 409)
        {
            _log?.Warning($"[{nameof(ActionReportTemplateApiService)}] 409 — 같은 문구가 이미 있다: {parsed.ErrorText(fallback)}");
            var conflict = ApiResponse<ActionReportTemplateDto>.CreateError(
                "CONFLICT", DuplicateContentText, parsed.Error?.Details);
            conflict.StatusCode = code;
            return conflict;
        }
        if (code == 404 && _isSupported == false)
        {
            var unsupported = ApiResponse<ActionReportTemplateDto>.CreateError(NotSupportedCode, NotSupportedText, $"HTTP 404{VersionSuffix}");
            unsupported.StatusCode = code;
            return unsupported;
        }
        return parsed;
    }

    /// <summary><c>content</c> 제약(1~500자) 선검증. 통과면 null, 아니면 사용자 문구.</summary>
    private static string? ValidateContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return "문구를 입력하세요.";
        if (content.Trim().Length > MaxContentLength) return $"문구는 {MaxContentLength}자까지 입력할 수 있습니다.";
        return null;
    }

    /// <summary><c>display_order</c> 제약(≥0) 선검증.</summary>
    private static string? ValidateOrder(int? order)
        => order.HasValue && order.Value < 0 ? "표시 순서는 0 이상이어야 합니다." : null;

    private string BaseUrl => $"{_setupModel.Url}/events/action-report-templates";

    /// <summary>진단 문구용 — 프로브가 확보한 서버 버전(없으면 빈 문자열). 판정에는 쓰지 않는다.</summary>
    private string VersionSuffix
        => _contractProbe?.IsResolved == true ? $"(서버 {_contractProbe.RawVersion})" : string.Empty;
    #endregion

    #region - Attributes -
    /// <summary>서버 <c>content</c> 최대 길이(스키마 <c>maxLength:500</c>).</summary>
    public const int MaxContentLength = 500;
    /// <summary><c>/reorder</c> 최대 건수(스키마 <c>maxItems:500</c>).</summary>
    public const int MaxReorderItems = 500;
    /// <summary>서버가 이 API 를 제공하지 않을 때의 오류 코드(클라 생성 — 서버 코드가 아니다).</summary>
    public const string NotSupportedCode = "NOT_SUPPORTED";
    /// <summary>클라 선검증 실패 코드(서버 왕복 없음).</summary>
    public const string ClientInvalidCode = "CLIENT_VALIDATION";

    private const string NotSupportedText = "이 서버는 조치보고 문구 관리를 지원하지 않습니다.";
    private const string DuplicateContentText = "같은 문구가 이미 등록돼 있습니다.";

    private readonly ILogService? _log;
    private readonly IApiService _apiService;
    private readonly ApiSetupModel _setupModel;
    private readonly IServerContractProbe? _contractProbe;
    private bool? _isSupported;
    #endregion
}
