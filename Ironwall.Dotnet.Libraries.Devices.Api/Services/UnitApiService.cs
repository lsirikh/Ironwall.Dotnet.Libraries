using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Ironwall.Dotnet.Libraries.Messages.Helpers;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Services;
/****************************************************************************
   Purpose      : 부대 편제 API 서비스 구현 (GOP RESTful API §11-A, /api/units)
   Created By   : Claude
   Created On   : 2026-09-18
   Department   : SW Team
   Company      : Sensorway Co., Ltd.

   Description  : /api/units 3경로 · 7 엔드포인트 호출 구현.
                  서버 8.0 전용 표면이므로 모든 호출이 V8_0 게이트 뒤에 있다.
****************************************************************************/

/// <summary>
/// 부대 편제 API 서비스 구현체.
/// </summary>
public class UnitApiService : IUnitApiService
{
    #region - Ctors -
    /// <param name="contractProbe">
    /// 서버 계약 세대 프로브. <b>선택 주입</b>이고 미주입(<c>null</c>)이면
    /// <see cref="EnumServerContract.V6_3"/>(운영 판본)으로 간주한다 — 즉 <b>부대 호출이 전부 차단</b>된다.
    /// <para>이 방향이 안전한 쪽이다: 프로브가 없는 호스트에서 부대 API 를 부르면 운영 서버(6.3.2)에
    /// 404 폭격을 하게 되고, 그 404 는 "알 수 없는 오류"로만 보인다.</para>
    /// </param>
    public UnitApiService(
        ILogService? log,
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

    #region - Contract Gate -
    /// <summary>
    /// 현재 서버 계약 세대. 프로브 미주입·미확보면 <see cref="EnumServerContract.V6_3"/>.
    /// </summary>
    private EnumServerContract Contract => _contractProbe?.Contract ?? EnumServerContract.V6_3;

    /// <summary>
    /// 부대 편제 표면을 쓸 수 있는가 — <b><c>&gt;=</c> 비교만 쓴다</b>.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>== EnumServerContract.V8_0</c> 같은 동치 비교를 쓰면 9.0 서버에서 부대 기능이 통째로 죽는다.
    /// 계약 세대는 <b>단조 증가</b>하는 축이고 이 표면은 8.0 에서 <b>생겼다</b>(사라진 게 아니다).
    /// </remarks>
    private bool IsUnitEra => Contract >= EnumServerContract.V8_0;

    /// <summary>부대 엔드포인트 기본 경로.</summary>
    private string UnitsUrl => $"{_setupModel.Url}/units";

    /// <summary>
    /// 이 계약 세대에 부대 표면이 없음을 <b>네트워크에 나가기 전에</b> 알린다.
    /// </summary>
    private ApiResponse<T> UnitsUnsupported<T>(string what)
    {
        var message = $"부대 편제({what})는 현재 서버 계약({Contract})에 존재하지 않습니다 — "
                    + $"서버 {nameof(EnumServerContract.V8_0)} 이상에서만 제공됩니다(6.3·7.0 은 404).";
        _log?.Warning($"[{nameof(UnitApiService)}] ENDPOINT_REMOVED {message}");
        return ApiResponse<T>.CreateError("ENDPOINT_REMOVED", message, "/api/units (API 8.0+)");
    }

    /// <summary>목록형 응답용 <see cref="UnitsUnsupported{T}"/>.</summary>
    private ApiListResponse<T> UnitsUnsupportedList<T>(string what)
    {
        var message = $"부대 편제({what})는 현재 서버 계약({Contract})에 존재하지 않습니다 — "
                    + $"서버 {nameof(EnumServerContract.V8_0)} 이상에서만 제공됩니다(6.3·7.0 은 404).";
        _log?.Warning($"[{nameof(UnitApiService)}] ENDPOINT_REMOVED {message}");
        return ApiListResponse<T>.CreateError("ENDPOINT_REMOVED", message, "/api/units (API 8.0+)");
    }

    /// <summary>
    /// 전송 전 지역 검증 실패를 서버 422 와 <b>같은 모양</b>으로 돌려준다
    /// (호출부가 두 갈래로 분기하지 않게).
    /// </summary>
    private ApiResponse<T> LocalValidationError<T>(string what, string detail)
    {
        _log?.Warning($"[{nameof(UnitApiService)}] VALIDATION_ERROR {what}: {detail}");
        return ApiResponse<T>.CreateError("VALIDATION_ERROR", detail, what);
    }

    /// <summary>
    /// 409 Conflict 를 전용 코드·문구로 승격한다 — 부대 삭제는 <b>매달린 것이 있으면 409</b> 이고,
    /// 건수는 <c>error.details.counts</c> 에 실린다. 분기가 없으면 "알 수 없는 오류"로 보인다.
    /// </summary>
    private ApiResponse<T> PromoteConflict<T>(ApiResponse<T> response, string what)
    {
        if (response.StatusCode != 409) return response;

        var detail = response.Error?.Message;
        if (string.IsNullOrWhiteSpace(detail)) detail = response.Message;

        _log?.Error($"[{nameof(UnitApiService)}] CONFLICT(409) {what}: {detail}");
        response.Error ??= new ApiError();
        response.Error.Code = "CONFLICT";
        return response;
    }
    #endregion

    #region - Implementation of IService -
    public Task ExecuteAsync(CancellationToken token = default)
    {
        _apiService.Initialize();
        _log?.Info($"[{nameof(UnitApiService)}] Initialized with BaseUrl: {_setupModel.Url} (Contract: {Contract})");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        _log?.Info($"[{nameof(UnitApiService)}] Stopping service...");
        return Task.CompletedTask;
    }
    #endregion

    #region - 읽기 (units:view) -
    public async Task<ApiListResponse<UnitListDto>> GetUnitsAsync(
        int page = 1,
        int limit = 20,
        EnumUnitEchelon? echelon = null,
        int? parentId = null,
        bool? isEnable = null,
        CancellationToken token = default)
    {
        if (!IsUnitEra) return UnitsUnsupportedList<UnitListDto>("목록 조회");

        try
        {
            // 서버 검증 범위로 먼저 꺾는다 — page>=1, 1<=limit<=100. 넘기면 422 라 조회 자체가 죽는다.
            var safePage = page < 1 ? 1 : page;
            var safeLimit = limit < 1 ? 1 : (limit > MAX_LIMIT ? MAX_LIMIT : limit);

            var parameters = new Dictionary<string, string>
            {
                ["page"] = safePage.ToString(),
                ["limit"] = safeLimit.ToString(),
            };

            // 선택 필터는 값이 있을 때만 키를 붙인다 — 빈 값은 422 위험이다(닫힌 어휘).
            if (echelon.HasValue) parameters["echelon"] = UnitRules.ToWire(echelon.Value);
            if (parentId.HasValue) parameters["parent_id"] = parentId.Value.ToString();
            if (isEnable.HasValue) parameters["is_enable"] = isEnable.Value ? "true" : "false";

            var response = await _apiService.GetRequestAsync(UnitsUrl, parameters);
            return await response.ToApiListResponseAsync<UnitListDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetUnitsAsync)}] Error: {ex.Message}");
            return ApiListResponse<UnitListDto>.CreateError("INTERNAL_ERROR", "부대 목록 조회에 실패했습니다.", ex.Message);
        }
    }

    public async Task<ApiResponse<UnitDetailDto>> GetUnitAsync(
        int unitId,
        string? include = null,
        CancellationToken token = default)
    {
        if (!IsUnitEra) return UnitsUnsupported<UnitDetailDto>("단건 조회");

        if (unitId <= 0)
            return LocalValidationError<UnitDetailDto>("unit_id", $"부대 id 가 올바르지 않습니다: {unitId}");

        if (!UnitRules.TryValidateInclude(include, out var normalizedInclude, out var includeError))
            return LocalValidationError<UnitDetailDto>("query.include", includeError!);

        try
        {
            Dictionary<string, string>? parameters = null;
            if (!string.IsNullOrEmpty(normalizedInclude))
                parameters = new Dictionary<string, string> { ["include"] = normalizedInclude! };

            var response = await _apiService.GetRequestAsync($"{UnitsUrl}/{unitId}", parameters);
            return await response.ToApiResponseAsync<UnitDetailDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetUnitAsync)}] Error: {ex.Message}");
            return ApiResponse<UnitDetailDto>.CreateError("INTERNAL_ERROR", $"부대 {unitId} 조회에 실패했습니다.", ex.Message);
        }
    }

    public async Task<ApiResponse<UnitGraphDto>> GetUnitGraphAsync(
        int? rootId = null,
        int? depth = null,
        CancellationToken token = default)
    {
        if (!IsUnitEra) return UnitsUnsupported<UnitGraphDto>("관계도 조회");

        if (depth.HasValue && depth.Value < 1)
            return LocalValidationError<UnitGraphDto>("query.depth",
                $"depth 는 1 이상이어야 합니다(뿌리를 0 으로 세어 1 이면 직속 자식까지). 요청: {depth.Value}");

        if (rootId.HasValue && rootId.Value <= 0)
            return LocalValidationError<UnitGraphDto>("query.root_id", $"root_id 가 올바르지 않습니다: {rootId.Value}");

        try
        {
            Dictionary<string, string>? parameters = null;
            if (rootId.HasValue || depth.HasValue)
            {
                parameters = new Dictionary<string, string>();
                if (rootId.HasValue) parameters["root_id"] = rootId.Value.ToString();
                if (depth.HasValue) parameters["depth"] = depth.Value.ToString();
            }

            // ⚠ 경로를 반드시 'units/graph' 로 고정해 부른다 — 서버 라우터가 /{unit_id} 보다 앞에
            //    선언돼 있어야 동작하는 구조이고, 우리가 id 자리에 'graph' 를 넣는 실수를 하면 422 다.
            var response = await _apiService.GetRequestAsync($"{UnitsUrl}/graph", parameters);
            return await response.ToApiResponseAsync<UnitGraphDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetUnitGraphAsync)}] Error: {ex.Message}");
            return ApiResponse<UnitGraphDto>.CreateError("INTERNAL_ERROR", "부대 관계도 조회에 실패했습니다.", ex.Message);
        }
    }
    #endregion

    #region - 쓰기 (units:edit / units:delete) -
    public async Task<ApiResponse<UnitDto>> CreateUnitAsync(
        UnitCreateDto dto,
        CancellationToken token = default)
    {
        if (!IsUnitEra) return UnitsUnsupported<UnitDto>("생성");

        if (dto is null)
            return LocalValidationError<UnitDto>("body", "요청 본문이 없습니다.");

        // 코드는 등록 뒤 불변이다 — 오타 한 번이 영구히 남고 NATS subject 토큰까지 그걸로 고정된다.
        if (!UnitRules.TryValidateCode(dto.Code, out var codeError))
            return LocalValidationError<UnitDto>("code", codeError!);

        if (string.IsNullOrWhiteSpace(dto.Name))
            return LocalValidationError<UnitDto>("name", "부대 이름은 1자 이상이어야 합니다.");

        if (dto.Name.Length > UnitRules.NAME_MAX_LENGTH)
            return LocalValidationError<UnitDto>("name",
                $"부대 이름이 {UnitRules.NAME_MAX_LENGTH}자를 넘습니다(현재 {dto.Name.Length}자).");

        if (dto.Description is { Length: > UnitRules.DESCRIPTION_MAX_LENGTH })
            return LocalValidationError<UnitDto>("description",
                $"설명이 {UnitRules.DESCRIPTION_MAX_LENGTH}자를 넘습니다(현재 {dto.Description.Length}자).");

        try
        {
            // 서버와 같은 정규화(중복 제거·오름차순)를 미리 해 둔다. null 은 null 로 남겨
            // "보내지 않음"을 유지한다 — 빈 배열로 바꾸면 의미가 달라진다.
            dto.AdjacentUnitIds = UnitRules.NormalizeAdjacency(dto.AdjacentUnitIds);

            var response = await _apiService.PostRequestAsync(UnitsUrl, dto);
            return await response.ToApiResponseAsync<UnitDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateUnitAsync)}] Error: {ex.Message}");
            return ApiResponse<UnitDto>.CreateError("INTERNAL_ERROR", "부대 생성에 실패했습니다.", ex.Message);
        }
    }

    public async Task<ApiResponse<UnitDto>> PatchUnitAsync(
        int unitId,
        UnitUpdateDto dto,
        CancellationToken token = default)
    {
        if (!IsUnitEra) return UnitsUnsupported<UnitDto>("부분 수정");

        if (unitId <= 0)
            return LocalValidationError<UnitDto>("unit_id", $"부대 id 가 올바르지 않습니다: {unitId}");

        if (dto is null)
            return LocalValidationError<UnitDto>("body", "요청 본문이 없습니다.");

        if (dto.IsEmpty)
            return LocalValidationError<UnitDto>("body",
                "바꿀 필드가 하나도 없습니다 — PATCH 는 보낸 필드만 반영하므로 빈 본문은 보낼 이유가 없습니다.");

        // 코드는 바꿀 수 없다. 보내려면 현재 값과 같아야 하고, 형식조차 틀리면 그 자리에서 잡는다.
        if (dto.Code is not null && !UnitRules.TryValidateCode(dto.Code, out var codeError))
            return LocalValidationError<UnitDto>("code", codeError!);

        // 자기 자신을 인접으로 지정하면 서버가 422 다 — 정규화에서 걸러낸다.
        if (dto.AdjacentUnitIds is not null && dto.AdjacentUnitIds.Contains(unitId))
        {
            _log?.Warning($"[{nameof(PatchUnitAsync)}] 인접 목록에서 자기 자신({unitId})을 제거했습니다.");
        }

        if (dto.ParentId.HasValue && dto.ParentId.Value == unitId)
            return LocalValidationError<UnitDto>("parent_id", "자기 자신을 상위 부대로 지정할 수 없습니다.");

        try
        {
            if (dto.AdjacentUnitIds is not null)
                dto.AdjacentUnitIds = UnitRules.NormalizeAdjacency(dto.AdjacentUnitIds, unitId);

            var response = await _apiService.PatchRequestAsync($"{UnitsUrl}/{unitId}", dto);
            return await response.ToApiResponseAsync<UnitDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchUnitAsync)}] Error: {ex.Message}");
            return ApiResponse<UnitDto>.CreateError("INTERNAL_ERROR", $"부대 {unitId} 수정에 실패했습니다.", ex.Message);
        }
    }

    public async Task<ApiResponse<UnitDto>> ReplaceUnitAsync(
        int unitId,
        UnitReplaceDto dto,
        CancellationToken token = default)
    {
        if (!IsUnitEra) return UnitsUnsupported<UnitDto>("전체 교체");

        if (unitId <= 0)
            return LocalValidationError<UnitDto>("unit_id", $"부대 id 가 올바르지 않습니다: {unitId}");

        if (dto is null)
            return LocalValidationError<UnitDto>("body", "요청 본문이 없습니다.");

        // 🔴 전삭제 방지 게이트 — PUT 에서 adjacent_unit_ids 를 생략하면 서버가 인접을 전부 지운다.
        //    "부분 수정 의도"가 소리 없이 삭제로 이어지는 유일한 지점이라 여기서 구조적으로 막는다.
        if (dto.AdjacentUnitIds is null)
            return LocalValidationError<UnitDto>("adjacent_unit_ids",
                "PUT 은 전체 교체입니다 — adjacent_unit_ids 를 생략하면 서버가 인접을 전부 지웁니다. "
                + "현재 값을 유지하려면 UnitReplaceDto.FromCurrent(단건 조회 결과) 로 만들고, "
                + "부분 수정이라면 PATCH(PatchUnitAsync)를 쓰고, 정말 비울 의도라면 빈 목록을 명시하십시오.");

        // 🔴 루트 이동 방지 게이트 — PUT 에서 parent_id 를 빼면 서버는 그 부대를 루트로 옮기고, 편제가 바뀐 부대의
        //    관계도 배치 행까지 지운다(서버 v8.0.4 회신 2026-09-29 ⑦ · REST §11-A.6). 최상위로 두는 것이 의도일 때만 보낸다.
        if (dto.ParentId is null && !dto.IsRootIntended)
            return LocalValidationError<UnitDto>("parent_id",
                "PUT 은 전체 교체입니다 — parent_id 를 생략하면 서버가 이 부대를 최상위로 옮기고 관계도 배치도 지웁니다. "
                + "일부 필드만 고치려면 PATCH(PatchUnitAsync)를, 현재 값을 유지하려면 UnitReplaceDto.FromCurrent(단건 조회 결과)를 쓰고, "
                + "정말 최상위로 옮길 의도라면 IsRootIntended = true 로 밝히십시오.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            return LocalValidationError<UnitDto>("name", "부대 이름은 1자 이상이어야 합니다.");

        if (dto.Name.Length > UnitRules.NAME_MAX_LENGTH)
            return LocalValidationError<UnitDto>("name",
                $"부대 이름이 {UnitRules.NAME_MAX_LENGTH}자를 넘습니다(현재 {dto.Name.Length}자).");

        if (dto.Description is { Length: > UnitRules.DESCRIPTION_MAX_LENGTH })
            return LocalValidationError<UnitDto>("description",
                $"설명이 {UnitRules.DESCRIPTION_MAX_LENGTH}자를 넘습니다(현재 {dto.Description.Length}자).");

        // code 는 선택이다. 보낼 거면 형식은 맞아야 한다(값 일치는 서버가 본다).
        if (dto.Code is not null && !UnitRules.TryValidateCode(dto.Code, out var codeError))
            return LocalValidationError<UnitDto>("code", codeError!);

        if (dto.ParentId.HasValue && dto.ParentId.Value == unitId)
            return LocalValidationError<UnitDto>("parent_id", "자기 자신을 상위 부대로 지정할 수 없습니다.");

        try
        {
            dto.AdjacentUnitIds = UnitRules.NormalizeAdjacency(dto.AdjacentUnitIds, unitId) ?? new List<int>();

            var response = await _apiService.PutRequestAsync($"{UnitsUrl}/{unitId}", dto);
            return await response.ToApiResponseAsync<UnitDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(ReplaceUnitAsync)}] Error: {ex.Message}");
            return ApiResponse<UnitDto>.CreateError("INTERNAL_ERROR", $"부대 {unitId} 교체에 실패했습니다.", ex.Message);
        }
    }

    public async Task<ApiResponse<UnitDeleteResultDto>> DeleteUnitAsync(
        int unitId,
        CancellationToken token = default)
    {
        if (!IsUnitEra) return UnitsUnsupported<UnitDeleteResultDto>("삭제");

        if (unitId <= 0)
            return LocalValidationError<UnitDeleteResultDto>("unit_id", $"부대 id 가 올바르지 않습니다: {unitId}");

        try
        {
            var response = await _apiService.DeleteRequestAsync($"{UnitsUrl}/{unitId}");
            var result = await response.ToApiResponseAsync<UnitDeleteResultDto>();

            // 매달린 것이 있으면 409 — 건수는 error.details.counts 에 있다. 승격해 사용자가 이유를 보게 한다.
            return PromoteConflict(result, $"부대 {unitId} 삭제(매달린 리소스가 있으면 지울 수 없습니다 — "
                                         + "운용을 멈추려면 is_enable=false 로 내리십시오)");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteUnitAsync)}] Error: {ex.Message}");
            return ApiResponse<UnitDeleteResultDto>.CreateError("INTERNAL_ERROR", $"부대 {unitId} 삭제에 실패했습니다.", ex.Message);
        }
    }
    #endregion

    #region - Attributes -
    /// <summary>서버가 허용하는 목록 페이지 크기 상한(스웨거 <c>limit.maximum</c> 실측).</summary>
    private const int MAX_LIMIT = 100;

    private readonly ILogService? _log;
    private readonly IApiService _apiService;
    private readonly ApiSetupModel _setupModel;
    private readonly IServerContractProbe? _contractProbe;
    #endregion
}
