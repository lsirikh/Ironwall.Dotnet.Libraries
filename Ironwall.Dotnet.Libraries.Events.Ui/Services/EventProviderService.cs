using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
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

    /// <summary>
    /// GOP API를 통해 Malfunction Event 목록을 조회하고 Model로 변환합니다.
    /// </summary>
    public async Task<List<IMalfunctionEventModel>> FetchMalfunctionEventsAsync(
        CancellationToken token = default)
    {
        var allEvents = new List<IMalfunctionEventModel>();
        int currentPage = 1;
        int pageSize = 100;

        try
        {
            _log?.Info("FetchMalfunctionEventsAsync() started");

            while (true)
            {
                var response = await _apiService.GetMalfunctionEventsAsync(
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
                    .Select(dto => dto.ToMalfunctionEventModel())
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
    public async Task<List<IConnectionEventModel>> FetchConnectionEventsAsync(
        CancellationToken token = default)
    {
        var allEvents = new List<IConnectionEventModel>();
        int currentPage = 1;
        int pageSize = 100;

        try
        {
            _log?.Info("FetchConnectionEventsAsync() started");

            while (true)
            {
                var response = await _apiService.GetConnectionEventsAsync(
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
                    .Select(dto => dto.ToConnectionEventModel())
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
    public async Task<List<IActionEventModel>> FetchActionEventsAsync(
        CancellationToken token = default)
    {
        var allEvents = new List<IActionEventModel>();
        int currentPage = 1;
        int pageSize = 100;

        try
        {
            _log?.Info("FetchActionEventsAsync() started");

            while (true)
            {
                var response = await _apiService.GetActionEventsAsync(
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
                    .Select(dto => dto.ToActionEventModel())
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

            var dto = model.ToDetectionEventDto();
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

            var dto = model.ToMalfunctionEventDto();
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

            var dto = model.ToConnectionEventDto();
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
                FromEvent = model.OriginEvent?.Id ?? 0,
                FromEventType = model.OriginEvent?.MessageType.ToString() ?? string.Empty
            };

            var response = await _apiService.CreateActionEventAsync(createDto, token);

            if (!response.Success || response.Data == null)
            {
                var errorMsg = $"Failed to create action event: {response.Error?.Message}";
                _log?.Error(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            var createdModel = response.Data.ToActionEventModel();
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

            var dto = model.ToActionEventDto();
            var response = await _apiService.UpdateActionEventAsync(model.Id, dto, token);

            if (!response.Success || response.Data == null)
            {
                var errorMsg = $"Failed to update action event ID {model.Id}: {response.Error?.Message}";
                _log?.Error(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            var updatedModel = response.Data.ToActionEventModel();
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
}
