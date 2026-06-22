using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Ironwall.Dotnet.Libraries.Devices.Providers;

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
    private readonly DeviceProvider? _deviceProvider;
    private readonly Ironwall.Dotnet.Libraries.Events.Providers.EventProvider? _eventProvider;

    public EventProviderService(ILogService? logService,
                                IEventApiService apiService,
                                DeviceProvider? deviceProvider = null,
                                Ironwall.Dotnet.Libraries.Events.Providers.EventProvider? eventProvider = null)
    {
        _log = logService;
        _apiService = apiService;
        _deviceProvider = deviceProvider;
        _eventProvider = eventProvider;
    }

    /// <summary>
    /// GOP API를 통해 Detection Event 목록을 조회하고 Model로 변환합니다.
    /// </summary>
    /// <param name="startDate">시작 날짜 (필수)</param>
    /// <param name="endDate">종료 날짜 (필수)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    public async Task<List<IDetectionEventModel>> FetchDetectionEventsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken token = default)
    {
        var allEvents = new List<IDetectionEventModel>();
        int currentPage = 1;
        int pageSize = 100;

        try
        {
            _log?.Info($"FetchDetectionEventsAsync() started: {startDate:yyyy-MM-dd} ~ {endDate:yyyy-MM-dd}");

            while (true)
            {
                var response = await _apiService.GetDetectionEventsAsync(
                    startDate: startDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                    endDate: endDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                    page: currentPage,
                    limit: pageSize,
                    token: token);

                if (!response.Success || response.Data == null || response.Data.Count == 0)
                {
                    if (!response.Success)
                        _log?.Error($"Failed to fetch detection events at page {currentPage}: {response.Error?.Message}");
                    break;
                }

                // DTO → Model 변환 using DtoToModelHelper (DeviceProvider 전달)
                var models = response.Data
                    .Select(dto => dto.ToDetectionEventModel(_deviceProvider))
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

    /// <summary>
    /// GOP API를 통해 Malfunction Event 목록을 조회하고 Model로 변환합니다.
    /// </summary>
    /// <param name="startDate">시작 날짜 (필수)</param>
    /// <param name="endDate">종료 날짜 (필수)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    public async Task<List<IMalfunctionEventModel>> FetchMalfunctionEventsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken token = default)
    {
        var allEvents = new List<IMalfunctionEventModel>();
        int currentPage = 1;
        int pageSize = 100;

        try
        {
            _log?.Info($"FetchMalfunctionEventsAsync() started: {startDate:yyyy-MM-dd} ~ {endDate:yyyy-MM-dd}");

            while (true)
            {
                var response = await _apiService.GetMalfunctionEventsAsync(
                    startDate: startDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                    endDate: endDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                    page: currentPage,
                    limit: pageSize,
                    token: token);

                if (!response.Success || response.Data == null || response.Data.Count == 0)
                {
                    if (!response.Success)
                        _log?.Error($"Failed to fetch malfunction events at page {currentPage}: {response.Error?.Message}");
                    break;
                }

                var models = response.Data
                    .Select(dto => dto.ToMalfunctionEventModel(_deviceProvider))
                    .ToList();

                allEvents.AddRange(models);

                _log?.Info($"Fetched page {currentPage}: {models.Count} malfunction events");

                if (response.Pagination == null ||
                    currentPage >= response.Pagination.TotalPages)
                    break;

                currentPage++;
            }

            _log?.Info($"FetchMalfunctionEventsAsync() completed: {allEvents.Count} total events");
            return allEvents;
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchMalfunctionEventsAsync() failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// GOP API를 통해 Connection Event 목록을 조회하고 Model로 변환합니다.
    /// </summary>
    /// <param name="startDate">시작 날짜 (필수)</param>
    /// <param name="endDate">종료 날짜 (필수)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    public async Task<List<IConnectionEventModel>> FetchConnectionEventsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken token = default)
    {
        var allEvents = new List<IConnectionEventModel>();
        int currentPage = 1;
        int pageSize = 100;

        try
        {
            _log?.Info($"FetchConnectionEventsAsync() started: {startDate:yyyy-MM-dd} ~ {endDate:yyyy-MM-dd}");

            while (true)
            {
                var response = await _apiService.GetConnectionEventsAsync(
                    startDate: startDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                    endDate: endDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                    page: currentPage,
                    limit: pageSize,
                    token: token);

                if (!response.Success || response.Data == null || response.Data.Count == 0)
                {
                    if (!response.Success)
                        _log?.Error($"Failed to fetch connection events at page {currentPage}: {response.Error?.Message}");
                    break;
                }

                var models = response.Data
                    .Select(dto => dto.ToConnectionEventModel(_deviceProvider))
                    .ToList();

                allEvents.AddRange(models);

                _log?.Info($"Fetched page {currentPage}: {models.Count} connection events");

                if (response.Pagination == null ||
                    currentPage >= response.Pagination.TotalPages)
                    break;

                currentPage++;
            }

            _log?.Info($"FetchConnectionEventsAsync() completed: {allEvents.Count} total events");
            return allEvents;
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchConnectionEventsAsync() failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// GOP API를 통해 Action Event 목록을 조회하고 Model로 변환합니다.
    /// </summary>
    /// <param name="startDate">시작 날짜 (필수)</param>
    /// <param name="endDate">종료 날짜 (필수)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    public async Task<List<IActionEventModel>> FetchActionEventsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken token = default)
    {
        var allEvents = new List<IActionEventModel>();
        int currentPage = 1;
        int pageSize = 100;

        try
        {
            _log?.Info($"FetchActionEventsAsync() started: {startDate:yyyy-MM-dd} ~ {endDate:yyyy-MM-dd}");

            while (true)
            {
                var response = await _apiService.GetActionEventsAsync(
                    startDate: startDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                    endDate: endDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                    page: currentPage,
                    limit: pageSize,
                    token: token);

                if (!response.Success || response.Data == null || response.Data.Count == 0)
                {
                    if (!response.Success)
                        _log?.Error($"Failed to fetch action events at page {currentPage}: {response.Error?.Message}");
                    break;
                }

                var models = response.Data
                    .Select(dto => dto.ToActionEventModel(_eventProvider, _deviceProvider))
                    .ToList();

                allEvents.AddRange(models);

                _log?.Info($"Fetched page {currentPage}: {models.Count} action events");

                if (response.Pagination == null ||
                    currentPage >= response.Pagination.TotalPages)
                    break;

                currentPage++;
            }

            _log?.Info($"FetchActionEventsAsync() completed: {allEvents.Count} total events");
            return allEvents;
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchActionEventsAsync() failed: {ex.Message}");
            throw;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Single Page Fetch (Infinite Scroll 용)
    // ═══════════════════════════════════════════════════════════════════════════════

    public async Task<PagedResult<IDetectionEventModel>> FetchDetectionEventsPageAsync(
        DateTime startDate, DateTime endDate,
        int page = 1, int limit = 100,
        CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetDetectionEventsAsync(
                startDate: startDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                endDate: endDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                page: page, limit: limit, token: token);

            if (!response.Success || response.Data == null || response.Data.Count == 0)
            {
                if (!response.Success)
                    _log?.Error($"FetchDetectionEventsPageAsync failed at page {page}: {response.Error?.Message}");

                return new PagedResult<IDetectionEventModel>
                {
                    // (EA2) API 실패는 Success=false(호출부 보존), 정상 빈 결과는 Success=true(클리어 정상)
                    Success = response.Success,
                    Page = page,
                    TotalPages = response.Pagination?.TotalPages ?? 1,
                    Total = response.Pagination?.Total ?? 0
                };
            }

            var models = response.Data
                .Select(dto => dto.ToDetectionEventModel(_deviceProvider))
                .ToList();

            return new PagedResult<IDetectionEventModel>
            {
                Items = models,
                Page = response.Pagination?.Page ?? page,
                TotalPages = response.Pagination?.TotalPages ?? 1,
                Total = response.Pagination?.Total ?? models.Count
            };
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchDetectionEventsPageAsync() failed: {ex.Message}");
            throw;
        }
    }

    public async Task<PagedResult<IMalfunctionEventModel>> FetchMalfunctionEventsPageAsync(
        DateTime startDate, DateTime endDate,
        int page = 1, int limit = 100,
        CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetMalfunctionEventsAsync(
                startDate: startDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                endDate: endDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                page: page, limit: limit, token: token);

            if (!response.Success || response.Data == null || response.Data.Count == 0)
            {
                if (!response.Success)
                    _log?.Error($"FetchMalfunctionEventsPageAsync failed at page {page}: {response.Error?.Message}");

                return new PagedResult<IMalfunctionEventModel>
                {
                    Success = response.Success,   // (EA2 대칭) API 실패 vs 빈 결과 구분 — swap-on-success 후속 대비
                    Page = page,
                    TotalPages = response.Pagination?.TotalPages ?? 1,
                    Total = response.Pagination?.Total ?? 0
                };
            }

            var models = response.Data
                .Select(dto => dto.ToMalfunctionEventModel(_deviceProvider))
                .ToList();

            return new PagedResult<IMalfunctionEventModel>
            {
                Items = models,
                Page = response.Pagination?.Page ?? page,
                TotalPages = response.Pagination?.TotalPages ?? 1,
                Total = response.Pagination?.Total ?? models.Count
            };
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchMalfunctionEventsPageAsync() failed: {ex.Message}");
            throw;
        }
    }

    public async Task<PagedResult<IConnectionEventModel>> FetchConnectionEventsPageAsync(
        DateTime startDate, DateTime endDate,
        int page = 1, int limit = 100,
        CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetConnectionEventsAsync(
                startDate: startDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                endDate: endDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                page: page, limit: limit, token: token);

            if (!response.Success || response.Data == null || response.Data.Count == 0)
            {
                if (!response.Success)
                    _log?.Error($"FetchConnectionEventsPageAsync failed at page {page}: {response.Error?.Message}");

                return new PagedResult<IConnectionEventModel>
                {
                    Success = response.Success,   // (EA2 대칭) API 실패 vs 빈 결과 구분 — swap-on-success 후속 대비
                    Page = page,
                    TotalPages = response.Pagination?.TotalPages ?? 1,
                    Total = response.Pagination?.Total ?? 0
                };
            }

            var models = response.Data
                .Select(dto => dto.ToConnectionEventModel(_deviceProvider))
                .ToList();

            return new PagedResult<IConnectionEventModel>
            {
                Items = models,
                Page = response.Pagination?.Page ?? page,
                TotalPages = response.Pagination?.TotalPages ?? 1,
                Total = response.Pagination?.Total ?? models.Count
            };
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchConnectionEventsPageAsync() failed: {ex.Message}");
            throw;
        }
    }

    public async Task<PagedResult<IActionEventModel>> FetchActionEventsPageAsync(
        DateTime startDate, DateTime endDate,
        int page = 1, int limit = 100,
        CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetActionEventsAsync(
                startDate: startDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                endDate: endDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                page: page, limit: limit, token: token);

            if (!response.Success || response.Data == null || response.Data.Count == 0)
            {
                if (!response.Success)
                    _log?.Error($"FetchActionEventsPageAsync failed at page {page}: [{response.Error?.Code}] {response.Error?.Message} | {response.Error?.Details}");

                return new PagedResult<IActionEventModel>
                {
                    Page = page,
                    TotalPages = response.Pagination?.TotalPages ?? 1,
                    Total = response.Pagination?.Total ?? 0
                };
            }

            _log?.Info($"FetchActionEventsPageAsync page {page}: {response.Data.Count} items (total: {response.Pagination?.Total})");

            var models = response.Data
                .Select(dto => dto.ToActionEventModel(_eventProvider, _deviceProvider))
                .ToList();

            return new PagedResult<IActionEventModel>
            {
                Items = models,
                Page = response.Pagination?.Page ?? page,
                TotalPages = response.Pagination?.TotalPages ?? 1,
                Total = response.Pagination?.Total ?? models.Count
            };
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchActionEventsPageAsync() failed: {ex.Message}");
            throw;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // CUD Operations (Create, Update, Delete)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GOP API를 통해 새로운 Detection Event를 생성합니다.
    /// </summary>
    public async Task<IDetectionEventModel> InsertDetectionEventAsync(
        IDetectionEventModel model,
        CancellationToken token = default)
    {
        try
        {
            _log?.Info($"InsertDetectionEventAsync() started for Sensor {model.Device?.Id}");

            var dto = model.ToDetectionEventDto();
            var response = await _apiService.CreateDetectionEventAsync(dto, token);

            if (!response.Success || response.Data == null)
            {
                var errorMsg = $"Failed to create detection event: {response.Error?.Message}";
                _log?.Error(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            var createdModel = response.Data.ToDetectionEventModel();
            _log?.Info($"InsertDetectionEventAsync() completed: Created ID {createdModel.Id}");
            return createdModel;
        }
        catch (Exception ex)
        {
            _log?.Error($"InsertDetectionEventAsync() failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// GOP API를 통해 기존 Detection Event를 수정합니다.
    /// </summary>
    public async Task<IDetectionEventModel> UpdateDetectionEventAsync(
        IDetectionEventModel model,
        CancellationToken token = default)
    {
        try
        {
            _log?.Info($"UpdateDetectionEventAsync() started for ID {model.Id}");

            var dto = model.ToDetectionEventReplaceDto();   // PUT은 Replace 계약(허용 필드만)
            var response = await _apiService.UpdateDetectionEventAsync(model.Id, dto, token);

            if (!response.Success || response.Data == null)
            {
                var errorMsg = $"Failed to update detection event ID {model.Id}: {response.Error?.Message}";
                _log?.Error(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            var updatedModel = response.Data.ToDetectionEventModel();
            _log?.Info($"UpdateDetectionEventAsync() completed for ID {model.Id}");
            return updatedModel;
        }
        catch (Exception ex)
        {
            _log?.Error($"UpdateDetectionEventAsync() failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// GOP API를 통해 Detection Event를 삭제합니다.
    /// </summary>
    public async Task<bool> DeleteDetectionEventAsync(
        int id,
        CancellationToken token = default)
    {
        try
        {
            _log?.Info($"DeleteDetectionEventAsync() started for ID {id}");

            var response = await _apiService.DeleteDetectionEventAsync(id, token);

            if (!response.Success)
            {
                _log?.Error($"Failed to delete detection event ID {id}: {response.Error?.Message}");
                return false;
            }

            _log?.Info($"DeleteDetectionEventAsync() completed for ID {id}");
            return response.Data;
        }
        catch (Exception ex)
        {
            _log?.Error($"DeleteDetectionEventAsync() failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// GOP API를 통해 새로운 Malfunction Event를 생성합니다.
    /// </summary>
    public async Task<IMalfunctionEventModel> InsertMalfunctionEventAsync(
        IMalfunctionEventModel model,
        CancellationToken token = default)
    {
        try
        {
            _log?.Info($"InsertMalfunctionEventAsync() started for Sensor {model.Device?.Id}");

            var dto = model.ToMalfunctionEventDto();
            var response = await _apiService.CreateMalfunctionEventAsync(dto, token);

            if (!response.Success || response.Data == null)
            {
                var errorMsg = $"Failed to create malfunction event: {response.Error?.Message}";
                _log?.Error(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            var createdModel = response.Data.ToMalfunctionEventModel();
            _log?.Info($"InsertMalfunctionEventAsync() completed: Created ID {createdModel.Id}");
            return createdModel;
        }
        catch (Exception ex)
        {
            _log?.Error($"InsertMalfunctionEventAsync() failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// GOP API를 통해 기존 Malfunction Event를 수정합니다.
    /// </summary>
    public async Task<IMalfunctionEventModel> UpdateMalfunctionEventAsync(
        IMalfunctionEventModel model,
        CancellationToken token = default)
    {
        try
        {
            _log?.Info($"UpdateMalfunctionEventAsync() started for ID {model.Id}");

            var dto = model.ToMalfunctionEventReplaceDto();   // PUT은 Replace 계약(허용 필드만)
            var response = await _apiService.UpdateMalfunctionEventAsync(model.Id, dto, token);

            if (!response.Success || response.Data == null)
            {
                var errorMsg = $"Failed to update malfunction event ID {model.Id}: {response.Error?.Message}";
                _log?.Error(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            var updatedModel = response.Data.ToMalfunctionEventModel();
            _log?.Info($"UpdateMalfunctionEventAsync() completed for ID {model.Id}");
            return updatedModel;
        }
        catch (Exception ex)
        {
            _log?.Error($"UpdateMalfunctionEventAsync() failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// GOP API를 통해 Malfunction Event를 삭제합니다.
    /// </summary>
    public async Task<bool> DeleteMalfunctionEventAsync(
        int id,
        CancellationToken token = default)
    {
        try
        {
            _log?.Info($"DeleteMalfunctionEventAsync() started for ID {id}");

            var response = await _apiService.DeleteMalfunctionEventAsync(id, token);

            if (!response.Success)
            {
                _log?.Error($"Failed to delete malfunction event ID {id}: {response.Error?.Message}");
                return false;
            }

            _log?.Info($"DeleteMalfunctionEventAsync() completed for ID {id}");
            return response.Data;
        }
        catch (Exception ex)
        {
            _log?.Error($"DeleteMalfunctionEventAsync() failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// GOP API를 통해 새로운 Connection Event를 생성합니다.
    /// </summary>
    public async Task<IConnectionEventModel> InsertConnectionEventAsync(
        IConnectionEventModel model,
        CancellationToken token = default)
    {
        try
        {
            _log?.Info($"InsertConnectionEventAsync() started for Sensor {model.Device?.Id}");

            var dto = model.ToConnectionEventDto();
            var response = await _apiService.CreateConnectionEventAsync(dto, token);

            if (!response.Success || response.Data == null)
            {
                var errorMsg = $"Failed to create connection event: {response.Error?.Message}";
                _log?.Error(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            var createdModel = response.Data.ToConnectionEventModel();
            _log?.Info($"InsertConnectionEventAsync() completed: Created ID {createdModel.Id}");
            return createdModel;
        }
        catch (Exception ex)
        {
            _log?.Error($"InsertConnectionEventAsync() failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// GOP API를 통해 기존 Connection Event를 수정합니다.
    /// </summary>
    public async Task<IConnectionEventModel> UpdateConnectionEventAsync(
        IConnectionEventModel model,
        CancellationToken token = default)
    {
        try
        {
            _log?.Info($"UpdateConnectionEventAsync() started for ID {model.Id}");

            var dto = model.ToConnectionEventReplaceDto();   // PUT은 Replace 계약(type_event만)
            var response = await _apiService.UpdateConnectionEventAsync(model.Id, dto, token);

            if (!response.Success || response.Data == null)
            {
                var errorMsg = $"Failed to update connection event ID {model.Id}: {response.Error?.Message}";
                _log?.Error(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            var updatedModel = response.Data.ToConnectionEventModel();
            _log?.Info($"UpdateConnectionEventAsync() completed for ID {model.Id}");
            return updatedModel;
        }
        catch (Exception ex)
        {
            _log?.Error($"UpdateConnectionEventAsync() failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// GOP API를 통해 Connection Event를 삭제합니다.
    /// </summary>
    public async Task<bool> DeleteConnectionEventAsync(
        int id,
        CancellationToken token = default)
    {
        try
        {
            _log?.Info($"DeleteConnectionEventAsync() started for ID {id}");

            var response = await _apiService.DeleteConnectionEventAsync(id, token);

            if (!response.Success)
            {
                _log?.Error($"Failed to delete connection event ID {id}: {response.Error?.Message}");
                return false;
            }

            _log?.Info($"DeleteConnectionEventAsync() completed for ID {id}");
            return response.Data;
        }
        catch (Exception ex)
        {
            _log?.Error($"DeleteConnectionEventAsync() failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// GOP API를 통해 새로운 Action Event를 생성합니다.
    /// </summary>
    public async Task<IActionEventModel> InsertActionEventAsync(
        IActionEventModel model,
        CancellationToken token = default)
    {
        try
        {
            _log?.Info($"InsertActionEventAsync() started by user {model.User}");

            // ActionEvent는 특별히 ActionEventCreateDto를 사용
            var createDto = new ActionEventCreateDto
            {
                TypeEvent = model.MessageType.ToString(),
                Content = model.Content ?? string.Empty,
                User = model.User ?? string.Empty,
                FromEventId = model.OriginEvent?.Id ?? 0
            };

            var response = await _apiService.CreateActionEventAsync(createDto, token);

            if (!response.Success || response.Data == null)
            {
                var errorMsg = $"Failed to create action event: {response.Error?.Message}";
                _log?.Error(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            var createdModel = response.Data.ToActionEventModel(_eventProvider, _deviceProvider);
            _log?.Info($"InsertActionEventAsync() completed: Created ID {createdModel.Id}");
            return createdModel;
        }
        catch (Exception ex)
        {
            _log?.Error($"InsertActionEventAsync() failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// GOP API를 통해 기존 Action Event를 수정합니다.
    /// </summary>
    public async Task<IActionEventModel> UpdateActionEventAsync(
        IActionEventModel model,
        CancellationToken token = default)
    {
        try
        {
            _log?.Info($"UpdateActionEventAsync() started for ID {model.Id}");

            // PUT은 ActionEventReplace 계약 — from_event_id 미포함(원본 연결은 사후 불변), content/user/type_event만
            var replaceDto = model.ToActionEventReplaceDto();
            var response = await _apiService.UpdateActionEventAsync(model.Id, replaceDto, token);

            if (!response.Success || response.Data == null)
            {
                var errorMsg = $"Failed to update action event ID {model.Id}: {response.Error?.Message}";
                _log?.Error(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            var updatedModel = response.Data.ToActionEventModel(_eventProvider, _deviceProvider);
            _log?.Info($"UpdateActionEventAsync() completed for ID {model.Id}");
            return updatedModel;
        }
        catch (Exception ex)
        {
            _log?.Error($"UpdateActionEventAsync() failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// GOP API를 통해 Action Event를 삭제합니다.
    /// </summary>
    public async Task<bool> DeleteActionEventAsync(
        int id,
        CancellationToken token = default)
    {
        try
        {
            _log?.Info($"DeleteActionEventAsync() started for ID {id}");

            var response = await _apiService.DeleteActionEventAsync(id, token);

            if (!response.Success)
            {
                _log?.Error($"Failed to delete action event ID {id}: {response.Error?.Message}");
                return false;
            }

            _log?.Info($"DeleteActionEventAsync() completed for ID {id}");
            return response.Data;
        }
        catch (Exception ex)
        {
            _log?.Error($"DeleteActionEventAsync() failed: {ex.Message}");
            throw;
        }
    }

    // ────────────────────────── Event Statistics (§6.7) ──────────────────────────

    public async Task<EventDashboardDto?> FetchEventDashboardAsync(
        DateTime startDate, DateTime endDate, string interval = "hour", CancellationToken token = default)
    {
        try
        {
            _log?.Info($"FetchEventDashboardAsync() started: {startDate:yyyy-MM-dd} ~ {endDate:yyyy-MM-dd}, interval={interval}");
            var response = await _apiService.GetEventStatisticsDashboardAsync(
                startDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                endDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                interval, token);

            if (!response.Success || response.Data == null)
            {
                _log?.Error($"FetchEventDashboardAsync() failed: {response.Error?.Message}");
                return null;
            }

            _log?.Info($"FetchEventDashboardAsync() completed: total={response.Data.Summary.Total}");
            return response.Data;
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchEventDashboardAsync() failed: {ex.Message}");
            throw;
        }
    }

    public async Task<EventSummaryDto?> FetchEventSummaryAsync(
        DateTime startDate, DateTime endDate, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetEventStatisticsSummaryAsync(
                startDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                endDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                token);

            if (!response.Success || response.Data == null)
            {
                _log?.Error($"FetchEventSummaryAsync() failed: {response.Error?.Message}");
                return null;
            }

            return response.Data;
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchEventSummaryAsync() failed: {ex.Message}");
            throw;
        }
    }

    public async Task<EventByDeviceDto?> FetchEventByDeviceAsync(
        DateTime startDate, DateTime endDate, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetEventStatisticsByDeviceAsync(
                startDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                endDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                token);

            if (!response.Success || response.Data == null)
            {
                _log?.Error($"FetchEventByDeviceAsync() failed: {response.Error?.Message}");
                return null;
            }

            return response.Data;
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchEventByDeviceAsync() failed: {ex.Message}");
            throw;
        }
    }
}
