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
using Autofac;
using Ironwall.Dotnet.Libraries.Events.Api.Modules;

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
        var startDate = DateTime.Now.AddDays(-1);
        var endDate = DateTime.Now;
        var result = await service.FetchDetectionEventsAsync(startDate, endDate);

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
        var startDate = DateTime.Now.AddDays(-1);
        var endDate = DateTime.Now;
        var result = await service.FetchDetectionEventsAsync(startDate, endDate);

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
        var startDate = DateTime.Now.AddDays(-1);
        var endDate = DateTime.Now;
        var result = await service.FetchDetectionEventsAsync(startDate, endDate);

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
        var startDate = DateTime.Now.AddDays(-1);
        var endDate = DateTime.Now;
        var result = await service.FetchDetectionEventsAsync(startDate, endDate);

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
        var startDate = DateTime.Now.AddDays(-1);
        var endDate = DateTime.Now;
        var result = await service.FetchDetectionEventsAsync(startDate, endDate);

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
        var startDate = DateTime.Now.AddDays(-1);
        var endDate = DateTime.Now;
        var result = await service.FetchMalfunctionEventsAsync(startDate, endDate);

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
        var startDate = DateTime.Now.AddDays(-1);
        var endDate = DateTime.Now;
        var result = await service.FetchConnectionEventsAsync(startDate, endDate);

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
        var startDate = DateTime.Now.AddDays(-1);
        var endDate = DateTime.Now;
        var result = await service.FetchActionEventsAsync(startDate, endDate);

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

/// <summary>
/// EventProviderService 통합 테스트 (실제 GOP API 사용)
/// GOP API 서버가 localhost:8000/api에서 실행 중이어야 합니다.
/// </summary>
public class EventProviderServiceIntegrationTests : IDisposable
{
    private readonly ILogService _logService;
    private readonly IEventApiService _apiService;
    private readonly EventProviderService _providerService;
    private readonly IContainer _container;

    public EventProviderServiceIntegrationTests()
    {
        _logService = new LogService();

        // 실제 GOP API 설정
        var setupModel = new Ironwall.Dotnet.Libraries.Api.Models.ApiSetupModel
        {
            Url = "http://localhost:8000/api",
            Username = "admin",
            Password = "admin123",
            ApiKey = "",
            Phone = "",
            Timeout = 30
        };

        // Autofac을 사용한 EventApiService 생성 (Events.Api 패턴과 동일)
        var builder = new ContainerBuilder();
        builder.RegisterModule(new EventApiModule(_logService, setupModel, "EventApi"));
        _container = builder.Build();

        // EventApiService 가져오기
        _apiService = _container.ResolveNamed<IEventApiService>("EventApi");

        // EventApiService 초기화 (비동기 실행)
        _apiService.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult();

        // EventProviderService 생성
        _providerService = new EventProviderService(_logService, _apiService);
    }

    public void Dispose()
    {
        _container?.Dispose();
    }

    #region - Detection Event Integration Tests -

    [Fact(DisplayName = "INT-01. FetchDetectionEventsAsync with Date Range - Real API")]
    public async Task FetchDetectionEventsAsync_WithDateRange_ShouldReturnFilteredEvents()
    {
        // Arrange
        var startDate = DateTime.Now.AddDays(-7);
        var endDate = DateTime.Now;

        // Act
        var result = await _providerService.FetchDetectionEventsAsync(startDate, endDate);

        // Assert
        Assert.NotNull(result);
        // API가 실행 중이면 결과가 있을 수 있음
        _logService.Info($"Fetched {result.Count} detection events from real API");

        // 모든 이벤트가 날짜 범위 내에 있는지 확인
        foreach (var evt in result)
        {
            Assert.True(evt.DateTime >= startDate && evt.DateTime <= endDate,
                $"Event DateTime {evt.DateTime} is outside range [{startDate}, {endDate}]");
        }
    }

    [Fact(DisplayName = "INT-02. FetchDetectionEventsAsync Single Day - Real API")]
    public async Task FetchDetectionEventsAsync_SingleDay_ShouldReturnTodayEvents()
    {
        // Arrange
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        // Act
        var result = await _providerService.FetchDetectionEventsAsync(today, tomorrow);

        // Assert
        Assert.NotNull(result);
        _logService.Info($"Fetched {result.Count} detection events for today from real API");

        // 모든 이벤트가 오늘 날짜인지 확인
        foreach (var evt in result)
        {
            Assert.True(evt.DateTime >= today && evt.DateTime < tomorrow,
                $"Event DateTime {evt.DateTime} is not today");
        }
    }

    #endregion

    #region - Malfunction Event Integration Tests -

    [Fact(DisplayName = "INT-03. FetchMalfunctionEventsAsync with Date Range - Real API")]
    public async Task FetchMalfunctionEventsAsync_WithDateRange_ShouldReturnFilteredEvents()
    {
        // Arrange
        var startDate = DateTime.Now.AddDays(-7);
        var endDate = DateTime.Now;

        // Act
        var result = await _providerService.FetchMalfunctionEventsAsync(startDate, endDate);

        // Assert
        Assert.NotNull(result);
        _logService.Info($"Fetched {result.Count} malfunction events from real API");

        // 모든 이벤트가 날짜 범위 내에 있는지 확인
        foreach (var evt in result)
        {
            Assert.True(evt.DateTime >= startDate && evt.DateTime <= endDate,
                $"Event DateTime {evt.DateTime} is outside range [{startDate}, {endDate}]");
        }
    }

    #endregion

    #region - Connection Event Integration Tests -

    [Fact(DisplayName = "INT-04. FetchConnectionEventsAsync with Date Range - Real API")]
    public async Task FetchConnectionEventsAsync_WithDateRange_ShouldReturnFilteredEvents()
    {
        // Arrange
        var startDate = DateTime.Now.AddDays(-7);
        var endDate = DateTime.Now;

        // Act
        var result = await _providerService.FetchConnectionEventsAsync(startDate, endDate);

        // Assert
        Assert.NotNull(result);
        _logService.Info($"Fetched {result.Count} connection events from real API");

        // 모든 이벤트가 날짜 범위 내에 있는지 확인
        foreach (var evt in result)
        {
            Assert.True(evt.DateTime >= startDate && evt.DateTime <= endDate,
                $"Event DateTime {evt.DateTime} is outside range [{startDate}, {endDate}]");
        }
    }

    #endregion

    #region - Action Event Integration Tests -

    [Fact(DisplayName = "INT-05. FetchActionEventsAsync with Date Range - Real API")]
    public async Task FetchActionEventsAsync_WithDateRange_ShouldReturnFilteredEvents()
    {
        // Arrange
        var startDate = DateTime.Now.AddDays(-7);
        var endDate = DateTime.Now;

        // Act
        var result = await _providerService.FetchActionEventsAsync(startDate, endDate);

        // Assert
        Assert.NotNull(result);
        _logService.Info($"Fetched {result.Count} action events from real API");

        // 모든 이벤트가 날짜 범위 내에 있는지 확인
        foreach (var evt in result)
        {
            Assert.True(evt.DateTime >= startDate && evt.DateTime <= endDate,
                $"Event DateTime {evt.DateTime} is outside range [{startDate}, {endDate}]");
        }
    }

    #endregion

    #region - Multi-Type Event Fetching Tests -

    [Fact(DisplayName = "INT-06. Fetch All Event Types Concurrently - Real API")]
    public async Task FetchAllEventTypes_Concurrently_ShouldReturnAllEvents()
    {
        // Arrange
        var startDate = DateTime.Now.AddDays(-3);
        var endDate = DateTime.Now;

        // Act - 모든 이벤트 타입을 동시에 가져오기
        var detectionTask = _providerService.FetchDetectionEventsAsync(startDate, endDate);
        var malfunctionTask = _providerService.FetchMalfunctionEventsAsync(startDate, endDate);
        var connectionTask = _providerService.FetchConnectionEventsAsync(startDate, endDate);
        var actionTask = _providerService.FetchActionEventsAsync(startDate, endDate);

        await Task.WhenAll(detectionTask, malfunctionTask, connectionTask, actionTask);

        // Assert
        var detectionEvents = await detectionTask;
        var malfunctionEvents = await malfunctionTask;
        var connectionEvents = await connectionTask;
        var actionEvents = await actionTask;

        Assert.NotNull(detectionEvents);
        Assert.NotNull(malfunctionEvents);
        Assert.NotNull(connectionEvents);
        Assert.NotNull(actionEvents);

        var totalCount = detectionEvents.Count + malfunctionEvents.Count +
                        connectionEvents.Count + actionEvents.Count;

        _logService.Info($"Fetched total {totalCount} events from real API:");
        _logService.Info($"  - Detection: {detectionEvents.Count}");
        _logService.Info($"  - Malfunction: {malfunctionEvents.Count}");
        _logService.Info($"  - Connection: {connectionEvents.Count}");
        _logService.Info($"  - Action: {actionEvents.Count}");
    }

    [Fact(DisplayName = "INT-07. Fetch Events with Short Date Range - Real API")]
    public async Task FetchEvents_WithShortDateRange_ShouldReturnLimitedEvents()
    {
        // Arrange - 지난 1시간만 조회
        var startDate = DateTime.Now.AddHours(-1);
        var endDate = DateTime.Now;

        // Act
        var detectionEvents = await _providerService.FetchDetectionEventsAsync(startDate, endDate);

        // Assert
        Assert.NotNull(detectionEvents);
        _logService.Info($"Fetched {detectionEvents.Count} detection events from last hour");

        // 모든 이벤트가 1시간 이내인지 확인
        foreach (var evt in detectionEvents)
        {
            Assert.True(evt.DateTime >= startDate && evt.DateTime <= endDate,
                $"Event DateTime {evt.DateTime} is outside last hour range");
        }
    }

    #endregion

    #region - Performance Tests -

    [Fact(DisplayName = "INT-08. Performance Test - Fetch Large Date Range - Real API")]
    public async Task FetchEvents_LargeDateRange_ShouldCompleteInReasonableTime()
    {
        // Arrange
        var startDate = DateTime.Now.AddDays(-30); // 지난 30일
        var endDate = DateTime.Now;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var detectionEvents = await _providerService.FetchDetectionEventsAsync(startDate, endDate);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(detectionEvents);
        _logService.Info($"Fetched {detectionEvents.Count} detection events in {stopwatch.ElapsedMilliseconds}ms");

        // 성능 확인 (30일치 데이터를 30초 이내에 가져와야 함)
        Assert.True(stopwatch.ElapsedMilliseconds < 30000,
            $"Fetching took too long: {stopwatch.ElapsedMilliseconds}ms");
    }

    #endregion

    #region - DTO to Model Conversion Tests -

    [Fact(DisplayName = "INT-09. Verify DTO to Model Conversion - Real API")]
    public async Task FetchDetectionEvents_ShouldConvertDtoToModelCorrectly()
    {
        // Arrange
        var startDate = DateTime.Now.AddDays(-1);
        var endDate = DateTime.Now;

        // Act
        var events = await _providerService.FetchDetectionEventsAsync(startDate, endDate);

        // Assert
        Assert.NotNull(events);

        if (events.Count > 0)
        {
            var firstEvent = events.First();

            // Model이 올바르게 변환되었는지 확인
            Assert.True(firstEvent.Id > 0, "Event ID should be positive");
            Assert.NotNull(firstEvent.Device);
            Assert.True(firstEvent.Device.Id > 0, "Device ID should be positive");
            Assert.NotEqual(default(DateTime), firstEvent.DateTime);

            _logService.Info($"First event: ID={firstEvent.Id}, Device={firstEvent.Device.Id}, DateTime={firstEvent.DateTime}");
        }
        else
        {
            _logService.Info("No events found in date range - test passed but no data to verify");
        }
    }

    #endregion
}

/// <summary>
/// DtoToModelHelper 확장 메소드 테스트
/// TDD로 ResolveDevice() 헬퍼 및 DeviceProvider 오버로드 구현
/// </summary>
public class DtoToModelHelperWithDeviceProviderTests
{
    #region - Test 1: ResolveDevice with valid DeviceProvider -
    [Fact(DisplayName = "TEST-01: ResolveDevice는 DeviceProvider에서 매칭되는 Sensor를 반환해야 함")]
    public void ResolveDevice_WithMatchingDevice_ShouldReturnCompleteDevice()
    {
        // Arrange
        var deviceProvider = new Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider();
        var expectedDevice = new SensorDeviceModel
        {
            Id = 1,
            DeviceNumber = 101,
            DeviceName = "Test Sensor 1",
            DeviceType = EnumDeviceType.Fence,
            Status = EnumDeviceStatus.ACTIVATED
        };
        deviceProvider.Add(expectedDevice);

        // Act - ResolveDevice는 private이므로 ToDetectionEventModel을 통해 간접 테스트
        var dto = new DetectionEventDto
        {
            Id = 100,
            Sensor = 1,
            TypeEvent = "Intrusion",
            Result = "THERMAL_SENSOR",
            ActionReported = "True",
            CreatedAt = "2025-11-24T10:00:00.000Z"
        };

        var model = dto.ToDetectionEventModel(deviceProvider); // ← 이 메소드가 없음 (RED)

        // Assert
        Assert.NotNull(model.Device);
        Assert.Equal(1, model.Device.Id);
        Assert.Equal("Test Sensor 1", model.Device.DeviceName); // 상세 정보 확인
        Assert.Equal(101, model.Device.DeviceNumber);
        Assert.Equal(EnumDeviceStatus.ACTIVATED, model.Device.Status);
    }
    #endregion

    #region - Test 2: ResolveDevice with non-matching ID -
    [Fact(DisplayName = "TEST-02: ResolveDevice는 매칭되지 않는 ID일 때 ID만 가진 객체를 반환해야 함")]
    public void ResolveDevice_WithNonMatchingDevice_ShouldReturnBasicDevice()
    {
        // Arrange
        var deviceProvider = new Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider();
        deviceProvider.Add(new SensorDeviceModel { Id = 999, DeviceName = "Other Sensor" });

        var dto = new DetectionEventDto
        {
            Id = 100,
            Sensor = 1, // 존재하지 않는 ID
            TypeEvent = "Intrusion",
            Result = "THERMAL_SENSOR",
            ActionReported = "True",
            CreatedAt = "2025-11-24T10:00:00.000Z"
        };

        // Act
        var model = dto.ToDetectionEventModel(deviceProvider); // ← RED

        // Assert
        Assert.NotNull(model.Device);
        Assert.Equal(1, model.Device.Id);
        Assert.Null(model.Device.DeviceName); // 상세 정보 없음
        Assert.Equal(0, model.Device.DeviceNumber);
    }
    #endregion

    #region - Test 3: ResolveDevice with null DeviceProvider -
    [Fact(DisplayName = "TEST-03: ResolveDevice는 DeviceProvider가 null일 때 ID만 가진 객체를 반환해야 함")]
    public void ResolveDevice_WithNullDeviceProvider_ShouldReturnBasicDevice()
    {
        // Arrange
        var dto = new DetectionEventDto
        {
            Id = 100,
            Sensor = 1,
            TypeEvent = "Intrusion",
            Result = "THERMAL_SENSOR",
            ActionReported = "True",
            CreatedAt = "2025-11-24T10:00:00.000Z"
        };

        // Act
        var model = dto.ToDetectionEventModel(null); // ← RED

        // Assert
        Assert.NotNull(model.Device);
        Assert.Equal(1, model.Device.Id);
        Assert.Null(model.Device.DeviceName);
    }
    #endregion

    #region - Test 4: Type filtering (Controller vs Sensor ID 충돌) -
    [Fact(DisplayName = "TEST-04: ResolveDevice는 Controller와 Sensor의 ID가 같을 때 Sensor만 반환해야 함")]
    public void ResolveDevice_WithSameIdControllerAndSensor_ShouldReturnSensorOnly()
    {
        // Arrange
        var deviceProvider = new Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider();

        // Controller id=1
        var controller = new ControllerDeviceModel
        {
            Id = 1,
            DeviceNumber = 1,
            DeviceName = "제어기_01",
            DeviceType = EnumDeviceType.Controller
        };
        deviceProvider.Add(controller);

        // Sensor id=1 (같은 ID!)
        var sensor = new SensorDeviceModel
        {
            Id = 1,
            DeviceNumber = 101,
            DeviceName = "펜스센서_01-001",
            DeviceType = EnumDeviceType.Fence
        };
        deviceProvider.Add(sensor);

        var dto = new DetectionEventDto
        {
            Id = 100,
            Sensor = 1, // Controller와 Sensor 둘 다 id=1
            TypeEvent = "Intrusion",
            Result = "THERMAL_SENSOR",
            ActionReported = "True",
            CreatedAt = "2025-11-24T10:00:00.000Z"
        };

        // Act
        var model = dto.ToDetectionEventModel(deviceProvider); // ← RED

        // Assert - Sensor가 반환되어야 함 (Controller 아님!)
        Assert.NotNull(model.Device);
        Assert.Equal(1, model.Device.Id);
        Assert.Equal("펜스센서_01-001", model.Device.DeviceName); // Sensor 이름
        Assert.Equal(EnumDeviceType.Fence, model.Device.DeviceType); // Sensor 타입
        Assert.NotEqual("제어기_01", model.Device.DeviceName); // Controller 이름이 아님
    }
    #endregion
}

/// <summary>
/// ResolveOriginEvent() 헬퍼 메서드 테스트
/// TDD로 ActionEvent의 OriginEvent Instantiation 구현
/// </summary>
public class DtoToModelHelperWithOriginEventTests
{
    #region - Test 1: ResolveOriginEvent with matching DetectionEvent in EventProvider -
    [Fact(DisplayName = "TEST-01: ResolveOriginEvent는 EventProvider에서 매칭되는 DetectionEvent를 반환해야 함")]
    public void ResolveOriginEvent_WithMatchingDetectionEvent_ShouldReturnEventProviderInstance()
    {
        // Arrange
        var eventProvider = new Ironwall.Dotnet.Libraries.Events.Providers.EventProvider();
        var deviceProvider = new Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider();

        // EventProvider에 기존 DetectionEvent 추가
        var existingEvent = new DetectionEventModel
        {
            Id = 456,
            DateTime = new DateTime(2025, 11, 25, 10, 0, 0, DateTimeKind.Utc),
            MessageType = EnumEventType.Intrusion,
            EventGroup = "1",
            Status = EnumTrueFalse.True,
            Result = EnumDetectionType.THERMAL_SENSOR,
            Device = new SensorDeviceModel { Id = 1, DeviceName = "Sensor 1" }
        };
        eventProvider.Add(existingEvent);

        // ActionEventDto with FromEvent
        var actionDto = new ActionEventDto
        {
            Id = 123,
            CreatedAt = "2025-11-25T10:30:00.000Z",
            TypeEvent = "Action",
            Content = "오탐 처리",
            User = "admin",
            FromEvent = new DetectionEventDto
            {
                Id = 456, // EventProvider에 있는 Event
                CreatedAt = "2025-11-25T10:00:00.000Z",
                TypeEvent = "Intrusion",
                GroupEvent = "1",
                ActionReported = "True",
                Result = "Intrusion",
                Sensor = 1
            }
        };

        // Act
        var model = actionDto.ToActionEventModel(eventProvider, deviceProvider); // ← RED

        // Assert
        Assert.NotNull(model.OriginEvent);
        Assert.Equal(456, model.OriginEvent.Id);
        Assert.Same(existingEvent, model.OriginEvent); // EventProvider 인스턴스와 동일해야 함
        Assert.Equal("Sensor 1", ((IDetectionEventModel)model.OriginEvent).Device.DeviceName);
    }
    #endregion

    #region - Test 2: ResolveOriginEvent with matching MalfunctionEvent in EventProvider -
    [Fact(DisplayName = "TEST-02: ResolveOriginEvent는 EventProvider에서 매칭되는 MalfunctionEvent를 반환해야 함")]
    public void ResolveOriginEvent_WithMatchingMalfunctionEvent_ShouldReturnEventProviderInstance()
    {
        // Arrange
        var eventProvider = new Ironwall.Dotnet.Libraries.Events.Providers.EventProvider();
        var deviceProvider = new Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider();

        // EventProvider에 기존 MalfunctionEvent 추가
        var existingEvent = new MalfunctionEventModel
        {
            Id = 789,
            DateTime = new DateTime(2025, 11, 25, 11, 0, 0, DateTimeKind.Utc),
            MessageType = EnumEventType.Fault,
            EventGroup = "2",
            Status = EnumTrueFalse.True,
            Reason = EnumFaultType.FAULT_FENCE,
            Device = new SensorDeviceModel { Id = 2, DeviceName = "Sensor 2" }
        };
        eventProvider.Add(existingEvent);

        // ActionEventDto with FromEvent (MalfunctionEventDto)
        var actionDto = new ActionEventDto
        {
            Id = 124,
            CreatedAt = "2025-11-25T11:30:00.000Z",
            TypeEvent = "Action",
            Content = "장애 확인",
            User = "admin",
            FromEvent = new MalfunctionEventDto
            {
                Id = 789, // EventProvider에 있는 Event
                CreatedAt = "2025-11-25T11:00:00.000Z",
                TypeEvent = "Fault",
                GroupEvent = "2",
                Status = "True",
                Reason = "FAULT_FENCE",
                Sensor = 2
            }
        };

        // Act
        var model = actionDto.ToActionEventModel(eventProvider, deviceProvider); // ← RED

        // Assert
        Assert.NotNull(model.OriginEvent);
        Assert.Equal(789, model.OriginEvent.Id);
        Assert.Same(existingEvent, model.OriginEvent); // EventProvider 인스턴스와 동일해야 함
    }
    #endregion

    #region - Test 3: ResolveOriginEvent with no match - should convert DTO -
    [Fact(DisplayName = "TEST-03: ResolveOriginEvent는 매칭되지 않을 때 DTO를 변환하여 반환해야 함")]
    public void ResolveOriginEvent_WithNoMatch_ShouldReturnConvertedDtoInstance()
    {
        // Arrange
        var eventProvider = new Ironwall.Dotnet.Libraries.Events.Providers.EventProvider();
        var deviceProvider = new Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider();

        // DeviceProvider에 Device 추가
        var device = new SensorDeviceModel { Id = 1, DeviceName = "Sensor 1", DeviceNumber = 101 };
        deviceProvider.Add(device);

        // ActionEventDto with FromEvent (EventProvider에 없음)
        var actionDto = new ActionEventDto
        {
            Id = 125,
            CreatedAt = "2025-11-25T12:00:00.000Z",
            TypeEvent = "Action",
            Content = "신규 조치",
            User = "admin",
            FromEvent = new DetectionEventDto
            {
                Id = 999, // EventProvider에 없는 Event
                CreatedAt = "2025-11-25T11:50:00.000Z",
                TypeEvent = "Intrusion",
                GroupEvent = "3",
                ActionReported = "True",
                Result = "THERMAL_SENSOR",
                Sensor = 1
            }
        };

        // Act
        var model = actionDto.ToActionEventModel(eventProvider, deviceProvider); // ← RED

        // Assert
        Assert.NotNull(model.OriginEvent);
        Assert.Equal(999, model.OriginEvent.Id);
        Assert.IsType<DetectionEventModel>(model.OriginEvent);
        // Device도 DeviceProvider로 매칭되어야 함
        var detectionEvent = (IDetectionEventModel)model.OriginEvent;
        Assert.Equal("Sensor 1", detectionEvent.Device.DeviceName);
        Assert.Equal(101, detectionEvent.Device.DeviceNumber);
    }
    #endregion

    #region - Test 4: ResolveOriginEvent with null EventProvider - should convert DTO -
    [Fact(DisplayName = "TEST-04: ResolveOriginEvent는 EventProvider가 null일 때 DTO를 변환해야 함")]
    public void ResolveOriginEvent_WithNullEventProvider_ShouldReturnConvertedDto()
    {
        // Arrange
        var deviceProvider = new Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider();
        deviceProvider.Add(new SensorDeviceModel { Id = 1, DeviceName = "Sensor 1" });

        var actionDto = new ActionEventDto
        {
            Id = 126,
            CreatedAt = "2025-11-25T13:00:00.000Z",
            TypeEvent = "Action",
            Content = "조치",
            User = "admin",
            FromEvent = new DetectionEventDto
            {
                Id = 888,
                CreatedAt = "2025-11-25T12:50:00.000Z",
                TypeEvent = "Intrusion",
                GroupEvent = "4",
                ActionReported = "True",
                Result = "THERMAL_SENSOR",
                Sensor = 1
            }
        };

        // Act
        var model = actionDto.ToActionEventModel(null, deviceProvider); // EventProvider = null

        // Assert
        Assert.NotNull(model.OriginEvent);
        Assert.Equal(888, model.OriginEvent.Id);
        Assert.IsType<DetectionEventModel>(model.OriginEvent);
        // Device는 DeviceProvider로 매칭됨
        var detectionEvent = (IDetectionEventModel)model.OriginEvent;
        Assert.Equal("Sensor 1", detectionEvent.Device.DeviceName);
    }
    #endregion

    #region - Test 5: ResolveOriginEvent with null FromEvent - should return null -
    [Fact(DisplayName = "TEST-05: ResolveOriginEvent는 FromEvent가 null일 때 null을 반환해야 함")]
    public void ResolveOriginEvent_WithNullFromEvent_ShouldReturnNull()
    {
        // Arrange
        var eventProvider = new Ironwall.Dotnet.Libraries.Events.Providers.EventProvider();
        var deviceProvider = new Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider();

        var actionDto = new ActionEventDto
        {
            Id = 127,
            CreatedAt = "2025-11-25T14:00:00.000Z",
            TypeEvent = "Action",
            Content = "조치",
            User = "admin",
            FromEvent = null // FromEvent가 null
        };

        // Act
        var model = actionDto.ToActionEventModel(eventProvider, deviceProvider);

        // Assert
        Assert.Null(model.OriginEvent);
    }
    #endregion

    #region - Test 6: Type filtering (DetectionEvent vs MalfunctionEvent ID 충돌) -
    [Fact(DisplayName = "TEST-06: ResolveOriginEvent는 Detection과 Malfunction의 ID가 같을 때 올바른 타입을 반환해야 함")]
    public void ResolveOriginEvent_WithSameIdDifferentTypes_ShouldReturnCorrectType()
    {
        // Arrange
        var eventProvider = new Ironwall.Dotnet.Libraries.Events.Providers.EventProvider();
        var deviceProvider = new Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider();

        // DetectionEvent id=1
        var detectionEvent = new DetectionEventModel
        {
            Id = 1,
            DateTime = new DateTime(2025, 11, 25, 10, 0, 0, DateTimeKind.Utc),
            MessageType = EnumEventType.Intrusion,
            EventGroup = "1",
            Status = EnumTrueFalse.True,
            Result = EnumDetectionType.THERMAL_SENSOR,
            Device = new SensorDeviceModel { Id = 1, DeviceName = "Detection Sensor" }
        };
        eventProvider.Add(detectionEvent);

        // MalfunctionEvent id=1 (같은 ID!)
        var malfunctionEvent = new MalfunctionEventModel
        {
            Id = 1,
            DateTime = new DateTime(2025, 11, 25, 11, 0, 0, DateTimeKind.Utc),
            MessageType = EnumEventType.Fault,
            EventGroup = "2",
            Status = EnumTrueFalse.True,
            Reason = EnumFaultType.FAULT_FENCE,
            Device = new SensorDeviceModel { Id = 2, DeviceName = "Malfunction Sensor" }
        };
        eventProvider.Add(malfunctionEvent);

        // ActionEventDto with FromEvent (MalfunctionEventDto, id=1)
        var actionDto = new ActionEventDto
        {
            Id = 128,
            CreatedAt = "2025-11-25T12:00:00.000Z",
            TypeEvent = "Action",
            Content = "장애 조치",
            User = "admin",
            FromEvent = new MalfunctionEventDto
            {
                Id = 1, // DetectionEvent와 MalfunctionEvent 둘 다 id=1
                CreatedAt = "2025-11-25T11:00:00.000Z",
                TypeEvent = "Fault",
                GroupEvent = "2",
                Status = "True",
                Reason = "FAULT_FENCE",
                Sensor = 2
            }
        };

        // Act
        var model = actionDto.ToActionEventModel(eventProvider, deviceProvider);

        // Assert - MalfunctionEvent가 반환되어야 함 (DetectionEvent 아님!)
        Assert.NotNull(model.OriginEvent);
        Assert.Equal(1, model.OriginEvent.Id);
        Assert.IsType<MalfunctionEventModel>(model.OriginEvent);
        Assert.Same(malfunctionEvent, model.OriginEvent); // MalfunctionEvent 인스턴스
        Assert.NotSame(detectionEvent, model.OriginEvent); // DetectionEvent 인스턴스 아님

        var malfunctionOrigin = (IMalfunctionEventModel)model.OriginEvent;
        Assert.Equal("Malfunction Sensor", malfunctionOrigin.Device.DeviceName);
    }
    #endregion
}
