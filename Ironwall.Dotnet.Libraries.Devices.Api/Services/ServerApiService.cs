using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Services;
/****************************************************************************
   Purpose      : Server API Service Implementation (GOP RESTful API §8.2~8.3, §8.6)
   Created By   : Claude
   Created On   : 2026-02-24
   Department   : SW Team
   Company      : Sensorway Co., Ltd.

   Description  : Server Category, Server Instance, Server Metrics API 호출 서비스 구현
****************************************************************************/

/// <summary>
/// Server API 서비스 구현체
/// </summary>
public class ServerApiService : IServerApiService
{
    #region - Ctors -
    /// <param name="contractProbe">
    /// 서버 계약 세대 프로브(<see cref="IServerContractProbe"/>). <b>선택 주입</b>이다 —
    /// 미주입(<c>null</c>)이면 <see cref="EnumServerContract.V6_3"/>(현 운영 판본)으로 간주한다.
    /// 6.3 과 7.0 은 서버 도메인에서 <b>같은 본문·같은 쿼리로 양립하지 않는다</b>
    /// (예: <c>?category_id=</c> 는 6.3 에서 유효하고 7.0 에서 422, <c>proxy-settings</c> 는 6.3 에서 200 이고 7.0 에서 410).
    /// </param>
    public ServerApiService(
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
    /// 현재 서버 계약 세대. 프로브 미주입·미확보면 <see cref="EnumServerContract.V6_3"/>
    /// (운영이 6.3.2 이므로 틀렸을 때 손해가 가장 작은 쪽 — <see cref="IServerContractProbe"/> 주석 참조).
    /// </summary>
    private EnumServerContract Contract => _contractProbe?.Contract ?? EnumServerContract.V6_3;

    /// <summary>계약이 7.0 이상인가(축 전환 이후).</summary>
    private bool IsAxisEra => Contract >= EnumServerContract.V7_0;

    /// <summary>
    /// 409 Conflict 를 전용 코드·문구로 승격한다(A-devices S-18).
    /// 서버는 <c>name</c> 중복(<c>/api/servers</c> POST·PATCH·PUT)과
    /// 소속 서버가 남은 카테고리 삭제(<c>/api/servers/categories/{id}</c>)를 409 로 돌려주는데,
    /// 호출부에 409 분기가 0건이라 "알 수 없는 오류"로 표시되어 사용자가 재시도 루프에 빠진다.
    /// </summary>
    private ApiResponse<T> PromoteConflict<T>(ApiResponse<T> response, string what)
    {
        if (response.StatusCode != 409) return response;

        var detail = response.Error?.Message;
        if (string.IsNullOrWhiteSpace(detail)) detail = response.Message;

        _log?.Error($"[{nameof(ServerApiService)}] CONFLICT(409) {what}: {detail}");
        response.Error ??= new ApiError();
        response.Error.Code = "CONFLICT";
        return response;
    }

    /// <summary>
    /// <c>?view=</c>·<c>?include=</c> 를 쿼리에 조립한다(서버 인스턴스 조회 2경로 전용 —
    /// 카테고리·계측 경로에는 이 파라미터가 <b>없다</b>, 7.0.1 실측).
    /// <para><b>버전 분기를 하지 않는다</b> — 무엇을 보낼지는 호출부 책임이다.
    /// 값이 비어 있으면 키를 <b>붙이지 않는다</b>: 서버가 닫힌 어휘(<c>basic</c>·<c>full</c> /
    /// <c>connection</c>·<c>server_config</c>)로 가는 중이라 빈 값은 422 위험이다.</para>
    /// </summary>
    private static void AddViewAndInclude(Dictionary<string, string> parameters, string? view, string? include)
    {
        if (!string.IsNullOrWhiteSpace(view)) parameters["view"] = view.Trim();
        if (!string.IsNullOrWhiteSpace(include)) parameters["include"] = include.Trim();
    }

    /// <summary>
    /// 이 판본에 존재하지 않는(=묘비) 엔드포인트 호출을 <b>네트워크에 나가기 전에</b> 차단한다.
    /// 실제로 호출하면 6.3 은 404, 7.0 은 410 을 돌려주는데 우리 매핑에 410 이 없어
    /// <c>UNKNOWN_ERROR</c> 로 표시되고 서버가 지목한 대체 경로 안내가 사람에게 안 보인다(S-19).
    /// </summary>
    private ApiResponse<T> EndpointRemoved<T>(string what, string replacement)
    {
        var message = $"{what} 은(는) 현재 서버 계약({Contract})에 존재하지 않습니다. 대체: {replacement}";
        _log?.Error($"[{nameof(ServerApiService)}] ENDPOINT_REMOVED {message}");
        return ApiResponse<T>.CreateError("ENDPOINT_REMOVED", message, replacement);
    }

    /// <summary>
    /// 축 계약(7.0+)에서 평면 <see cref="ServerDto"/> 그대로는 등록·수정 본문을 보낼 수 없다(D-22) —
    /// <c>category_id</c>·<c>ip_address</c>·<c>port</c>·<c>hostname</c>·<c>user_name</c>·<c>user_password</c>·
    /// <c>threshold_config</c> 전부가 서버의 <c>extra="forbid"</c> 축 계약에서 즉시 422 를 받는 레거시 키다
    /// (<c>app/schemas/server.py:59-66</c> <c>SERVER_REMOVED_FIELDS</c> · <c>:328-339</c> <c>_ServerWriteBase</c>).
    /// <para>이 서비스는 <c>category_id</c>(정수) 하나로 서버 분류를 받는데, 축 계약은 문자열 판별자
    /// <c>category_server</c> 를 요구한다 — 정수 → 문자열 변환은 <c>GET /api/servers/categories</c> 조회가
    /// 선행돼야 하는 <b>비동기 자원 해석</b>이라 이 얇은 매핑 계층에 넣으면 판본 분기가 두 곳(여기 +
    /// <c>ServerAxisWriter</c>)으로 갈라진다("판본 분기는 통로 한 곳에만" — N-12).
    /// 이미 그 해석까지 끝낸 완전한 통로가 있다 — <b>네트워크로 나가기 전에</b> 막고 그리로 보낸다.</para>
    /// </summary>
    private ApiResponse<ServerDto> AxisShapeRequired(string what)
    {
        var message = $"{what} 은(는) 현재 서버 계약({Contract})에서 평면 ServerDto 로 보낼 수 없습니다 — " +
                       "category_id·ip_address·port·hostname·threshold_config 는 7.0+ 축 계약에서 422(UNKNOWN_FIELD)입니다. " +
                       "Devices.Api.Servers.IServerAxisApiService(서버 콘솔의 IServerConsoleService 가 쓰는 통로)를 사용하십시오.";
        _log?.Error($"[{nameof(ServerApiService)}] AXIS_SHAPE_REQUIRED {message}");
        return ApiResponse<ServerDto>.CreateError("AXIS_SHAPE_REQUIRED", message,
            "Ironwall.Dotnet.Libraries.Devices.Api.Servers.IServerAxisApiService");
    }
    #endregion

    #region - Implementation of IService -
    public Task ExecuteAsync(CancellationToken token = default)
    {
        _apiService.Initialize();
        _log?.Info($"[{nameof(ServerApiService)}] Initialized with BaseUrl: {_setupModel.Url}");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        _log?.Info($"[{nameof(ServerApiService)}] Stopping service...");
        return Task.CompletedTask;
    }
    #endregion

    #region - Server Category API (§8.2) -
    public async Task<ApiListResponse<CategoryDto>> GetCategoriesAsync(
        int page = 1, int limit = 20, CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["page"] = page.ToString(),
                ["limit"] = limit.ToString()
            };
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/servers/categories", parameters);
            return await response.ToApiListResponseAsync<CategoryDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetCategoriesAsync)}] Error: {ex.Message}");
            return ApiListResponse<CategoryDto>.CreateError("INTERNAL_ERROR", "Failed to get categories", ex.Message);
        }
    }

    public async Task<ApiResponse<CategoryDetailDto>> GetCategoryByIdAsync(
        int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/servers/categories/{id}");
            return await response.ToApiResponseAsync<CategoryDetailDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetCategoryByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<CategoryDetailDto>.CreateError("INTERNAL_ERROR", $"Failed to get category {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<CategoryDto>> CreateCategoryAsync(
        CategoryDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/servers/categories", dto);
            return await response.ToApiResponseAsync<CategoryDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateCategoryAsync)}] Error: {ex.Message}");
            return ApiResponse<CategoryDto>.CreateError("INTERNAL_ERROR", "Failed to create category", ex.Message);
        }
    }

    public async Task<ApiResponse<CategoryDto>> PatchCategoryAsync(
        int id, CategoryDto dto, CancellationToken token = default)
    {
        try
        {
            var patchBody = JObject.FromObject(dto, JsonSerializer.Create(new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore
            }));
            // Remove empty string properties for PATCH (server rejects empty type_server/name)
            var propsToRemove = patchBody.Properties()
                .Where(p => p.Value.Type == JTokenType.String && string.IsNullOrEmpty(p.Value.ToString()))
                .Select(p => p.Name).ToList();
            foreach (var prop in propsToRemove) patchBody.Remove(prop);

            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/servers/categories/{id}", patchBody);
            return await response.ToApiResponseAsync<CategoryDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchCategoryAsync)}] Error: {ex.Message}");
            return ApiResponse<CategoryDto>.CreateError("INTERNAL_ERROR", $"Failed to patch category {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<CategoryDto>> UpdateCategoryAsync(
        int id, CategoryDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/servers/categories/{id}", dto);
            return await response.ToApiResponseAsync<CategoryDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateCategoryAsync)}] Error: {ex.Message}");
            return ApiResponse<CategoryDto>.CreateError("INTERNAL_ERROR", $"Failed to update category {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<object>> DeleteCategoryAsync(
        int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/servers/categories/{id}");
            // 소속 서버가 남아 있으면 409 — message 에 id:name 목록(최대 10건)이 실려 온다.
            return PromoteConflict(await response.ToApiResponseAsync<object>(), $"카테고리 {id} 삭제");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteCategoryAsync)}] Error: {ex.Message}");
            return ApiResponse<object>.CreateError("INTERNAL_ERROR", $"Failed to delete category {id}", ex.Message);
        }
    }
    #endregion

    #region - Server Instance API (§8.3) -
    public async Task<ApiListResponse<ServerDto>> GetServersAsync(
        int? categoryId = null, string? status = null,
        int page = 1, int limit = 20,
        string? view = null, string? include = null,
        CancellationToken token = default,
        string? categoryServer = null,
        int? unitId = null,
        bool? includeDescendants = null)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["page"] = page.ToString(),
                ["limit"] = limit.ToString()
            };
            if (categoryId.HasValue)
            {
                if (IsAxisEra)
                {
                    // 7.0 은 ?category_id= 를 deprecated 선언만 남기고 값이 오면 422 로 거부한다
                    // (raise_removed_query → override "?category_server="). 그런데 대체 파라미터는
                    // 문자열 판별자(EnumServerType)라 정수 categoryId 로는 만들 수 없다.
                    // → 필터를 생략해 전체(상위집합)를 받고 호출부가 걸러 쓰게 한다.
                    //   목록 조회 전체가 422 로 실패하는 것보다 실패 모드가 확실히 낫다(A-devices S-06).
                    _log?.Warning($"[{nameof(GetServersAsync)}] contract={Contract} 에서 category_id({categoryId}) 필터는 422 다. " +
                                  $"필터를 생략하고 전체 목록을 조회한다 — 카테고리 필터는 ?category_server=<EnumServerType> 로 이관 필요.");
                }
                else
                {
                    parameters["category_id"] = categoryId.Value.ToString();
                }
            }

            if (!string.IsNullOrEmpty(status))
            {
                // EnumServerStatus 는 닫힌 어휘다 — 6.3.2=NORMAL·WARNING·ERROR, 7.0.1=+UNKNOWN(실측).
                // 그 밖의 값(소문자·구 어휘)은 422 라 조회 전체가 실패한다 → 나가기 전에 걸러 낸다(S-24).
                if (_serverStatusVocabulary.Contains(status))
                {
                    parameters["status"] = status;
                }
                else
                {
                    _log?.Warning($"[{nameof(GetServersAsync)}] status=\"{status}\" 는 서버 어휘가 아니다" +
                                  $"({string.Join(" · ", _serverStatusVocabulary)}). 422 를 피해 필터를 생략한다.");
                }
            }

            AddViewAndInclude(parameters, view, include);

            // 7.0 대체 필터 — 제거된 ?category_id= 의 자리를 메운다(문자열 판별자).
            if (!string.IsNullOrWhiteSpace(categoryServer))
            {
                if (IsAxisEra)
                {
                    parameters["category_server"] = categoryServer.Trim();
                }
                else
                {
                    // 6.3 에는 이 키가 없다 — 보내면 조용히 무시되어 "걸렀는데 전건" 이 된다.
                    _log?.Warning($"[{nameof(GetServersAsync)}] contract={Contract} 에는 category_server 가 없다 — 전송 생략. " +
                                  $"6.3 에서는 categoryId 를 사용하십시오. value=\"{categoryServer}\"");
                }
            }

            // 부대 편제 축 — 8.0 이상에서만. 6.3·7.0 은 미지 쿼리를 조용히 무시한다(침묵 실패 방지선).
            if (unitId.HasValue || includeDescendants.HasValue)
            {
                if (Contract < EnumServerContract.V8_0)
                {
                    _log?.Warning($"[{nameof(GetServersAsync)}] contract={Contract} 에는 unit_id·include_descendants 가 없다 — 전송 생략 " +
                                  $"(보내면 '부대로 걸렀는데 전건' 이 된다).");
                }
                else if (!unitId.HasValue)
                {
                    _log?.Warning($"[{nameof(GetServersAsync)}] include_descendants 는 unit_id 와 함께만 유효하다 — 단독 지정이라 전송 생략.");
                }
                else
                {
                    parameters["unit_id"] = unitId.Value.ToString();
                    if (includeDescendants.HasValue)
                        parameters["include_descendants"] = includeDescendants.Value ? "true" : "false";
                }
            }

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/servers", parameters);
            return await response.ToApiListResponseAsync<ServerDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetServersAsync)}] Error: {ex.Message}");
            return ApiListResponse<ServerDto>.CreateError("INTERNAL_ERROR", "Failed to get servers", ex.Message);
        }
    }

    public async Task<ApiResponse<ServerDto>> GetServerByIdAsync(
        int id, string? view = null, string? include = null, CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            AddViewAndInclude(parameters, view, include);

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/servers/{id}", parameters);
            return await response.ToApiResponseAsync<ServerDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetServerByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<ServerDto>.CreateError("INTERNAL_ERROR", $"Failed to get server {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<ServerDto>> CreateServerAsync(
        ServerDto dto, CancellationToken token = default)
    {
        if (IsAxisEra) return AxisShapeRequired("POST /servers (평면 ServerDto)");

        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/servers", dto);
            // name 중복은 409 — 일반 오류로 흘리면 사용자가 같은 이름으로 재시도를 반복한다(S-18).
            return PromoteConflict(await response.ToApiResponseAsync<ServerDto>(), "서버 등록");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateServerAsync)}] Error: {ex.Message}");
            return ApiResponse<ServerDto>.CreateError("INTERNAL_ERROR", "Failed to create server", ex.Message);
        }
    }

    public async Task<ApiResponse<ServerDto>> PatchServerAsync(
        int id, ServerDto dto, CancellationToken token = default)
    {
        if (IsAxisEra) return AxisShapeRequired($"PATCH /servers/{id} (평면 ServerDto)");

        try
        {
            var patchBody = JObject.FromObject(dto, JsonSerializer.Create(new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore
            }));
            // Remove empty string properties for PATCH
            var propsToRemove = patchBody.Properties()
                .Where(p => p.Value.Type == JTokenType.String && string.IsNullOrEmpty(p.Value.ToString()))
                .Select(p => p.Name).ToList();
            foreach (var prop in propsToRemove) patchBody.Remove(prop);

            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/servers/{id}", patchBody);
            return PromoteConflict(await response.ToApiResponseAsync<ServerDto>(), $"서버 {id} 수정");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchServerAsync)}] Error: {ex.Message}");
            return ApiResponse<ServerDto>.CreateError("INTERNAL_ERROR", $"Failed to patch server {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<ServerDto>> UpdateServerAsync(
        int id, ServerDto dto, CancellationToken token = default)
    {
        if (IsAxisEra) return AxisShapeRequired($"PUT /servers/{id} (평면 ServerDto)");

        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/servers/{id}", dto);
            return PromoteConflict(await response.ToApiResponseAsync<ServerDto>(), $"서버 {id} 교체");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateServerAsync)}] Error: {ex.Message}");
            return ApiResponse<ServerDto>.CreateError("INTERNAL_ERROR", $"Failed to update server {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<object>> DeleteServerAsync(
        int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/servers/{id}");
            return await response.ToApiResponseAsync<object>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteServerAsync)}] Error: {ex.Message}");
            return ApiResponse<object>.CreateError("INTERNAL_ERROR", $"Failed to delete server {id}", ex.Message);
        }
    }
    #endregion

    #region - Server Metrics API (§8.6) -
    public async Task<ApiResponse<ServerMetricDto>> CreateServerMetricAsync(
        int serverId, ServerMetricDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/servers/{serverId}/metrics", dto);
            return await response.ToApiResponseAsync<ServerMetricDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateServerMetricAsync)}] Error: {ex.Message}");
            return ApiResponse<ServerMetricDto>.CreateError("INTERNAL_ERROR", "Failed to create server metric", ex.Message);
        }
    }

    public async Task<ApiListResponse<ServerMetricDto>> GetServerMetricsAsync(
        int serverId, string? startDate = null, string? endDate = null,
        int limit = 100, CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["limit"] = limit.ToString()
            };
            // 서버 파라미터는 start_time / end_time 이다(6.3.2·7.0.1·8.0.1 Swagger 실측 동일).
            // 종전 start_date/end_date 는 미선언 쿼리라 FastAPI 가 조용히 버렸고 → 기간 필터가 전혀 안 걸린 채
            // 항상 최신 limit 건이 돌아왔다(A-devices S-13). 그래프·보고서가 잘못된 구간을 그린 원인.
            if (!string.IsNullOrEmpty(startDate)) parameters["start_time"] = startDate;
            if (!string.IsNullOrEmpty(endDate)) parameters["end_time"] = endDate;

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/servers/{serverId}/metrics", parameters);
            return await response.ToApiListResponseAsync<ServerMetricDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetServerMetricsAsync)}] Error: {ex.Message}");
            return ApiListResponse<ServerMetricDto>.CreateError("INTERNAL_ERROR", "Failed to get server metrics", ex.Message);
        }
    }

    public async Task<ApiResponse<ServerMetricLatestDto>> GetServerMetricLatestAsync(
        int serverId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/servers/{serverId}/metrics/latest");
            return await response.ToApiResponseAsync<ServerMetricLatestDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetServerMetricLatestAsync)}] Error: {ex.Message}");
            return ApiResponse<ServerMetricLatestDto>.CreateError("INTERNAL_ERROR", "Failed to get latest server metric", ex.Message);
        }
    }

    /// <summary>
    /// 서버 계측 시계열을 <b>보존기간(일)</b> 기준으로 삭제한다.
    /// <para><b>서버 파라미터는 <c>older_than_days</c>(정수, 기본값 30, 최소 1)</b> 하나뿐이다 —
    /// 6.3.2(운영)·7.0.1·8.0.1 <b>세 판본 Swagger 실측 전부 동일</b>(정수·기본값 30·최소 1)하며 <c>before_date</c> 는 <b>어느 판본에도 없다</b>.</para>
    /// <para>⚠ <b>왜 이 메서드가 필수인가</b>(A-devices S-14 · P0 데이터 손상):
    /// 종전 구현은 <c>?before_date=</c> 를 보냈다. FastAPI 는 <b>미선언 쿼리를 조용히 버리므로</b>
    /// ① 422 가 나지 않고 ② 서버는 <b>기본값 30일로 삭제를 실제로 실행</b>하며
    /// ③ 응답 <c>data</c> 가 <c>null</c> 이라 화면엔 "0건 삭제"로 보였다.
    /// 즉 운영자가 지정한 시점과 무관하게 시계열이 지워지고 아무도 모르는 상태였다.</para>
    /// <para>응답 봉투의 <c>data</c> 는 <b>항상 <c>null</c></b>
    /// (6.3.2 <c>ApiSingleResponse_NoneType_</c> · 7.0.1 <c>ApiWriteResponse_NoneType_</c>)이고
    /// 삭제 건수는 <c>message</c> 문장에만 실린다. 그래서 건수를 <c>message</c> 에서 최선 노력으로 파싱해
    /// <see cref="MetricDeleteResultDto"/> 를 합성하고, 원본 <c>message</c> 는 로그에 남긴다.
    /// 파싱 실패 시 <see cref="MetricDeleteResultDto.DeletedCount"/> 는 <c>0</c> 이지만
    /// 그것이 "0건 삭제"를 의미하지 않는다 — 로그의 <c>message</c> 를 근거로 삼는다.</para>
    /// </summary>
    /// <param name="olderThanDays">
    /// 이 일수보다 <b>오래된</b> 계측을 삭제한다(서버 기준 시각). <b>1 이상 필수</b> —
    /// 기본값을 두지 않는 것이 의도다. 값을 생략할 수 있게 하면 서버 기본값 30일이 조용히 적용되어
    /// 위 P0 결함이 그대로 재현된다.
    /// </param>
    public async Task<ApiResponse<MetricDeleteResultDto>> DeleteServerMetricsAsync(
        int serverId, int olderThanDays, CancellationToken token = default)
    {
        // 서버 스키마 minimum=1. 0·음수는 422 이고, 무엇보다 "전부 삭제" 오해를 부른다 — 나가기 전에 막는다.
        if (olderThanDays < 1)
        {
            var message = $"olderThanDays 는 1 이상이어야 합니다(받은 값: {olderThanDays}). 서버 계약 minimum=1.";
            _log?.Error($"[{nameof(DeleteServerMetricsAsync)}] {message}");
            return ApiResponse<MetricDeleteResultDto>.CreateError("INVALID_ARGUMENT", message);
        }

        try
        {
            // 정수 단일 파라미터 — 이스케이프 불필요(종전 aware ISO '+' 손상 회피 로직도 함께 사라진다).
            var endpoint = $"{_setupModel.Url}/servers/{serverId}/metrics?older_than_days={olderThanDays}";

            var response = await _apiService.DeleteRequestAsync(endpoint);
            var result = await response.ToApiResponseAsync<MetricDeleteResultDto>();

            if (result.Success)
            {
                // 건수는 data 가 아니라 message 에만 있다 — 원본을 반드시 남긴다.
                _log?.Info($"[{nameof(DeleteServerMetricsAsync)}] server={serverId} older_than_days={olderThanDays} " +
                           $"server_message=\"{result.Message}\"");

                result.Data ??= new MetricDeleteResultDto
                {
                    ServerId = serverId,
                    DeletedCount = ParseDeletedCount(result.Message)
                };
            }

            return result;
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteServerMetricsAsync)}] Error: {ex.Message}");
            return ApiResponse<MetricDeleteResultDto>.CreateError("INTERNAL_ERROR", "Failed to delete server metrics", ex.Message);
        }
    }

    /// <summary>
    /// <b>사용 금지</b> — <c>before_date</c> 는 서버에 존재하지 않는 파라미터다(6.3.2·7.0.1·8.0.1 실측).
    /// 호출하면 <b>네트워크에 나가지 않고</b> 오류를 돌려준다. 시점(날짜) 대신
    /// <b>보존기간(일)</b>을 주는 <see cref="DeleteServerMetricsAsync(int, int, CancellationToken)"/> 를 쓴다.
    /// <para>이 오버로드를 남겨 둔 이유는 기존 호출부의 컴파일을 깨지 않으면서
    /// <b>조용한 삭제 경로를 확실히 닫기</b> 위해서다. 종전 구현은 파라미터가 무시된 채
    /// 서버 기본값 30일 삭제를 실행했다(A-devices S-14).</para>
    /// </summary>
    [Obsolete("before_date 는 서버에 없는 파라미터다. DeleteServerMetricsAsync(serverId, olderThanDays) 를 사용하라. " +
              "이 오버로드는 호출을 거부한다(조용한 30일 삭제 방지).", error: false)]
    public Task<ApiResponse<MetricDeleteResultDto>> DeleteServerMetricsAsync(
        int serverId, string? beforeDate = null, CancellationToken token = default)
    {
        var message =
            "DELETE /api/servers/{id}/metrics 는 'before_date' 를 받지 않습니다(6.3.2·7.0.1·8.0.1 실측). " +
            $"보존기간(일)을 넘기는 DeleteServerMetricsAsync(serverId, olderThanDays) 를 사용하십시오. (받은 beforeDate: {beforeDate ?? "null"})";

        _log?.Error($"[{nameof(DeleteServerMetricsAsync)}] REFUSED — {message}");
        return Task.FromResult(ApiResponse<MetricDeleteResultDto>.CreateError("INVALID_ARGUMENT", message));
    }

    /// <summary>
    /// 삭제 건수를 서버 <c>message</c> 문장에서 최선 노력으로 파싱한다(첫 정수).
    /// 서버가 <c>data.deleted_count</c> 를 싣기 시작하면 이 폴백은 필요 없어진다.
    /// </summary>
    private static int ParseDeletedCount(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return 0;
        var match = System.Text.RegularExpressions.Regex.Match(message, @"\d+");
        return match.Success && int.TryParse(match.Value, out var count) ? count : 0;
    }
    #endregion

    #region - Server Status Report (§8.3 · 7.0+) -
    /// <summary>
    /// 서버 매니저가 관측한 서버 상태를 보고한다(<c>PATCH /api/servers/{id}/status</c>, 권한 <c>servers:control</c>).
    /// <para><b>7.0 전용 경로다</b> — 6.3.2 Swagger 에 이 경로가 <b>없다</b>(실측). 6.3 계약에서는 호출하지 않고 오류를 돌려준다.</para>
    /// <para>왜 필요한가(A-devices S-17): <c>status</c> 는 <b>관측 필드</b>라
    /// <c>PATCH /api/servers/{id}</c> 본문에 실으면 422(<c>OBSERVED_FIELD</c>)다.
    /// 상태를 갱신할 유일한 입구가 이 경로인데 구현이 없어 화면이 영구 <c>UNKNOWN</c> 이었다.</para>
    /// </summary>
    /// <param name="status">관측 상태. <c>NORMAL</c> · <c>WARNING</c> · <c>ERROR</c> 만 허용 — <c>UNKNOWN</c> 보고는 서버가 422.</param>
    /// <param name="observedAt">
    /// 관측 시각. <b>오프셋 필수</b>이므로 <see cref="DateTimeOffset"/> 로 받아 ISO8601(오프셋 포함)로 직렬화한다.
    /// 생략하면 서버가 수신 시각으로 채우고 <c>OBSERVED_AT_DEFAULTED</c> 경고를 싣는다.
    /// </param>
    public async Task<ApiResponse<ServerStatusReportDto>> ReportServerStatusAsync(
        int serverId, string status, DateTimeOffset? observedAt = null, CancellationToken token = default)
    {
        if (!IsAxisEra)
            return EndpointRemoved<ServerStatusReportDto>(
                $"PATCH /servers/{serverId}/status",
                "6.3 계약에는 이 경로가 없다 — 서버를 7.0 이상으로 올린 뒤 사용하십시오.");

        // UNKNOWN 은 서버가 명시적으로 422 로 거부한다(보고할 수 없는 값이다).
        if (!_serverStatusReportVocabulary.Contains(status))
        {
            var message = $"보고 가능한 상태는 {string.Join(" · ", _serverStatusReportVocabulary)} 뿐입니다(받은 값: \"{status}\"). " +
                          "UNKNOWN 보고는 서버가 422 로 거부합니다.";
            _log?.Error($"[{nameof(ReportServerStatusAsync)}] {message}");
            return ApiResponse<ServerStatusReportDto>.CreateError("INVALID_ARGUMENT", message);
        }

        try
        {
            var body = new ServerStatusReportDto
            {
                Status = status,
                ObservedAt = observedAt?.ToString("o")   // 오프셋 포함 ISO8601 — 없으면 서버가 CONSTRAINT 422
            };

            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/servers/{serverId}/status", body);
            return await response.ToApiResponseAsync<ServerStatusReportDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(ReportServerStatusAsync)}] Error: {ex.Message}");
            return ApiResponse<ServerStatusReportDto>.CreateError("INTERNAL_ERROR", $"Failed to report status for server {serverId}", ex.Message);
        }
    }
    #endregion

    #region - Proxy Settings (§8.8) -
    /// <summary>
    /// 프록시 설정 조회. <b>계약별로 존재 여부가 다르다</b>(실측):
    /// 6.3.2 = <c>200</c> 정상 동작 · 7.0.1 = <b><c>410 ENDPOINT_REMOVED</c> 묘비</b>이고
    /// 대체는 <c>GET /api/servers/{id}/config</c> 의 <c>server_config.modes</c>
    /// (<c>windy_mode</c> 값은 <b>소문자</b> — 대문자는 422).
    /// <para>7.0 에서는 네트워크에 나가기 전에 차단한다 — 410 이 우리 매핑에 없어
    /// "알 수 없는 오류"로 표시되고 대체 경로 안내가 유실되기 때문이다(S-15 · S-19).</para>
    /// </summary>
    public async Task<ApiResponse<ProxySettingDto>> GetProxySettingsAsync(
        int serverId, CancellationToken token = default)
    {
        if (IsAxisEra)
            return EndpointRemoved<ProxySettingDto>(
                $"GET /servers/{serverId}/proxy-settings",
                $"GET /servers/{serverId}/config 의 server_config.modes (operation_mode · windy_mode — 소문자)");

        try
        {
            var url = $"{_setupModel.Url}/servers/{serverId}/proxy-settings";
            _log?.Info($"[{nameof(GetProxySettingsAsync)}] GET {url}");

            var response = await _apiService.GetRequestAsync(url);
            return await response.ToApiResponseAsync<ProxySettingDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetProxySettingsAsync)}] Error: {ex.Message}");
            return ApiResponse<ProxySettingDto>.CreateError("INTERNAL_ERROR", "Failed to get proxy settings", ex.Message);
        }
    }
    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly IApiService _apiService;
    private readonly ApiSetupModel _setupModel;
    private readonly IServerContractProbe? _contractProbe;

    /// <summary>
    /// <c>?status=</c> 필터에 허용되는 어휘(EnumServerStatus). 6.3.2 는 3값, 7.0.1 은 UNKNOWN 을 더한 4값(실측).
    /// 두 판본의 <b>합집합</b>을 쓴다 — 6.3 에 UNKNOWN 을 보내면 422 지만, 그건 호출부가 그 값을 쓸 때만 발생하고
    /// 교집합으로 좁히면 7.0 에서 유효한 필터를 우리가 막아 버린다.
    /// </summary>
    private static readonly HashSet<string> _serverStatusVocabulary =
        new(StringComparer.Ordinal) { "NORMAL", "WARNING", "ERROR", "UNKNOWN" };

    /// <summary>
    /// <c>PATCH /servers/{id}/status</c> 로 <b>보고</b>할 수 있는 어휘. <c>UNKNOWN</c> 은 빠진다 —
    /// 서버가 명시적으로 422 로 거부한다(관측되지 않은 상태는 보고 대상이 아니다).
    /// </summary>
    private static readonly HashSet<string> _serverStatusReportVocabulary =
        new(StringComparer.Ordinal) { "NORMAL", "WARNING", "ERROR" };
    #endregion
}
