using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Events;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;

/// <summary>
/// Event Provider Service Implementation
/// <para>GOP API를 통한 Event 데이터 Fetching 및 변환</para>
/// <para>TDD 방식으로 구현되었으며, 각 메서드는 독립적으로 테스트 가능합니다.</para>
/// </summary>
public class EventProviderService
{
    private readonly ILogService? _log;
    private readonly IEventApiService _apiService;

    public EventProviderService(
        ILogService? logService,
        IEventApiService apiService)
    {
        _log = logService;
        _apiService = apiService;
    }

    /// <summary>
    /// GOP API를 통해 Detection Event 목록을 조회하고 Model로 변환합니다.
    /// </summary>
    public async Task<List<IDetectionEventModel>> FetchDetectionEventsAsync(
        CancellationToken token = default)
    {
        var allEvents = new List<IDetectionEventModel>();
        int currentPage = 1;
        int pageSize = 100;

        try
        {
            _log?.Info("FetchDetectionEventsAsync() started");

            while (true)
            {
                var response = await _apiService.GetDetectionEventsAsync(
                    page: currentPage,
                    limit: pageSize,
                    token: token);

                if (!response.Success || response.Data == null || response.Data.Count == 0)
                {
                    if (!response.Success)
                        _log?.Error($"Failed to fetch detection events at page {currentPage}: {response.Error?.Message}");
                    break;
                }

                // DTO → Model 변환 using DtoToModelHelper
                var models = response.Data
                    .Select(dto => dto.ToDetectionEventModel())
                    .ToList();

                allEvents.AddRange(models);

                _log?.Info($"Fetched page {currentPage}: {models.Count} detection events");

                // 더 이상 데이터가 없으면 종료
                if (response.Pagination == null ||
                    currentPage >= response.Pagination.TotalPages)
                    break;

                currentPage++;
            }

            _log?.Info($"FetchDetectionEventsAsync() completed: {allEvents.Count} total events");
            return allEvents;
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchDetectionEventsAsync() failed: {ex.Message}");
            throw;
        }
    }
}
