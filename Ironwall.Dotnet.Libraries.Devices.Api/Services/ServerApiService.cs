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
    public ServerApiService(
        ILogService? log,
        IApiService apiService,
        ApiSetupModel setupModel)
    {
        _log = log;
        _apiService = apiService;
        _setupModel = setupModel;
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
            return await response.ToApiResponseAsync<object>();
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
        int page = 1, int limit = 20, CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["page"] = page.ToString(),
                ["limit"] = limit.ToString()
            };
            if (categoryId.HasValue) parameters["category_id"] = categoryId.Value.ToString();
            if (!string.IsNullOrEmpty(status)) parameters["status"] = status;

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
        int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/servers/{id}");
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
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/servers", dto);
            return await response.ToApiResponseAsync<ServerDto>();
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
            return await response.ToApiResponseAsync<ServerDto>();
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
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/servers/{id}", dto);
            return await response.ToApiResponseAsync<ServerDto>();
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
            if (!string.IsNullOrEmpty(startDate)) parameters["start_date"] = startDate;
            if (!string.IsNullOrEmpty(endDate)) parameters["end_date"] = endDate;

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

    public async Task<ApiResponse<MetricDeleteResultDto>> DeleteServerMetricsAsync(
        int serverId, string? beforeDate = null, CancellationToken token = default)
    {
        try
        {
            var endpoint = $"{_setupModel.Url}/servers/{serverId}/metrics";
            if (!string.IsNullOrEmpty(beforeDate))
                endpoint += $"?before_date={beforeDate}";

            var response = await _apiService.DeleteRequestAsync(endpoint);
            return await response.ToApiResponseAsync<MetricDeleteResultDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteServerMetricsAsync)}] Error: {ex.Message}");
            return ApiResponse<MetricDeleteResultDto>.CreateError("INTERNAL_ERROR", "Failed to delete server metrics", ex.Message);
        }
    }
    #endregion

    #region - Proxy Settings (§8.8) -
    public async Task<ApiResponse<ProxySettingDto>> GetProxySettingsAsync(
        int serverId, CancellationToken token = default)
    {
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
    #endregion
}
