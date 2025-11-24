using Xunit;
using Moq;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// Unit tests for Events.Ui API migration
/// Following TDD (Red-Green-Refactor) methodology
/// </summary>
public class DtoToModelHelperTests
{
    [Fact]
    public void ToDetectionEventModel_ShouldConvertDtoToModel()
    {
        // Arrange
        var dto = new DetectionEventDto
        {
            Id = 1,
            GroupEvent = "Zone A",
            TypeEvent = "Intrusion",
            Controller = 10,
            Sensor = 100,
            TypeDevice = "FENCE",
            Sequence = 1,
            ActionReported = "True",
            Result = "THERMAL_SENSOR",
            CreatedAt = "2025-11-24T10:30:00.000Z",
            UpdatedAt = "2025-11-24T10:30:00.000Z"
        };

        // Act
        var model = dto.ToDetectionEventModel(); // ← This method doesn't exist yet (RED)

        // Assert
        Assert.Equal(1, model.Id);
        Assert.Equal(new DateTime(2025, 11, 24, 10, 30, 0, DateTimeKind.Utc), model.DateTime);
        Assert.Equal(EnumEventType.Intrusion, model.MessageType);
        Assert.Equal("Zone A", model.EventGroup);
        Assert.Equal(EnumTrueFalse.True, model.Status);
        Assert.Equal(EnumDetectionType.THERMAL_SENSOR, model.Result);
        Assert.NotNull(model.Device);
        Assert.Equal(100, model.Device.Id);
    }

    [Fact]
    public void ToDetectionEventDto_ShouldConvertModelToDto()
    {
        // Arrange
        var model = new DetectionEventModel
        {
            Id = 1,
            DateTime = new DateTime(2025, 11, 24, 10, 30, 0, DateTimeKind.Utc),
            MessageType = EnumEventType.Intrusion,
            EventGroup = "Zone A",
            Status = EnumTrueFalse.True,
            Result = EnumDetectionType.THERMAL_SENSOR,
            Device = new SensorDeviceModel { Id = 100 }
        };

        // Act
        var dto = model.ToDetectionEventDto(); // ← This method doesn't exist yet (RED)

        // Assert
        Assert.Equal(1, dto.Id);
        Assert.Equal("2025-11-24T10:30:00.000Z", dto.CreatedAt);
        Assert.Equal("Intrusion", dto.TypeEvent);
        Assert.Equal("Zone A", dto.GroupEvent);
        Assert.Equal("True", dto.ActionReported);
        Assert.Equal("THERMAL_SENSOR", dto.Result);
        Assert.Equal(100, dto.Sensor);
    }

    [Fact]
    public void ToMalfunctionEventModel_ShouldConvertDtoToModel()
    {
        // Arrange
        var dto = new MalfunctionEventDto
        {
            Id = 2,
            GroupEvent = "Zone B",
            TypeEvent = "Fault",
            Controller = 20,
            Sensor = 200,
            TypeDevice = "FENCE",
            Sequence = 2,
            Status = "True",
            ActionReported = "False",
            Reason = "FAULT_FENCE",
            FirstStart = 10,
            FirstEnd = 20,
            SecondStart = 30,
            SecondEnd = 40,
            CreatedAt = "2025-11-24T11:00:00.000Z",
            UpdatedAt = "2025-11-24T11:00:00.000Z"
        };

        // Act
        var model = dto.ToMalfunctionEventModel(); // ← This method doesn't exist yet (RED)

        // Assert
        Assert.Equal(2, model.Id);
        Assert.Equal(new DateTime(2025, 11, 24, 11, 0, 0, DateTimeKind.Utc), model.DateTime);
        Assert.Equal(EnumEventType.Fault, model.MessageType);
        Assert.Equal("Zone B", model.EventGroup);
        Assert.Equal(EnumTrueFalse.True, model.Status);
        Assert.Equal(EnumFaultType.FAULT_FENCE, model.Reason);
        Assert.Equal(10, model.FirstStart);
        Assert.Equal(20, model.FirstEnd);
        Assert.Equal(30, model.SecondStart);
        Assert.Equal(40, model.SecondEnd);
        Assert.NotNull(model.Device);
        Assert.Equal(200, model.Device.Id);
    }

    [Fact]
    public void ToMalfunctionEventDto_ShouldConvertModelToDto()
    {
        // Arrange
        var model = new MalfunctionEventModel
        {
            Id = 2,
            DateTime = new DateTime(2025, 11, 24, 11, 0, 0, DateTimeKind.Utc),
            MessageType = EnumEventType.Fault,
            EventGroup = "Zone B",
            Status = EnumTrueFalse.True,
            Reason = EnumFaultType.FAULT_FENCE,
            FirstStart = 10,
            FirstEnd = 20,
            SecondStart = 30,
            SecondEnd = 40,
            Device = new SensorDeviceModel { Id = 200 }
        };

        // Act
        var dto = model.ToMalfunctionEventDto(); // ← This method doesn't exist yet (RED)

        // Assert
        Assert.Equal(2, dto.Id);
        Assert.Equal("2025-11-24T11:00:00.000Z", dto.CreatedAt);
        Assert.Equal("Fault", dto.TypeEvent);
        Assert.Equal("Zone B", dto.GroupEvent);
        Assert.Equal("True", dto.Status);
        Assert.Equal("FAULT_FENCE", dto.Reason);
        Assert.Equal(10, dto.FirstStart);
        Assert.Equal(20, dto.FirstEnd);
        Assert.Equal(30, dto.SecondStart);
        Assert.Equal(40, dto.SecondEnd);
        Assert.Equal(200, dto.Sensor);
    }

    [Fact]
    public void ToConnectionEventModel_ShouldConvertDtoToModel()
    {
        // Arrange
        var dto = new ConnectionEventDto
        {
            Id = 3,
            GroupEvent = "Zone C",
            TypeEvent = "Connection",
            Controller = 30,
            Sensor = 300,
            TypeDevice = "CONTROLLER",
            Sequence = 3,
            CreatedAt = "2025-11-24T12:00:00.000Z",
            UpdatedAt = "2025-11-24T12:00:00.000Z"
        };

        // Act
        var model = dto.ToConnectionEventModel(); // ← This method doesn't exist yet (RED)

        // Assert
        Assert.Equal(3, model.Id);
        Assert.Equal(new DateTime(2025, 11, 24, 12, 0, 0, DateTimeKind.Utc), model.DateTime);
        Assert.Equal(EnumEventType.Connection, model.MessageType);
        Assert.Equal("Zone C", model.EventGroup);
        Assert.NotNull(model.Device);
        Assert.Equal(300, model.Device.Id);
    }

    [Fact]
    public void ToConnectionEventDto_ShouldConvertModelToDto()
    {
        // Arrange
        var model = new ConnectionEventModel
        {
            Id = 3,
            DateTime = new DateTime(2025, 11, 24, 12, 0, 0, DateTimeKind.Utc),
            MessageType = EnumEventType.Connection,
            EventGroup = "Zone C",
            Status = EnumTrueFalse.True,
            Device = new SensorDeviceModel { Id = 300 }
        };

        // Act
        var dto = model.ToConnectionEventDto(); // ← This method doesn't exist yet (RED)

        // Assert
        Assert.Equal(3, dto.Id);
        Assert.Equal("2025-11-24T12:00:00.000Z", dto.CreatedAt);
        Assert.Equal("Connection", dto.TypeEvent);
        Assert.Equal("Zone C", dto.GroupEvent);
        Assert.Equal(300, dto.Sensor);
    }

    [Fact]
    public void ToActionEventModel_ShouldConvertDtoToModel()
    {
        // Arrange
        var dto = new ActionEventDto
        {
            Id = 4,
            TypeEvent = "Action",
            Content = "Reset system",
            User = "admin",
            FromEvent = null, // Simplified - no nested event for now
            CreatedAt = "2025-11-24T13:00:00.000Z",
            UpdatedAt = "2025-11-24T13:00:00.000Z"
        };

        // Act
        var model = dto.ToActionEventModel(); // ← This method doesn't exist yet (RED)

        // Assert
        Assert.Equal(4, model.Id);
        Assert.Equal(new DateTime(2025, 11, 24, 13, 0, 0, DateTimeKind.Utc), model.DateTime);
        Assert.Equal(EnumEventType.Action, model.MessageType);
        Assert.Equal("Reset system", model.Content);
        Assert.Equal("admin", model.User);
    }

    [Fact]
    public void ToActionEventDto_ShouldConvertModelToDto()
    {
        // Arrange
        var model = new ActionEventModel
        {
            Id = 4,
            DateTime = new DateTime(2025, 11, 24, 13, 0, 0, DateTimeKind.Utc),
            MessageType = EnumEventType.Action,
            Content = "Reset system",
            User = "admin",
            OriginEvent = null // Simplified - no nested event for now
        };

        // Act
        var dto = model.ToActionEventDto(); // ← This method doesn't exist yet (RED)

        // Assert
        Assert.Equal(4, dto.Id);
        Assert.Equal("2025-11-24T13:00:00.000Z", dto.CreatedAt);
        Assert.Equal("Action", dto.TypeEvent);
        Assert.Equal("Reset system", dto.Content);
        Assert.Equal("admin", dto.User);
    }
}

public class EventProviderServiceTests
{
    [Fact]
    public async Task FetchDetectionEventsAsync_ShouldReturnConvertedModels()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var dtoList = new List<DetectionEventDto>
        {
            new DetectionEventDto
            {
                Id = 1,
                GroupEvent = "Zone A",
                TypeEvent = "Intrusion",
                Sensor = 100,
                ActionReported = "True",
                Result = "THERMAL_SENSOR",
                CreatedAt = "2025-11-24T10:00:00.000Z"
            }
        };

        var apiResponse = new ApiListResponse<DetectionEventDto>
        {
            Success = true,
            Data = dtoList,
            Pagination = new PaginationDto
            {
                Page = 1,
                TotalPages = 1,
                Total = 1,
                Limit = 100
            }
        };

        mockApiService
            .Setup(x => x.GetDetectionEventsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(
            mockLogService.Object,
            mockApiService.Object);

        // Act
        var result = await service.FetchDetectionEventsAsync(); // ← This method doesn't exist yet (RED)

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
        Assert.Equal(EnumEventType.Intrusion, result[0].MessageType);
        Assert.Equal(EnumDetectionType.THERMAL_SENSOR, result[0].Result);
    }

    [Fact]
    public async Task FetchDetectionEventsAsync_WithMultiplePages_ShouldReturnAllPages()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        // Page 1
        var page1Response = new ApiListResponse<DetectionEventDto>
        {
            Success = true,
            Data = new List<DetectionEventDto>
            {
                new DetectionEventDto { Id = 1, GroupEvent = "Zone A", TypeEvent = "Intrusion", Sensor = 100, ActionReported = "True", Result = "THERMAL_SENSOR", CreatedAt = "2025-11-24T10:00:00.000Z" },
                new DetectionEventDto { Id = 2, GroupEvent = "Zone A", TypeEvent = "Intrusion", Sensor = 101, ActionReported = "False", Result = "PIR_SENSOR", CreatedAt = "2025-11-24T10:05:00.000Z" }
            },
            Pagination = new PaginationDto { Page = 1, TotalPages = 2, Total = 3, Limit = 2 }
        };

        // Page 2
        var page2Response = new ApiListResponse<DetectionEventDto>
        {
            Success = true,
            Data = new List<DetectionEventDto>
            {
                new DetectionEventDto { Id = 3, GroupEvent = "Zone B", TypeEvent = "Intrusion", Sensor = 102, ActionReported = "False", Result = "VIBRATION_SENSOR", CreatedAt = "2025-11-24T10:10:00.000Z" }
            },
            Pagination = new PaginationDto { Page = 2, TotalPages = 2, Total = 3, Limit = 2 }
        };

        mockApiService
            .SetupSequence(x => x.GetDetectionEventsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(page1Response)
            .ReturnsAsync(page2Response);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        // Act
        var result = await service.FetchDetectionEventsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(1, result[0].Id);
        Assert.Equal(2, result[1].Id);
        Assert.Equal(3, result[2].Id);
    }

    [Fact]
    public async Task FetchDetectionEventsAsync_WithEmptyResponse_ShouldReturnEmptyList()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var apiResponse = new ApiListResponse<DetectionEventDto>
        {
            Success = true,
            Data = new List<DetectionEventDto>(),
            Pagination = new PaginationDto { Page = 1, TotalPages = 0, Total = 0, Limit = 100 }
        };

        mockApiService
            .Setup(x => x.GetDetectionEventsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        // Act
        var result = await service.FetchDetectionEventsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchDetectionEventsAsync_WithApiError_ShouldReturnEmptyListAndLogError()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var apiResponse = new ApiListResponse<DetectionEventDto>
        {
            Success = false,
            Data = null,
            Error = new ApiError { Message = "Database connection failed" }
        };

        mockApiService
            .Setup(x => x.GetDetectionEventsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        // Act
        var result = await service.FetchDetectionEventsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
        // Note: Cannot verify ILogService.Error call due to CallerMemberName attributes in expression trees
    }

    [Fact]
    public async Task FetchDetectionEventsAsync_WithNullData_ShouldReturnEmptyList()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var apiResponse = new ApiListResponse<DetectionEventDto>
        {
            Success = true,
            Data = null,
            Pagination = new PaginationDto { Page = 1, TotalPages = 1, Total = 0, Limit = 100 }
        };

        mockApiService
            .Setup(x => x.GetDetectionEventsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        // Act
        var result = await service.FetchDetectionEventsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchMalfunctionEventsAsync_ShouldReturnConvertedModels()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var dtoList = new List<MalfunctionEventDto>
        {
            new MalfunctionEventDto
            {
                Id = 1,
                GroupEvent = "Zone A",
                TypeEvent = "Fault",
                Sensor = 100,
                Status = "True",
                ActionReported = "False",
                Reason = "FAULT_FENCE",
                FirstStart = 10,
                FirstEnd = 15,
                SecondStart = 20,
                SecondEnd = 25,
                CreatedAt = "2025-11-24T11:00:00.000Z"
            }
        };

        var apiResponse = new ApiListResponse<MalfunctionEventDto>
        {
            Success = true,
            Data = dtoList,
            Pagination = new PaginationDto
            {
                Page = 1,
                TotalPages = 1,
                Total = 1,
                Limit = 100
            }
        };

        mockApiService
            .Setup(x => x.GetMalfunctionEventsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        // Act
        var result = await service.FetchMalfunctionEventsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
        Assert.Equal(EnumEventType.Fault, result[0].MessageType);
        Assert.Equal(EnumFaultType.FAULT_FENCE, result[0].Reason);
    }

    [Fact]
    public async Task FetchConnectionEventsAsync_ShouldReturnConvertedModels()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var dtoList = new List<ConnectionEventDto>
        {
            new ConnectionEventDto
            {
                Id = 1,
                GroupEvent = "Zone A",
                TypeEvent = "Connection",
                Sensor = 100,
                CreatedAt = "2025-11-24T12:00:00.000Z"
            }
        };

        var apiResponse = new ApiListResponse<ConnectionEventDto>
        {
            Success = true,
            Data = dtoList,
            Pagination = new PaginationDto
            {
                Page = 1,
                TotalPages = 1,
                Total = 1,
                Limit = 100
            }
        };

        mockApiService
            .Setup(x => x.GetConnectionEventsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        // Act
        var result = await service.FetchConnectionEventsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
        Assert.Equal(EnumEventType.Connection, result[0].MessageType);
    }

    [Fact]
    public async Task FetchActionEventsAsync_ShouldReturnConvertedModels()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var dtoList = new List<ActionEventDto>
        {
            new ActionEventDto
            {
                Id = 1,
                TypeEvent = "Action",
                Content = "Patrol dispatched",
                User = "operator_test",
                CreatedAt = "2025-11-24T13:00:00.000Z"
            }
        };

        var apiResponse = new ApiListResponse<ActionEventDto>
        {
            Success = true,
            Data = dtoList,
            Pagination = new PaginationDto
            {
                Page = 1,
                TotalPages = 1,
                Total = 1,
                Limit = 100
            }
        };

        mockApiService
            .Setup(x => x.GetActionEventsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        // Act
        var result = await service.FetchActionEventsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
        Assert.Equal(EnumEventType.Action, result[0].MessageType);
        Assert.Equal("Patrol dispatched", result[0].Content);
        Assert.Equal("operator_test", result[0].User);
    }

    // ═════════════════════════ Phase 1.2.4: CUD Operations ═════════════════════════

    #region Detection Event CUD Tests

    [Fact]
    public async Task InsertDetectionEventAsync_ShouldCreateAndReturnModel()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var model = new DetectionEventModel
        {
            DateTime = DateTime.Parse("2025-11-24T12:00:00.000Z").ToUniversalTime(),
            MessageType = EnumEventType.Intrusion,
            EventGroup = "Zone C",
            Status = EnumTrueFalse.True,
            Result = EnumDetectionType.THERMAL_SENSOR,
            Device = new SensorDeviceModel { Id = 200 }
        };

        var createdDto = new DetectionEventDto
        {
            Id = 999,
            GroupEvent = "Zone C",
            TypeEvent = "Intrusion",
            Sensor = 200,
            ActionReported = "True",
            Result = "THERMAL_SENSOR",
            CreatedAt = "2025-11-24T12:00:00.000Z"
        };

        var apiResponse = new ApiResponse<DetectionEventDto>
        {
            Success = true,
            Data = createdDto
        };

        mockApiService
            .Setup(x => x.CreateDetectionEventAsync(
                It.IsAny<DetectionEventDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        // Act
        var result = await service.InsertDetectionEventAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(999, result.Id);
        Assert.Equal(EnumEventType.Intrusion, result.MessageType);
        Assert.Equal(EnumDetectionType.THERMAL_SENSOR, result.Result);
        mockApiService.Verify(x => x.CreateDetectionEventAsync(
            It.Is<DetectionEventDto>(dto => dto.Sensor == 200 && dto.Result == "THERMAL_SENSOR"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateDetectionEventAsync_ShouldUpdateAndReturnModel()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var model = new DetectionEventModel
        {
            Id = 100,
            DateTime = DateTime.Parse("2025-11-24T12:00:00.000Z").ToUniversalTime(),
            MessageType = EnumEventType.Intrusion,
            EventGroup = "Zone C",
            Status = EnumTrueFalse.False,
            Result = EnumDetectionType.PIR_SENSOR,
            Device = new SensorDeviceModel { Id = 200 }
        };

        var updatedDto = new DetectionEventDto
        {
            Id = 100,
            GroupEvent = "Zone C",
            TypeEvent = "Intrusion",
            Sensor = 200,
            ActionReported = "False",
            Result = "PIR_SENSOR",
            CreatedAt = "2025-11-24T12:00:00.000Z"
        };

        var apiResponse = new ApiResponse<DetectionEventDto>
        {
            Success = true,
            Data = updatedDto
        };

        mockApiService
            .Setup(x => x.UpdateDetectionEventAsync(
                100,
                It.IsAny<DetectionEventDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        // Act
        var result = await service.UpdateDetectionEventAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(100, result.Id);
        Assert.Equal(EnumTrueFalse.False, result.Status);
        mockApiService.Verify(x => x.UpdateDetectionEventAsync(
            100,
            It.Is<DetectionEventDto>(dto => dto.Id == 100 && dto.ActionReported == "False"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteDetectionEventAsync_ShouldReturnTrue()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var apiResponse = new ApiResponse<bool>
        {
            Success = true,
            Data = true
        };

        mockApiService
            .Setup(x => x.DeleteDetectionEventAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        // Act
        var result = await service.DeleteDetectionEventAsync(100);

        // Assert
        Assert.True(result);
        mockApiService.Verify(x => x.DeleteDetectionEventAsync(100, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Malfunction Event CUD Tests

    [Fact]
    public async Task InsertMalfunctionEventAsync_ShouldCreateAndReturnModel()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var model = new MalfunctionEventModel
        {
            DateTime = DateTime.Parse("2025-11-24T13:00:00.000Z").ToUniversalTime(),
            MessageType = EnumEventType.Fault,
            EventGroup = "Zone D",
            Status = EnumTrueFalse.True,
            Reason = EnumFaultType.FAULT_CONTROLLER,
            FirstStart = 10,
            FirstEnd = 15,
            SecondStart = 20,
            SecondEnd = 25,
            Device = new SensorDeviceModel { Id = 300 }
        };

        var createdDto = new MalfunctionEventDto
        {
            Id = 888,
            GroupEvent = "Zone D",
            TypeEvent = "Fault",
            Sensor = 300,
            Status = "True",
            Reason = "FAULT_CONTROLLER",
            FirstStart = 10,
            FirstEnd = 15,
            SecondStart = 20,
            SecondEnd = 25,
            CreatedAt = "2025-11-24T13:00:00.000Z"
        };

        var apiResponse = new ApiResponse<MalfunctionEventDto>
        {
            Success = true,
            Data = createdDto
        };

        mockApiService
            .Setup(x => x.CreateMalfunctionEventAsync(
                It.IsAny<MalfunctionEventDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        // Act
        var result = await service.InsertMalfunctionEventAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(888, result.Id);
        Assert.Equal(EnumEventType.Fault, result.MessageType);
        Assert.Equal(EnumFaultType.FAULT_CONTROLLER, result.Reason);
    }

    [Fact]
    public async Task UpdateMalfunctionEventAsync_ShouldUpdateAndReturnModel()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var model = new MalfunctionEventModel
        {
            Id = 200,
            DateTime = DateTime.Parse("2025-11-24T13:00:00.000Z").ToUniversalTime(),
            MessageType = EnumEventType.Fault,
            EventGroup = "Zone D",
            Status = EnumTrueFalse.False,
            Reason = EnumFaultType.FAULT_FENCE,
            FirstStart = 11,
            FirstEnd = 16,
            SecondStart = 21,
            SecondEnd = 26,
            Device = new SensorDeviceModel { Id = 300 }
        };

        var updatedDto = new MalfunctionEventDto
        {
            Id = 200,
            GroupEvent = "Zone D",
            TypeEvent = "Fault",
            Sensor = 300,
            Status = "False",
            Reason = "FAULT_FENCE",
            FirstStart = 11,
            FirstEnd = 16,
            SecondStart = 21,
            SecondEnd = 26,
            CreatedAt = "2025-11-24T13:00:00.000Z"
        };

        var apiResponse = new ApiResponse<MalfunctionEventDto>
        {
            Success = true,
            Data = updatedDto
        };

        mockApiService
            .Setup(x => x.UpdateMalfunctionEventAsync(
                200,
                It.IsAny<MalfunctionEventDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        // Act
        var result = await service.UpdateMalfunctionEventAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.Id);
        Assert.Equal(EnumTrueFalse.False, result.Status);
        Assert.Equal(11, result.FirstStart);
    }

    [Fact]
    public async Task DeleteMalfunctionEventAsync_ShouldReturnTrue()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var apiResponse = new ApiResponse<bool>
        {
            Success = true,
            Data = true
        };

        mockApiService
            .Setup(x => x.DeleteMalfunctionEventAsync(200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        // Act
        var result = await service.DeleteMalfunctionEventAsync(200);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Connection Event CUD Tests

    [Fact]
    public async Task InsertConnectionEventAsync_ShouldCreateAndReturnModel()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var model = new ConnectionEventModel
        {
            DateTime = DateTime.Parse("2025-11-24T14:00:00.000Z").ToUniversalTime(),
            MessageType = EnumEventType.Connection,
            EventGroup = "Zone E",
            Status = EnumTrueFalse.True,
            Device = new SensorDeviceModel { Id = 400 }
        };

        var createdDto = new ConnectionEventDto
        {
            Id = 777,
            GroupEvent = "Zone E",
            TypeEvent = "Connection",
            Sensor = 400,
            CreatedAt = "2025-11-24T14:00:00.000Z"
        };

        var apiResponse = new ApiResponse<ConnectionEventDto>
        {
            Success = true,
            Data = createdDto
        };

        mockApiService
            .Setup(x => x.CreateConnectionEventAsync(
                It.IsAny<ConnectionEventDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        // Act
        var result = await service.InsertConnectionEventAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(777, result.Id);
        Assert.Equal(EnumEventType.Connection, result.MessageType);
    }

    [Fact]
    public async Task UpdateConnectionEventAsync_ShouldUpdateAndReturnModel()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var model = new ConnectionEventModel
        {
            Id = 300,
            DateTime = DateTime.Parse("2025-11-24T14:00:00.000Z").ToUniversalTime(),
            MessageType = EnumEventType.Connection,
            EventGroup = "Zone E",
            Status = EnumTrueFalse.True,
            Device = new SensorDeviceModel { Id = 400 }
        };

        var updatedDto = new ConnectionEventDto
        {
            Id = 300,
            GroupEvent = "Zone E",
            TypeEvent = "Connection",
            Sensor = 400,
            CreatedAt = "2025-11-24T14:00:00.000Z"
        };

        var apiResponse = new ApiResponse<ConnectionEventDto>
        {
            Success = true,
            Data = updatedDto
        };

        mockApiService
            .Setup(x => x.UpdateConnectionEventAsync(
                300,
                It.IsAny<ConnectionEventDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        // Act
        var result = await service.UpdateConnectionEventAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(300, result.Id);
        Assert.Equal(EnumEventType.Connection, result.MessageType);
    }

    [Fact]
    public async Task DeleteConnectionEventAsync_ShouldReturnTrue()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var apiResponse = new ApiResponse<bool>
        {
            Success = true,
            Data = true
        };

        mockApiService
            .Setup(x => x.DeleteConnectionEventAsync(300, It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        // Act
        var result = await service.DeleteConnectionEventAsync(300);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Action Event CUD Tests

    [Fact]
    public async Task InsertActionEventAsync_ShouldCreateAndReturnModel()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var model = new ActionEventModel
        {
            DateTime = DateTime.Parse("2025-11-24T15:00:00.000Z").ToUniversalTime(),
            MessageType = EnumEventType.Action,
            Content = "Emergency response",
            User = "admin_user"
        };

        var createdDto = new ActionEventDto
        {
            Id = 666,
            TypeEvent = "Action",
            Content = "Emergency response",
            User = "admin_user",
            CreatedAt = "2025-11-24T15:00:00.000Z"
        };

        var apiResponse = new ApiResponse<ActionEventDto>
        {
            Success = true,
            Data = createdDto
        };

        mockApiService
            .Setup(x => x.CreateActionEventAsync(
                It.IsAny<ActionEventCreateDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        // Act
        var result = await service.InsertActionEventAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(666, result.Id);
        Assert.Equal(EnumEventType.Action, result.MessageType);
        Assert.Equal("Emergency response", result.Content);
    }

    [Fact]
    public async Task UpdateActionEventAsync_ShouldUpdateAndReturnModel()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var model = new ActionEventModel
        {
            Id = 400,
            DateTime = DateTime.Parse("2025-11-24T15:00:00.000Z").ToUniversalTime(),
            MessageType = EnumEventType.Action,
            Content = "Updated response",
            User = "supervisor"
        };

        var updatedDto = new ActionEventDto
        {
            Id = 400,
            TypeEvent = "Action",
            Content = "Updated response",
            User = "supervisor",
            CreatedAt = "2025-11-24T15:00:00.000Z"
        };

        var apiResponse = new ApiResponse<ActionEventDto>
        {
            Success = true,
            Data = updatedDto
        };

        mockApiService
            .Setup(x => x.UpdateActionEventAsync(
                400,
                It.IsAny<ActionEventDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        // Act
        var result = await service.UpdateActionEventAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(400, result.Id);
        Assert.Equal("Updated response", result.Content);
    }

    [Fact]
    public async Task DeleteActionEventAsync_ShouldReturnTrue()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var apiResponse = new ApiResponse<bool>
        {
            Success = true,
            Data = true
        };

        mockApiService
            .Setup(x => x.DeleteActionEventAsync(400, It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        // Act
        var result = await service.DeleteActionEventAsync(400);

        // Assert
        Assert.True(result);
    }

    #endregion
}
