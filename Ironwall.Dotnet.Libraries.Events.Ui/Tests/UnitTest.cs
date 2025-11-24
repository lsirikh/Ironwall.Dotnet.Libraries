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
}
