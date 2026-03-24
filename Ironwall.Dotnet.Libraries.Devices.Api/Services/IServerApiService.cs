using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Services;
/****************************************************************************
   Purpose      : Server API Service Interface (GOP RESTful API §8.2~8.3, §8.6)
   Created By   : Claude
   Created On   : 2026-02-24
   Department   : SW Team
   Company      : Sensorway Co., Ltd.

   Description  : Server Category, Server Instance, Server Metrics API 호출 서비스
                  - /api/servers/categories (§8.2)
                  - /api/servers (§8.3)
                  - /api/servers/{id}/metrics (§8.6)
****************************************************************************/

/// <summary>
/// Server API 서비스 인터페이스
/// </summary>
public interface IServerApiService : IService
{
    // ────────────────────────── Server Category CRUD (§8.2) ──────────────────────────

    Task<ApiListResponse<CategoryDto>> GetCategoriesAsync(
        int page = 1,
        int limit = 20,
        CancellationToken token = default);

    Task<ApiResponse<CategoryDetailDto>> GetCategoryByIdAsync(
        int id,
        CancellationToken token = default);

    Task<ApiResponse<CategoryDto>> CreateCategoryAsync(
        CategoryDto dto,
        CancellationToken token = default);

    Task<ApiResponse<CategoryDto>> PatchCategoryAsync(
        int id,
        CategoryDto dto,
        CancellationToken token = default);

    Task<ApiResponse<CategoryDto>> UpdateCategoryAsync(
        int id,
        CategoryDto dto,
        CancellationToken token = default);

    Task<ApiResponse<object>> DeleteCategoryAsync(
        int id,
        CancellationToken token = default);

    // ────────────────────────── Server Instance CRUD (§8.3) ──────────────────────────

    Task<ApiListResponse<ServerDto>> GetServersAsync(
        int? categoryId = null,
        string? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default);

    Task<ApiResponse<ServerDto>> GetServerByIdAsync(
        int id,
        CancellationToken token = default);

    Task<ApiResponse<ServerDto>> CreateServerAsync(
        ServerDto dto,
        CancellationToken token = default);

    Task<ApiResponse<ServerDto>> PatchServerAsync(
        int id,
        ServerDto dto,
        CancellationToken token = default);

    Task<ApiResponse<ServerDto>> UpdateServerAsync(
        int id,
        ServerDto dto,
        CancellationToken token = default);

    Task<ApiResponse<object>> DeleteServerAsync(
        int id,
        CancellationToken token = default);

    // ────────────────────────── Server Metrics (§8.6) ──────────────────────────

    Task<ApiResponse<ServerMetricDto>> CreateServerMetricAsync(
        int serverId,
        ServerMetricDto dto,
        CancellationToken token = default);

    Task<ApiListResponse<ServerMetricDto>> GetServerMetricsAsync(
        int serverId,
        string? startDate = null,
        string? endDate = null,
        int limit = 100,
        CancellationToken token = default);

    Task<ApiResponse<ServerMetricLatestDto>> GetServerMetricLatestAsync(
        int serverId,
        CancellationToken token = default);

    Task<ApiResponse<MetricDeleteResultDto>> DeleteServerMetricsAsync(
        int serverId,
        string? beforeDate = null,
        CancellationToken token = default);

    // ────────────────────────── Proxy Settings (§8.8) ──────────────────────────

    Task<ApiResponse<ProxySettingDto>> GetProxySettingsAsync(
        int serverId,
        CancellationToken token = default);
}
