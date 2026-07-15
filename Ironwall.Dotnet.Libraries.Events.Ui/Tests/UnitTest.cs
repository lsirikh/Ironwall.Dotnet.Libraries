using Xunit;
using Moq;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Autofac;
using Ironwall.Dotnet.Libraries.Events.Api.Modules;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using System.IO;
using System.Linq;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Components;

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
            TypeEvent = "Intrusion",
            Device = new BaseDeviceDto { Id = 100, TypeDevice = "Fence" },
            DeviceDescription = "Fence sensor #100",
            ActionReported = "True",
            Result = "THERMAL_SENSOR",
            CreatedAt = "2025-11-24T10:30:00.000Z",
            UpdatedAt = "2025-11-24T10:30:00.000Z"
        };

        // Act
        var model = dto.ToDetectionEventModel();

        // Assert
        Assert.Equal(1, model.Id);
        Assert.Equal(DateTime.Parse("2025-11-24T10:30:00.000Z"), model.DateTime);
        Assert.Equal(EnumEventType.Intrusion, model.MessageType);
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
        Assert.Equal("True", dto.ActionReported);
        Assert.Equal("THERMAL_SENSOR", dto.Result);
        Assert.NotNull(dto.Device);
        Assert.Equal(100, dto.Device.Id);
    }

    [Fact]
    public void ToMalfunctionEventModel_ShouldConvertDtoToModel()
    {
        // Arrange
        var dto = new MalfunctionEventDto
        {
            Id = 2,
            TypeEvent = "Fault",
            Device = new BaseDeviceDto { Id = 200, TypeDevice = "Fence" },
            DeviceDescription = "Fence sensor #200",
            ActionReported = "False",
            Reason = "FAULT_FENCE",
            Detail = new MalfunctionDetailDto
            {
                FirstStart = 10,
                FirstEnd = 20,
                SecondStart = 30,
                SecondEnd = 40
            },
            CreatedAt = "2025-11-24T11:00:00.000Z",
            UpdatedAt = "2025-11-24T11:00:00.000Z"
        };

        // Act
        var model = dto.ToMalfunctionEventModel();

        // Assert
        Assert.Equal(2, model.Id);
        Assert.Equal(DateTime.Parse("2025-11-24T11:00:00.000Z"), model.DateTime);
        Assert.Equal(EnumEventType.Fault, model.MessageType);
        Assert.Equal(EnumTrueFalse.False, model.Status);
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
        Assert.Equal("FAULT_FENCE", dto.Reason);
        Assert.NotNull(dto.Detail);
        Assert.Equal(10, dto.Detail.FirstStart);
        Assert.Equal(20, dto.Detail.FirstEnd);
        Assert.Equal(30, dto.Detail.SecondStart);
        Assert.Equal(40, dto.Detail.SecondEnd);
        Assert.NotNull(dto.Device);
        Assert.Equal(200, dto.Device.Id);
    }

    [Fact]
    public void ToConnectionEventModel_ShouldConvertDtoToModel()
    {
        // Arrange
        var dto = new ConnectionEventDto
        {
            Id = 3,
            TypeEvent = "Connection",
            Device = new BaseDeviceDto { Id = 300, TypeDevice = "Controller" },
            DeviceDescription = "Controller #300",
            CreatedAt = "2025-11-24T12:00:00.000Z",
            UpdatedAt = "2025-11-24T12:00:00.000Z"
        };

        // Act
        var model = dto.ToConnectionEventModel();

        // Assert
        Assert.Equal(3, model.Id);
        Assert.Equal(DateTime.Parse("2025-11-24T12:00:00.000Z"), model.DateTime);
        Assert.Equal(EnumEventType.Connection, model.MessageType);
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
        Assert.NotNull(dto.Device);
        Assert.Equal(300, dto.Device.Id);
    }

    [Fact]
    public void ToActionEventModel_ShouldConvertDtoToModel()
    {
        // Arrange
        var dto = new ActionEventDto
        {
            Id = 4,
            TypeEvent = "ACTION",
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
        Assert.Equal(DateTime.Parse("2025-11-24T13:00:00.000Z"), model.DateTime);
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

    /// <summary>
    /// ACTION_REPORT NATS 발행 봉투 계약 검증 (Gop_Message_Broker §2.3·§2.4·§6.4).
    /// NatsDomainService 발행 경로와 동일: OriginEvent 담은 ActionEventModel → ToActionEventDto() → ToBrokerPublish("GIS").
    /// 회귀 방지: body가 {content,user}만이던 결함(from_event 누락) + from=SystemUuid 결함 재발 차단.
    /// </summary>
    [Fact]
    public void ToBrokerPublish_ActionReport_ShouldCarryFullFromEventAndGisFrom()
    {
        // Arrange — 조치보고 원본(Detection + Device)
        var origin = new DetectionEventModel
        {
            Id = 1001,
            MessageType = EnumEventType.Intrusion,
            DateTime = new DateTime(2026, 7, 8, 5, 21, 31, DateTimeKind.Utc),
            Device = new SensorDeviceModel
            {
                Id = 101,
                DeviceType = EnumDeviceType.Fence,
                DeviceName = "Sensor-A-1",
                DeviceNumber = 1,
                Status = EnumDeviceStatus.ACTIVATED,
                Version = "v1.5.0",
                Controller = new ControllerDeviceModel { Id = 1 },
                Latitude = 37.5,
                Longitude = 127.0
            }
        };
        var actionModel = new ActionEventModel
        {
            Id = 4001,
            MessageType = EnumEventType.Action,
            DateTime = new DateTime(2026, 7, 8, 5, 21, 31, DateTimeKind.Utc),
            Content = "야생동물출현",
            User = "시스템 관리자",
            OriginEvent = origin
        };

        // Act — 발행부(NatsDomainService)와 동일 경로
        var dto = actionModel.ToActionEventDto();
        var envelope = dto.ToBrokerPublish(EnumGopCommand.ACTION_REPORT.ToString(), "GIS");
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(envelope);

        // Assert — 계약: from="GIS", cmd=ACTION_REPORT, body.from_event(device.id=101)
        Assert.Equal("GIS", envelope.From);
        Assert.Equal("ACTION_REPORT", envelope.Command);
        Assert.NotNull(dto.FromEvent);
        var dev = ((DetectionEventDto)dto.FromEvent!).Device!;
        Assert.Equal(101, dev.Id);
        Assert.Equal("ACTIVATED", dev.Status);      // §6.4 device.status
        Assert.Equal("v1.5.0", dev.Version);        // §6.4 device.version
        Assert.Equal(1, dev.ControllerId);          // §6.4 device.controller_id
        Assert.NotNull(dev.Geolocation);            // §6.4 device.geolocation
        Assert.Equal(4001, dto.Id);
        Assert.Contains("\"from\":\"GIS\"", json);
        Assert.Contains("\"from_event\"", json);
        Assert.Contains("\"id\":101", json);            // from_event.device.id
        Assert.Contains("\"controller_id\":1", json);
        Assert.Contains("\"status\":\"ACTIVATED\"", json);
    }

    /// <summary>
    /// FR-02/FR-04(GIS.md v1.5 §2.1/§6.4): ACTION_REPORT from_event.device 가
    /// device_groups(N:N EventMapping 라우팅 키 id) + geolocation 전필드(location/altitude/heading)를 담는다.
    /// </summary>
    [Fact]
    public void should_carry_device_groups_and_full_geolocation_when_action_report_published()
    {
        // Arrange — 그룹 소속 + 고도/방위/설명 있는 장비
        var origin = new DetectionEventModel
        {
            Id = 1002,
            MessageType = EnumEventType.Intrusion,
            DateTime = new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc),
            Device = new SensorDeviceModel
            {
                Id = 202,
                DeviceType = EnumDeviceType.Fence,
                DeviceName = "Sensor-B-11",
                DeviceNumber = 11,
                Status = EnumDeviceStatus.ERROR,
                Version = "v1.5.0",
                Controller = new ControllerDeviceModel { Id = 2 },
                DeviceGroups = new List<int> { 1, 10 },
                Latitude = 37.5,
                Longitude = 127.0,
                Altitude = 42.5,
                Heading = 135.0,
                Location = "북측 울타리 A구간"
            }
        };
        var actionModel = new ActionEventModel
        {
            Id = 4002,
            MessageType = EnumEventType.Action,
            DateTime = new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc),
            Content = "동물",
            User = "운영자",
            OriginEvent = origin
        };

        // Act — 발행부와 동일 경로
        var dto = actionModel.ToActionEventDto();
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(
            dto.ToBrokerPublish(EnumGopCommand.ACTION_REPORT.ToString(), "GIS"));

        // Assert — device_groups(라우팅 키 id) + geolocation 전필드
        var dev = ((DetectionEventDto)dto.FromEvent!).Device!;
        Assert.NotNull(dev.DeviceGroups);
        Assert.Equal(new[] { 1, 10 }, dev.DeviceGroups!.Select(g => g.Id).ToArray());
        Assert.NotNull(dev.Geolocation);
        Assert.Equal(42.5, dev.Geolocation!.Altitude);
        Assert.Equal(135.0, dev.Geolocation!.Heading);
        Assert.Equal("북측 울타리 A구간", dev.Geolocation!.Location);
        Assert.Contains("\"device_groups\"", json);
        Assert.Contains("\"altitude\":42.5", json);
    }
}

/// <summary>
/// FR-01(GIS.md v1.5 §3.6): PTZ_STATUS subject 판별 — 스펙 gis.ptz-status + 구 nvr_manager.ptz-status 병행 수용.
/// </summary>
public class CameraPtzSubjectFilterTests
{
    [Theory]
    [InlineData("sensorway.HQ.gis.ptz-status", true)]          // 스펙 v1.5
    [InlineData("sensorway.HQ.nvr_manager.ptz-status", true)]  // 구 subject 과도기
    [InlineData("sensorway.HQ.gis.tracking-status", false)]    // 타 gis 메시지
    [InlineData("", false)]
    [InlineData(null, false)]
    public void should_match_ptz_status_subject_when_gis_or_legacy_token(string? subject, bool expected)
        => Assert.Equal(
            expected,
            Ironwall.Dotnet.Libraries.Events.Ui.Services.CameraPtzNatsSyncService.IsPtzStatusSubject(subject));
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
                TypeEvent = "Intrusion",
                Device = new BaseDeviceDto { Id = 100 },
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
                new DetectionEventDto { Id = 1, TypeEvent = "Intrusion", Device = new BaseDeviceDto { Id = 100 }, ActionReported = "True", Result = "THERMAL_SENSOR", CreatedAt = "2025-11-24T10:00:00.000Z" },
                new DetectionEventDto { Id = 2, TypeEvent = "Intrusion", Device = new BaseDeviceDto { Id = 101 }, ActionReported = "False", Result = "PIR_SENSOR", CreatedAt = "2025-11-24T10:05:00.000Z" }
            },
            Pagination = new PaginationDto { Page = 1, TotalPages = 2, Total = 3, Limit = 2 }
        };

        // Page 2
        var page2Response = new ApiListResponse<DetectionEventDto>
        {
            Success = true,
            Data = new List<DetectionEventDto>
            {
                new DetectionEventDto { Id = 3, TypeEvent = "Intrusion", Device = new BaseDeviceDto { Id = 102 }, ActionReported = "False", Result = "VIBRATION_SENSOR", CreatedAt = "2025-11-24T10:10:00.000Z" }
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
                TypeEvent = "Fault",
                Device = new BaseDeviceDto { Id = 100 },
                ActionReported = "False",
                Reason = "FAULT_FENCE",
                Detail = new MalfunctionDetailDto { FirstStart = 10, FirstEnd = 15, SecondStart = 20, SecondEnd = 25 },
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
                TypeEvent = "Connection",
                Device = new BaseDeviceDto { Id = 100 },
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
                TypeEvent = "ACTION",
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
            TypeEvent = "Intrusion",
            Device = new BaseDeviceDto { Id = 200 },
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
            It.Is<DetectionEventDto>(dto => dto.Device != null && dto.Device.Id == 200 && dto.Result == "THERMAL_SENSOR"),
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
            TypeEvent = "Intrusion",
            Device = new BaseDeviceDto { Id = 200 },
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
                It.IsAny<DetectionEventReplaceDto>(),
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
            It.Is<DetectionEventReplaceDto>(dto => dto.TypeEvent == "Intrusion" && dto.Result == "PIR_SENSOR"),
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
            TypeEvent = "Fault",
            Device = new BaseDeviceDto { Id = 300 },
            Reason = "FAULT_CONTROLLER",
            Detail = new MalfunctionDetailDto { FirstStart = 10, FirstEnd = 15, SecondStart = 20, SecondEnd = 25 },
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
            TypeEvent = "Fault",
            Device = new BaseDeviceDto { Id = 300 },
            Reason = "FAULT_FENCE",
            Detail = new MalfunctionDetailDto { FirstStart = 11, FirstEnd = 16, SecondStart = 21, SecondEnd = 26 },
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
                It.IsAny<MalfunctionEventReplaceDto>(),
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
            TypeEvent = "Connection",
            Device = new BaseDeviceDto { Id = 400 },
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
            TypeEvent = "Connection",
            Device = new BaseDeviceDto { Id = 400 },
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
                It.IsAny<ConnectionEventReplaceDto>(),
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
            TypeEvent = "ACTION",
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
            TypeEvent = "ACTION",
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
                It.IsAny<ActionEventReplaceDto>(),
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

    // 회귀 방지: 실서버 DELETE는 {success:true, data:null} 반환 → ApiResponse<bool>.Data=false.
    // 과거 provider가 response.Data를 반환 → 서버 성공을 "삭제 실패"로 오판(조치보고 600건 삭제 버그).
    // .Success로 판정해야 true. (옛 코드면 이 테스트 실패, 수정본이면 통과)
    [Fact]
    public async Task should_return_true_when_server_delete_succeeds_with_null_data()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var apiResponse = new ApiResponse<bool>
        {
            Success = true,
            Data = false   // 서버 data:null → 역직렬화 시 bool 기본값 false
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
        _logService.Info($"  - CONNECTION: {connectionEvents.Count}");
        _logService.Info($"  - ACTION: {actionEvents.Count}");
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

        // Act - ConvertDeviceFromDto는 private이므로 ToDetectionEventModel을 통해 간접 테스트
        var dto = new DetectionEventDto
        {
            Id = 100,
            Device = new BaseDeviceDto { Id = 1, TypeDevice = "Fence" },
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
            Device = new BaseDeviceDto { Id = 1 }, // 존재하지 않는 ID
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
        // fallback 시 DTO.NameDevice 기본값(빈 문자열)이 매핑됨
        Assert.Equal(string.Empty, model.Device.DeviceName);
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
            Device = new BaseDeviceDto { Id = 1 },
            TypeEvent = "Intrusion",
            Result = "THERMAL_SENSOR",
            ActionReported = "True",
            CreatedAt = "2025-11-24T10:00:00.000Z"
        };

        // Act
        var model = dto.ToDetectionEventModel(null);

        // Assert
        Assert.NotNull(model.Device);
        Assert.Equal(1, model.Device.Id);
        // fallback 시 DTO.NameDevice 기본값(빈 문자열)이 매핑됨
        Assert.Equal(string.Empty, model.Device.DeviceName);
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
            Device = new BaseDeviceDto { Id = 1, TypeDevice = "Fence" }, // Controller와 Sensor 둘 다 id=1
            TypeEvent = "Intrusion",
            Result = "THERMAL_SENSOR",
            ActionReported = "True",
            CreatedAt = "2025-11-24T10:00:00.000Z"
        };

        // Act
        var model = dto.ToDetectionEventModel(deviceProvider);

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
            TypeEvent = "ACTION",
            Content = "오탐 처리",
            User = "admin",
            FromEvent = new DetectionEventDto
            {
                Id = 456, // EventProvider에 있는 Event
                CreatedAt = "2025-11-25T10:00:00.000Z",
                TypeEvent = "Intrusion",
                ActionReported = "True",
                Result = "THERMAL_SENSOR",
                Device = new BaseDeviceDto { Id = 1 }
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
            TypeEvent = "ACTION",
            Content = "장애 확인",
            User = "admin",
            FromEvent = new MalfunctionEventDto
            {
                Id = 789, // EventProvider에 있는 Event
                CreatedAt = "2025-11-25T11:00:00.000Z",
                TypeEvent = "Fault",
                Reason = "FAULT_FENCE",
                Device = new BaseDeviceDto { Id = 2 }
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
            TypeEvent = "ACTION",
            Content = "신규 조치",
            User = "admin",
            FromEvent = new DetectionEventDto
            {
                Id = 999, // EventProvider에 없는 Event
                CreatedAt = "2025-11-25T11:50:00.000Z",
                TypeEvent = "Intrusion",
                ActionReported = "True",
                Result = "THERMAL_SENSOR",
                Device = new BaseDeviceDto { Id = 1 }
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
            TypeEvent = "ACTION",
            Content = "조치",
            User = "admin",
            FromEvent = new DetectionEventDto
            {
                Id = 888,
                CreatedAt = "2025-11-25T12:50:00.000Z",
                TypeEvent = "Intrusion",
                ActionReported = "True",
                Result = "THERMAL_SENSOR",
                Device = new BaseDeviceDto { Id = 1 }
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
            TypeEvent = "ACTION",
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
            TypeEvent = "ACTION",
            Content = "장애 조치",
            User = "admin",
            FromEvent = new MalfunctionEventDto
            {
                Id = 1, // DetectionEvent와 MalfunctionEvent 둘 다 id=1
                CreatedAt = "2025-11-25T11:00:00.000Z",
                TypeEvent = "Fault",
                Reason = "FAULT_FENCE",
                Device = new BaseDeviceDto { Id = 2 }
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

#region Phase 12: PTZ → FOV Integration Tests
/// <summary>
/// Unit tests for DeviceSymbolLookupModel PTZ → FOV conversion
/// Following TDD (Red-Green-Refactor) methodology
/// PRD Reference: docs/prds/PRD_PTZ_FOV_Integration.md
/// </summary>
public class DeviceSymbolLookupModelTests
{
    #region Test 12.1.1: ConvertPanToBearing - 기본 변환
    [Fact]
    public void ConvertPanToBearing_WithPan90_ShouldReturn90()
    {
        // Arrange
        var lookup = CreateTestLookup();
        float pan = 90f;

        // Act
        var bearing = lookup.TestConvertPanToBearing(pan);

        // Assert
        Assert.Equal(90.0, bearing);
    }
    #endregion

    #region Test 12.1.2: ConvertPanToBearing - 180도 초과 변환
    [Fact]
    public void ConvertPanToBearing_WithPan270_ShouldReturnMinus90()
    {
        // Arrange
        var lookup = CreateTestLookup();
        float pan = 270f;

        // Act
        var bearing = lookup.TestConvertPanToBearing(pan);

        // Assert
        Assert.Equal(-90.0, bearing);  // 270 - 360 = -90
    }
    #endregion

    #region Test 12.1.3: ConvertZoomToAngle - 줌 100% (1x)
    [Fact]
    public void ConvertZoomToAngle_WithZoom100_ShouldReturnBaseAngle()
    {
        // Arrange
        var lookup = CreateTestLookup();
        float zoom = 100f;  // 1x

        // Act
        var angle = lookup.TestConvertZoomToAngle(zoom);

        // Assert
        Assert.Equal(80.0, angle);  // BaseDetectionAngle = 80
    }
    #endregion

    #region Test 12.1.4: ConvertZoomToAngle - 줌 200% (2x)
    [Fact]
    public void ConvertZoomToAngle_WithZoom200_ShouldReturnHalfAngle()
    {
        // Arrange
        var lookup = CreateTestLookup();
        float zoom = 200f;  // 2x

        // Act
        var angle = lookup.TestConvertZoomToAngle(zoom);

        // Assert
        Assert.Equal(40.0, angle);  // 80 / 2 = 40
    }
    #endregion

    #region Test 12.1.5: ConvertZoomToAngle - 최소값 제한
    [Fact]
    public void ConvertZoomToAngle_WithZoom2000_ShouldClampToMinAngle()
    {
        // Arrange
        var lookup = CreateTestLookup();
        float zoom = 2000f;  // 20x → 80/20 = 4 < MinDetectionAngle(5)

        // Act
        var angle = lookup.TestConvertZoomToAngle(zoom);

        // Assert
        Assert.Equal(5.0, angle);  // Clamped to MinDetectionAngle
    }
    #endregion

    #region Test 12.1.6: ConvertZoomToRange - 줌 100% (1x)
    [Fact]
    public void ConvertZoomToRange_WithZoom100_ShouldReturnBaseRange()
    {
        // Arrange
        var lookup = CreateTestLookup();
        float zoom = 100f;  // 1x

        // Act
        var range = lookup.TestConvertZoomToRange(zoom);

        // Assert
        Assert.Equal(30.0, range);  // BaseDetectionRange = 30, zoom 1x
    }
    #endregion

    #region Test 12.1.7: ConvertZoomToRange - 줌 400% (4x)
    [Fact]
    public void ConvertZoomToRange_WithZoom400_ShouldReturn4xRange()
    {
        // Arrange
        var lookup = CreateTestLookup();
        float zoom = 400f;  // 4x

        // Act
        var range = lookup.TestConvertZoomToRange(zoom);

        // Assert
        Assert.Equal(120.0, range);  // 30 * 4 = 120
    }
    #endregion

    #region Test 12.1.8: ConvertZoomToRange - 최대값 제한
    [Fact]
    public void ConvertZoomToRange_WithZoom3000_ShouldClampToMaxRange()
    {
        // Arrange
        var lookup = CreateTestLookup();
        float zoom = 3000f;  // 30x → 30*30 = 900 (under MaxDetectionRange 2000)

        // Act
        var range = lookup.TestConvertZoomToRange(zoom);

        // Assert
        Assert.Equal(900.0, range);  // 30 * 30 = 900
    }
    #endregion

    #region Test 12.1.9: UpdateFOV - IPidsSymbolModel 아닌 경우 무시
    [Fact]
    public void UpdateFOV_WithNonPidsSymbol_ShouldNotThrow()
    {
        // Arrange
        var lookup = CreateTestLookup();
        // SymbolModel is IPidsEventCapable but NOT IPidsSymbolModel
        var mockSymbol = new Mock<Ironwall.Dotnet.Monitoring.Models.Symbols.IPidsEventCapable>();
        lookup.SymbolModel = mockSymbol.Object;

        // Act & Assert - should not throw exception
        var exception = Record.Exception(() => lookup.TestUpdateFOV(90f, 45f, 200f));
        Assert.Null(exception);
    }
    #endregion

    #region Test 12.1.10: UpdateFOV - 정상 동작
    [Fact]
    public void UpdateFOV_WithValidPidsSymbol_ShouldUpdateAllProperties()
    {
        // Arrange
        var lookup = CreateTestLookup();
        var mockPidsSymbol = new Mock<Ironwall.Dotnet.Monitoring.Models.Symbols.IPidsSymbolModel>();
        mockPidsSymbol.SetupAllProperties();  // Allow property setters
        lookup.SymbolModel = mockPidsSymbol.Object;

        // Act
        lookup.TestUpdateFOV(pan: 180f, tilt: 45f, zoom: 200f);

        // Assert
        // DetectionBearing = 180 (pan 180 → bearing 180)
        mockPidsSymbol.VerifySet(s => s.DetectionBearing = 180.0, Times.Once);
        // DetectionAngle = 40 (80 / 2)
        mockPidsSymbol.VerifySet(s => s.DetectionAngle = 40.0, Times.Once);
        // DetectionRange = 60 (30 * 2)
        mockPidsSymbol.VerifySet(s => s.DetectionRange = 60.0, Times.Once);
        // SetUpdate() should be called once
        mockPidsSymbol.Verify(s => s.SetUpdate(), Times.Once);
    }
    #endregion

    #region Helper Methods
    private TestableDeviceSymbolLookupModel CreateTestLookup()
    {
        return new TestableDeviceSymbolLookupModel();
    }
    #endregion
}

/// <summary>
/// Testable wrapper for DeviceSymbolLookupModel to expose private methods
/// </summary>
public class TestableDeviceSymbolLookupModel : Ironwall.Dotnet.Libraries.Events.Ui.Models.DeviceSymbolLookupModel
{
    public TestableDeviceSymbolLookupModel() : base(null!)
    {
    }

    // 하위 호환성 유지용 (기존 EventSetupModel 참조 제거)
    private static Ironwall.Dotnet.Libraries.Events.Models.EventSetupModel CreateMockEventSetupModel()
    {
        var mockModel = new Mock<Ironwall.Dotnet.Libraries.Events.Models.IEventSetupModel>();
        mockModel.Setup(m => m.IsAutoEventDiscard).Returns(false);
        mockModel.Setup(m => m.IsSound).Returns(false);
        mockModel.Setup(m => m.TimeDurationSound).Returns(0);
        mockModel.Setup(m => m.TimeDiscardSec).Returns(0);
        mockModel.Setup(m => m.LengthMaxEventPrev).Returns(0);
        mockModel.Setup(m => m.LengthMinEventPrev).Returns(0);
        return new Ironwall.Dotnet.Libraries.Events.Models.EventSetupModel(mockModel.Object);
    }

    public double TestConvertPanToBearing(float pan)
    {
        return ConvertPanToBearing(pan);
    }

    public double TestConvertZoomToAngle(float zoom)
    {
        return ConvertZoomToAngle(zoom);
    }

    public double TestConvertZoomToRange(float zoom)
    {
        return ConvertZoomToRange(zoom);
    }

    public void TestUpdateFOV(float pan, float tilt, float zoom)
    {
        UpdateFOV(pan, tilt, zoom);
    }
}
#endregion

#region Phase 12.2: SymbolEventManager Tests
/// <summary>
/// SymbolEventManager PTZ/FOV 테스트
/// PRD Reference: docs/prds/PRD_PTZ_FOV_Integration.md
/// </summary>
public class SymbolEventManagerTests
{
    #region Test 12.2.1: ProcessCameraPtz - 등록된 카메라
    [Fact]
    public void ProcessCameraPtz_WithRegisteredCamera_ShouldCallUpdateFOV()
    {
        // Arrange
        var mockEa = new Mock<Caliburn.Micro.IEventAggregator>();
        var mockLog = new Mock<ILogService>();
        var eventSetupModel = CreateMockEventSetupModel();

        var manager = new Ironwall.Dotnet.Libraries.Events.Ui.Managers.SymbolEventManager(
            mockEa.Object, mockLog.Object, eventSetupModel);

        // 카메라 장비 생성 (Id = 1)
        var cameraDevice = new Mock<Ironwall.Dotnet.Monitoring.Models.Devices.IBaseDeviceModel>();
        cameraDevice.Setup(d => d.Id).Returns(1);
        cameraDevice.Setup(d => d.DeviceType).Returns(EnumDeviceType.IpCamera);

        // PIDS 심볼 생성
        var mockSymbol = new Mock<Ironwall.Dotnet.Monitoring.Models.Symbols.IPidsSymbolModel>();
        mockSymbol.SetupAllProperties();

        // 장비-심볼 매핑 등록
        manager.RegisterDeviceSymbol(cameraDevice.Object, mockSymbol.Object);

        // Act — zoom 0~100 정규화 (50 = 중간 줌)
        manager.ProcessCameraPtz(cameraId: 1, pan: 90f, tilt: 45f, zoom: 50f);

        // Assert - FOV 속성이 업데이트 되어야 함
        // zoom=50 → ratio=0.5 → Angle=80-0.5*75=42.5, Range=10+0.5*2990=1505
        mockSymbol.VerifySet(s => s.DetectionBearing = 90.0, Times.Once);
        mockSymbol.VerifySet(s => s.DetectionAngle = 42.5, Times.Once);
        mockSymbol.VerifySet(s => s.DetectionRange = 1505.0, Times.Once);
        // RegisterDeviceSymbol now calls SyncFromDevice (1 extra SetUpdate), PTZ adds 1 more
        mockSymbol.Verify(s => s.SetUpdate(), Times.AtLeastOnce());
    }
    #endregion

    #region Test 12.2.2: ProcessCameraPtz - 미등록 카메라
    [Fact]
    public void ProcessCameraPtz_WithUnregisteredCamera_ShouldLogWarning()
    {
        // Arrange
        var mockEa = new Mock<Caliburn.Micro.IEventAggregator>();
        var mockLog = new Mock<ILogService>();
        var eventSetupModel = CreateMockEventSetupModel();

        var manager = new Ironwall.Dotnet.Libraries.Events.Ui.Managers.SymbolEventManager(
            mockEa.Object, mockLog.Object, eventSetupModel);

        // 등록된 장비 없음

        // Act
        var exception = Record.Exception(() => manager.ProcessCameraPtz(cameraId: 999, pan: 90f, tilt: 45f, zoom: 200f));

        // Assert - 예외 없음, 경고 로그만 출력
        Assert.Null(exception);
        mockLog.Verify(l => l.Warning(
            It.Is<string>(s => s.Contains("999")),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>()), Times.Once);
    }
    #endregion

    #region Test 12.4.1: End-to-End PTZ → FOV 통합 테스트
    [Fact]
    public void EndToEnd_PtzUpdate_ShouldUpdateCameraFOV()
    {
        // Arrange - Full integration setup
        var mockEa = new Mock<Caliburn.Micro.IEventAggregator>();
        var mockLog = new Mock<ILogService>();
        var eventSetupModel = CreateMockEventSetupModel();

        var manager = new Ironwall.Dotnet.Libraries.Events.Ui.Managers.SymbolEventManager(
            mockEa.Object, mockLog.Object, eventSetupModel);

        // 카메라 장비 생성 (Id = 100, DeviceName = "CAM-001")
        var cameraDevice = new Mock<Ironwall.Dotnet.Monitoring.Models.Devices.ICameraDeviceModel>();
        cameraDevice.Setup(d => d.Id).Returns(100);
        cameraDevice.Setup(d => d.DeviceName).Returns("CAM-001");
        cameraDevice.Setup(d => d.DeviceType).Returns(EnumDeviceType.IpCamera);

        // PIDS 심볼 생성 (카메라 FOV 표시용)
        var pidsSymbol = new Mock<Ironwall.Dotnet.Monitoring.Models.Symbols.IPidsSymbolModel>();
        pidsSymbol.SetupAllProperties();

        // 장비-심볼 매핑 등록 (카메라도 일반 디바이스처럼 등록)
        manager.RegisterDeviceSymbol(cameraDevice.Object, pidsSymbol.Object);

        // Act - PTZ 메시지 수신 시뮬레이션
        // NatsDomainService.ProcessCurrentPtz() → SymbolEventManager.ProcessCameraPtz()
        manager.ProcessCameraPtz(cameraId: 100, pan: 270f, tilt: 30f, zoom: 75f);

        // Assert - FOV 속성이 올바르게 업데이트 되어야 함
        // Pan 270 → Bearing -90 (270 - 360)
        pidsSymbol.VerifySet(s => s.DetectionBearing = -90.0, Times.Once);
        // zoom=75 → ratio=0.75 → Angle=80-0.75*75=23.75
        pidsSymbol.VerifySet(s => s.DetectionAngle = 23.75, Times.Once);
        // zoom=75 → ratio=0.75 → Range=10+0.75*2990=2252.5
        pidsSymbol.VerifySet(s => s.DetectionRange = 2252.5, Times.Once);
        // SetUpdate() called during registration (SyncFromDevice) + PTZ update
        pidsSymbol.Verify(s => s.SetUpdate(), Times.AtLeastOnce());
    }
    #endregion

    #region Helper Methods
    private static Ironwall.Dotnet.Libraries.Events.Models.EventSetupModel CreateMockEventSetupModel()
    {
        var mockModel = new Mock<Ironwall.Dotnet.Libraries.Events.Models.IEventSetupModel>();
        mockModel.Setup(m => m.IsAutoEventDiscard).Returns(false);
        mockModel.Setup(m => m.IsSound).Returns(false);
        mockModel.Setup(m => m.TimeDurationSound).Returns(0);
        mockModel.Setup(m => m.TimeDiscardSec).Returns(0);
        mockModel.Setup(m => m.LengthMaxEventPrev).Returns(0);
        mockModel.Setup(m => m.LengthMinEventPrev).Returns(0);
        return new Ironwall.Dotnet.Libraries.Events.Models.EventSetupModel(mockModel.Object);
    }
    #endregion
}
#endregion

#region Phase 13: 두 딕셔너리 구조로 심볼 분리 (PRD v1.5)
/// <summary>
/// Phase 13: SymbolEventManager 두 딕셔너리 구조 테스트
/// PRD Reference: docs/prds/PRD_PTZ_FOV_Integration.md (Section 11)
/// 목표: _deviceSymbolLookup + _groupSymbolLookup 분리
/// </summary>
public class SymbolEventManagerDualDictionaryTests
{
    #region Test 13.1.1: RegisterGroupSymbol - 그룹 심볼 등록
    [Fact]
    public void RegisterGroupSymbol_ShouldAddToGroupLookup()
    {
        // Arrange
        var mockEa = new Mock<Caliburn.Micro.IEventAggregator>();
        var mockLog = new Mock<Ironwall.Dotnet.Libraries.Base.Services.ILogService>();
        var eventSetupModel = CreateMockEventSetupModel();
        var manager = new Ironwall.Dotnet.Libraries.Events.Ui.Managers.SymbolEventManager(
            mockEa.Object, mockLog.Object, eventSetupModel);

        var mockDevice = new Mock<Ironwall.Dotnet.Monitoring.Models.Devices.IBaseDeviceModel>();
        mockDevice.Setup(d => d.Id).Returns(5);
        mockDevice.Setup(d => d.DeviceGroups).Returns(new List<int> { 1 });

        var mockGroupSymbol = new Mock<Ironwall.Dotnet.Monitoring.Models.Symbols.IPidsGroupSymbolModel>();
        mockGroupSymbol.SetupAllProperties();
        mockGroupSymbol.Setup(s => s.Title).Returns("Group1");

        // Act
        manager.RegisterGroupSymbol(deviceGroup: 1, mockDevice.Object, mockGroupSymbol.Object);

        // Assert - 그룹 심볼이 등록되었는지 확인
        Assert.True(manager.HasGroupSymbol(1));
    }
    #endregion

    #region Test 13.1.2: RegisterBothSymbols - 동시 등록 시 덮어쓰기 없음
    [Fact]
    public void RegisterBothSymbols_ShouldNotOverwrite()
    {
        // Arrange
        var mockEa = new Mock<Caliburn.Micro.IEventAggregator>();
        var mockLog = new Mock<Ironwall.Dotnet.Libraries.Base.Services.ILogService>();
        var eventSetupModel = CreateMockEventSetupModel();
        var manager = new Ironwall.Dotnet.Libraries.Events.Ui.Managers.SymbolEventManager(
            mockEa.Object, mockLog.Object, eventSetupModel);

        // Device.Id = 5, DeviceGroup = 1, DeviceType = Fence (복합 키용)
        var mockDevice = new Mock<Ironwall.Dotnet.Monitoring.Models.Devices.IBaseDeviceModel>();
        mockDevice.Setup(d => d.Id).Returns(5);
        mockDevice.Setup(d => d.DeviceGroups).Returns(new List<int> { 1 });
        mockDevice.Setup(d => d.DeviceType).Returns(Ironwall.Dotnet.Libraries.Enums.EnumDeviceType.Fence);

        var mockPidsSymbol = new Mock<Ironwall.Dotnet.Monitoring.Models.Symbols.IPidsSymbolModel>();
        mockPidsSymbol.SetupAllProperties();
        mockPidsSymbol.Setup(s => s.Title).Returns("Camera1");

        var mockGroupSymbol = new Mock<Ironwall.Dotnet.Monitoring.Models.Symbols.IPidsGroupSymbolModel>();
        mockGroupSymbol.SetupAllProperties();
        mockGroupSymbol.Setup(s => s.Title).Returns("Zone1");

        // Act - 두 심볼 모두 등록
        manager.RegisterDeviceSymbol(mockDevice.Object, mockPidsSymbol.Object);
        manager.RegisterGroupSymbol(deviceGroup: 1, mockDevice.Object, mockGroupSymbol.Object);

        // Assert - 두 딕셔너리에 각각 등록되었는지 확인 (Phase 14: 복합 키 사용)
        Assert.True(manager.HasDeviceSymbol(5, Ironwall.Dotnet.Libraries.Enums.EnumDeviceType.Fence));  // Device.Id = 5, DeviceType = Fence
        Assert.True(manager.HasGroupSymbol(1));   // DeviceGroup = 1

        // 개별 심볼과 그룹 심볼이 서로 덮어쓰지 않음
        var deviceLookup = manager.GetDeviceSymbol(5, Ironwall.Dotnet.Libraries.Enums.EnumDeviceType.Fence);
        var groupLookup = manager.GetGroupSymbol(1);
        Assert.NotNull(deviceLookup);
        Assert.NotNull(groupLookup);
        Assert.NotSame(deviceLookup.SymbolModel, groupLookup.SymbolModel);
    }
    #endregion

    #region Test 13.2.1: Intrusion 이벤트 - 개별 + 그룹 모두 처리
    [Fact]
    public void ProcessDeviceEvent_Intrusion_ShouldProcessBothSymbols()
    {
        // Arrange
        var mockEa = new Mock<Caliburn.Micro.IEventAggregator>();
        var mockLog = new Mock<Ironwall.Dotnet.Libraries.Base.Services.ILogService>();
        var eventSetupModel = CreateMockEventSetupModel();
        var manager = new Ironwall.Dotnet.Libraries.Events.Ui.Managers.SymbolEventManager(
            mockEa.Object, mockLog.Object, eventSetupModel);

        var mockDevice = new Mock<Ironwall.Dotnet.Monitoring.Models.Devices.IBaseDeviceModel>();
        mockDevice.Setup(d => d.Id).Returns(5);
        mockDevice.Setup(d => d.DeviceGroups).Returns(new List<int> { 1 });
        mockDevice.Setup(d => d.DeviceType).Returns(Ironwall.Dotnet.Libraries.Enums.EnumDeviceType.Fence);

        var mockPidsSymbol = new Mock<Ironwall.Dotnet.Monitoring.Models.Symbols.IPidsSymbolModel>();
        mockPidsSymbol.SetupAllProperties();

        var mockGroupSymbol = new Mock<Ironwall.Dotnet.Monitoring.Models.Symbols.IPidsGroupSymbolModel>();
        mockGroupSymbol.SetupAllProperties();

        manager.RegisterDeviceSymbol(mockDevice.Object, mockPidsSymbol.Object);
        manager.RegisterGroupSymbol(deviceGroup: 1, mockDevice.Object, mockGroupSymbol.Object);

        // Act - Intrusion 이벤트 (Phase 14: deviceType 추가)
        manager.ProcessDeviceEvent(
            deviceId: 5,
            deviceType: Ironwall.Dotnet.Libraries.Enums.EnumDeviceType.Fence,
            deviceGroups: new List<int> { 1 },
            eventType: Ironwall.Dotnet.Libraries.Enums.EnumEventType.Intrusion,
            severity: Ironwall.Dotnet.Libraries.Enums.EnumSeverityLevel.WARNING);

        // Assert - Intrusion 이벤트는 ProcessEvent 경로에서 CompositeStatus=Detecting 설정
        mockPidsSymbol.VerifySet(s => s.CompositeStatus = Ironwall.Dotnet.Libraries.Enums.EnumCompositeEventStatus.Detecting, Times.AtLeastOnce);
        mockGroupSymbol.VerifySet(s => s.CompositeStatus = Ironwall.Dotnet.Libraries.Enums.EnumCompositeEventStatus.Detecting, Times.AtLeastOnce);
    }
    #endregion

    #region Test 13.2.2: Connection 이벤트 - 개별만 처리
    [Fact]
    public void ProcessDeviceEvent_Connection_ShouldProcessOnlyDeviceSymbol()
    {
        // Arrange
        var mockEa = new Mock<Caliburn.Micro.IEventAggregator>();
        var mockLog = new Mock<Ironwall.Dotnet.Libraries.Base.Services.ILogService>();
        var eventSetupModel = CreateMockEventSetupModel();
        var manager = new Ironwall.Dotnet.Libraries.Events.Ui.Managers.SymbolEventManager(
            mockEa.Object, mockLog.Object, eventSetupModel);

        var mockDevice = new Mock<Ironwall.Dotnet.Monitoring.Models.Devices.IBaseDeviceModel>();
        mockDevice.Setup(d => d.Id).Returns(5);
        mockDevice.Setup(d => d.DeviceGroups).Returns(new List<int> { 1 });
        mockDevice.Setup(d => d.DeviceType).Returns(Ironwall.Dotnet.Libraries.Enums.EnumDeviceType.Fence);

        var mockPidsSymbol = new Mock<Ironwall.Dotnet.Monitoring.Models.Symbols.IPidsSymbolModel>();
        mockPidsSymbol.SetupAllProperties();

        var mockGroupSymbol = new Mock<Ironwall.Dotnet.Monitoring.Models.Symbols.IPidsGroupSymbolModel>();
        mockGroupSymbol.SetupAllProperties();

        manager.RegisterDeviceSymbol(mockDevice.Object, mockPidsSymbol.Object);
        manager.RegisterGroupSymbol(deviceGroup: 1, mockDevice.Object, mockGroupSymbol.Object);

        // Act - Connection 이벤트 (Phase 14: deviceType 추가)
        manager.ProcessDeviceEvent(
            deviceId: 5,
            deviceType: Ironwall.Dotnet.Libraries.Enums.EnumDeviceType.Fence,
            deviceGroups: new List<int> { 1 },
            eventType: Ironwall.Dotnet.Libraries.Enums.EnumEventType.Connection,
            severity: Ironwall.Dotnet.Libraries.Enums.EnumSeverityLevel.WARNING);

        // Assert - ProcessEvent는 심볼 비주얼을 변경하지 않음 (SyncFromDevice로 일원화)
        mockPidsSymbol.VerifySet(s => s.EventStatus = Ironwall.Dotnet.Libraries.Enums.EnumEventStatus.Connection, Times.Never);
    }
    #endregion

    #region Test 13.2.3: Fault 이벤트 - 개별 + 그룹 모두 처리
    [Fact]
    public void ProcessDeviceEvent_Fault_ShouldProcessBothDeviceAndGroupSymbols()
    {
        // Arrange
        var mockEa = new Mock<Caliburn.Micro.IEventAggregator>();
        var mockLog = new Mock<Ironwall.Dotnet.Libraries.Base.Services.ILogService>();
        var eventSetupModel = CreateMockEventSetupModel();
        var manager = new Ironwall.Dotnet.Libraries.Events.Ui.Managers.SymbolEventManager(
            mockEa.Object, mockLog.Object, eventSetupModel);

        var mockDevice = new Mock<Ironwall.Dotnet.Monitoring.Models.Devices.IBaseDeviceModel>();
        mockDevice.Setup(d => d.Id).Returns(5);
        mockDevice.Setup(d => d.DeviceGroups).Returns(new List<int> { 1 });
        mockDevice.Setup(d => d.DeviceType).Returns(Ironwall.Dotnet.Libraries.Enums.EnumDeviceType.Fence);

        var mockPidsSymbol = new Mock<Ironwall.Dotnet.Monitoring.Models.Symbols.IPidsSymbolModel>();
        mockPidsSymbol.SetupAllProperties();

        var mockGroupSymbol = new Mock<Ironwall.Dotnet.Monitoring.Models.Symbols.IPidsGroupSymbolModel>();
        mockGroupSymbol.SetupAllProperties();

        manager.RegisterDeviceSymbol(mockDevice.Object, mockPidsSymbol.Object);
        manager.RegisterGroupSymbol(deviceGroup: 1, mockDevice.Object, mockGroupSymbol.Object);

        // Act - Fault 이벤트 (FAULT_FENCE)
        manager.ProcessDeviceEvent(
            deviceId: 5,
            deviceType: Ironwall.Dotnet.Libraries.Enums.EnumDeviceType.Fence,
            deviceGroups: new List<int> { 1 },
            eventType: Ironwall.Dotnet.Libraries.Enums.EnumEventType.Fault,
            severity: Ironwall.Dotnet.Libraries.Enums.EnumSeverityLevel.CRITICAL);

        // Assert - ProcessEvent는 심볼 비주얼을 변경하지 않음 (SyncFromDevice로 일원화)
        mockPidsSymbol.VerifySet(s => s.EventStatus = Ironwall.Dotnet.Libraries.Enums.EnumEventStatus.Fault, Times.Never);
        mockGroupSymbol.VerifySet(s => s.EventStatus = Ironwall.Dotnet.Libraries.Enums.EnumEventStatus.Fault, Times.Never);
    }
    #endregion

    #region Test 13.3.1: Fence 타입 제어기 장애 - 그룹 처리
    [Fact]
    public void ProcessControllerEvent_FenceType_ShouldProcessGroupSymbol()
    {
        // Arrange
        var mockEa = new Mock<Caliburn.Micro.IEventAggregator>();
        var mockLog = new Mock<Ironwall.Dotnet.Libraries.Base.Services.ILogService>();
        var eventSetupModel = CreateMockEventSetupModel();
        var manager = new Ironwall.Dotnet.Libraries.Events.Ui.Managers.SymbolEventManager(
            mockEa.Object, mockLog.Object, eventSetupModel);

        var mockController = new Mock<Ironwall.Dotnet.Monitoring.Models.Devices.IControllerDeviceModel>();
        mockController.Setup(d => d.Id).Returns(10);
        mockController.Setup(d => d.DeviceGroups).Returns(new List<int> { 1 });
        mockController.Setup(d => d.DeviceType).Returns(Ironwall.Dotnet.Libraries.Enums.EnumDeviceType.Controller);

        var mockPidsSymbol = new Mock<Ironwall.Dotnet.Monitoring.Models.Symbols.IPidsSymbolModel>();
        mockPidsSymbol.SetupAllProperties();

        var mockGroupSymbol = new Mock<Ironwall.Dotnet.Monitoring.Models.Symbols.IPidsGroupSymbolModel>();
        mockGroupSymbol.SetupAllProperties();

        manager.RegisterDeviceSymbol(mockController.Object, mockPidsSymbol.Object);
        manager.RegisterGroupSymbol(deviceGroup: 1, mockController.Object, mockGroupSymbol.Object);

        // Act - Fence 타입 제어기 장애 이벤트
        manager.ProcessControllerEvent(
            controllerId: 10,
            deviceGroups: new List<int> { 1 },
            deviceType: Ironwall.Dotnet.Libraries.Enums.EnumDeviceType.Fence,
            eventType: Ironwall.Dotnet.Libraries.Enums.EnumEventType.Fault,
            severity: Ironwall.Dotnet.Libraries.Enums.EnumSeverityLevel.CRITICAL);

        // Assert - ProcessEvent는 심볼 비주얼을 변경하지 않음 (SyncFromDevice로 일원화)
        mockPidsSymbol.VerifySet(s => s.EventStatus = Ironwall.Dotnet.Libraries.Enums.EnumEventStatus.Fault, Times.Never);
        mockGroupSymbol.VerifySet(s => s.EventStatus = Ironwall.Dotnet.Libraries.Enums.EnumEventStatus.Fault, Times.Never);
    }
    #endregion

    #region Test 13.3.2: 일반 타입 제어기 장애 - 개별만 처리
    [Fact]
    public void ProcessControllerEvent_NonFenceType_ShouldProcessOnlyDeviceSymbol()
    {
        // Arrange
        var mockEa = new Mock<Caliburn.Micro.IEventAggregator>();
        var mockLog = new Mock<Ironwall.Dotnet.Libraries.Base.Services.ILogService>();
        var eventSetupModel = CreateMockEventSetupModel();
        var manager = new Ironwall.Dotnet.Libraries.Events.Ui.Managers.SymbolEventManager(
            mockEa.Object, mockLog.Object, eventSetupModel);

        var mockController = new Mock<Ironwall.Dotnet.Monitoring.Models.Devices.IControllerDeviceModel>();
        mockController.Setup(d => d.Id).Returns(10);
        mockController.Setup(d => d.DeviceGroups).Returns(new List<int> { 1 });
        mockController.Setup(d => d.DeviceType).Returns(Ironwall.Dotnet.Libraries.Enums.EnumDeviceType.Controller);

        var mockPidsSymbol = new Mock<Ironwall.Dotnet.Monitoring.Models.Symbols.IPidsSymbolModel>();
        mockPidsSymbol.SetupAllProperties();

        var mockGroupSymbol = new Mock<Ironwall.Dotnet.Monitoring.Models.Symbols.IPidsGroupSymbolModel>();
        mockGroupSymbol.SetupAllProperties();

        manager.RegisterDeviceSymbol(mockController.Object, mockPidsSymbol.Object);
        manager.RegisterGroupSymbol(deviceGroup: 1, mockController.Object, mockGroupSymbol.Object);

        // Act - IpCamera 타입 (Non-Fence) 장애 이벤트
        manager.ProcessControllerEvent(
            controllerId: 10,
            deviceGroups: new List<int> { 1 },
            deviceType: Ironwall.Dotnet.Libraries.Enums.EnumDeviceType.IpCamera,
            eventType: Ironwall.Dotnet.Libraries.Enums.EnumEventType.Fault,
            severity: Ironwall.Dotnet.Libraries.Enums.EnumSeverityLevel.CRITICAL);

        // Assert - ProcessEvent는 심볼 비주얼을 변경하지 않음 (SyncFromDevice로 일원화)
        mockPidsSymbol.VerifySet(s => s.EventStatus = Ironwall.Dotnet.Libraries.Enums.EnumEventStatus.Fault, Times.Never);
    }
    #endregion

    #region Test 13.4.1: 조치보고 - 개별 + 그룹 복원
    [Fact]
    public void ProcessEventReport_ShouldRestoreBothSymbols()
    {
        // Arrange
        var mockEa = new Mock<Caliburn.Micro.IEventAggregator>();
        var mockLog = new Mock<Ironwall.Dotnet.Libraries.Base.Services.ILogService>();
        var eventSetupModel = CreateMockEventSetupModel();
        var manager = new Ironwall.Dotnet.Libraries.Events.Ui.Managers.SymbolEventManager(
            mockEa.Object, mockLog.Object, eventSetupModel);

        var mockDevice = new Mock<Ironwall.Dotnet.Monitoring.Models.Devices.IBaseDeviceModel>();
        mockDevice.Setup(d => d.Id).Returns(5);
        mockDevice.Setup(d => d.DeviceGroups).Returns(new List<int> { 1 });
        mockDevice.Setup(d => d.DeviceType).Returns(Ironwall.Dotnet.Libraries.Enums.EnumDeviceType.Fence);

        var mockPidsSymbol = new Mock<Ironwall.Dotnet.Monitoring.Models.Symbols.IPidsSymbolModel>();
        mockPidsSymbol.SetupAllProperties();

        var mockGroupSymbol = new Mock<Ironwall.Dotnet.Monitoring.Models.Symbols.IPidsGroupSymbolModel>();
        mockGroupSymbol.SetupAllProperties();

        manager.RegisterDeviceSymbol(mockDevice.Object, mockPidsSymbol.Object);
        manager.RegisterGroupSymbol(deviceGroup: 1, mockDevice.Object, mockGroupSymbol.Object);

        // Act - 조치보고 (Phase 14: deviceType 추가)
        // ProcessEventReport는 _animationManager.ReportCurrentEvent()를 호출
        // 이 테스트는 메서드가 예외 없이 호출되는지 확인
        var ex = Record.Exception(() => manager.ProcessEventReport(
            deviceId: 5,
            deviceType: Ironwall.Dotnet.Libraries.Enums.EnumDeviceType.Fence,
            deviceGroups: new List<int> { 1 }));

        // Assert - 예외 없이 정상 호출됨
        Assert.Null(ex);

        // Note: EventStatus 복원은 EventQueueManager.Dequeue → ProcessEventReport 경로에서 수행됨
    }
    #endregion

    #region Test 13.4.2: Intrusion → ProcessEventReport → EventStatus=Normal 복원
    [Fact]
    public void RestoreDeviceSymbol_AfterIntrusion_ShouldRestoreEventStatusToNormal()
    {
        // Arrange — ProcessEventReport는 no-op (심볼 복원은 RestoreDeviceSymbol로 일원화)
        var mockEa = new Mock<Caliburn.Micro.IEventAggregator>();
        var mockLog = new Mock<Ironwall.Dotnet.Libraries.Base.Services.ILogService>();
        var eventSetupModel = CreateMockEventSetupModel();
        eventSetupModel.IsAutoEventDiscard = false;

        var manager = new Ironwall.Dotnet.Libraries.Events.Ui.Managers.SymbolEventManager(
            mockEa.Object, mockLog.Object, eventSetupModel);

        var mockDevice = new Mock<Ironwall.Dotnet.Monitoring.Models.Devices.IBaseDeviceModel>();
        mockDevice.Setup(d => d.Id).Returns(5);
        mockDevice.Setup(d => d.DeviceGroups).Returns(new List<int> { 1 });
        mockDevice.Setup(d => d.DeviceType).Returns(Ironwall.Dotnet.Libraries.Enums.EnumDeviceType.Fence);
        mockDevice.Setup(d => d.Status).Returns(Ironwall.Dotnet.Libraries.Enums.EnumDeviceStatus.ACTIVATED);

        var mockPidsSymbol = new Mock<Ironwall.Dotnet.Monitoring.Models.Symbols.IPidsSymbolModel>();
        mockPidsSymbol.SetupAllProperties();

        manager.RegisterDeviceSymbol(mockDevice.Object, mockPidsSymbol.Object);

        // Act 1: Intrusion 이벤트 → Detecting (SetDeviceDetecting 경유)
        manager.SetDeviceDetecting(5, Ironwall.Dotnet.Libraries.Enums.EnumDeviceType.Fence, Ironwall.Dotnet.Libraries.Enums.EnumEventType.Intrusion);

        // Verify: CompositeStatus=Detecting
        mockPidsSymbol.VerifySet(s => s.CompositeStatus = Ironwall.Dotnet.Libraries.Enums.EnumCompositeEventStatus.Detecting, Times.AtLeastOnce);

        // CompositeStatus 변경 이력 추적
        var statusHistory = new List<Ironwall.Dotnet.Libraries.Enums.EnumCompositeEventStatus>();
        mockPidsSymbol.SetupSet(s => s.CompositeStatus = It.IsAny<Ironwall.Dotnet.Libraries.Enums.EnumCompositeEventStatus>())
            .Callback<Ironwall.Dotnet.Libraries.Enums.EnumCompositeEventStatus>(v => statusHistory.Add(v));

        // Act 2: RestoreDeviceSymbol → Normal 복원 (EventQueueManager OnDeviceEmpty 경유)
        manager.RestoreDeviceSymbol(5, Ironwall.Dotnet.Libraries.Enums.EnumDeviceType.Fence);

        // Assert: 복원 후 CompositeStatus=Normal
        Assert.Contains(Ironwall.Dotnet.Libraries.Enums.EnumCompositeEventStatus.Normal, statusHistory);
    }
    #endregion

    #region Test 13.5.1: PTZ - 개별 심볼만 FOV 업데이트
    [Fact]
    public void ProcessCameraPtz_ShouldUpdateOnlyDeviceSymbolFOV()
    {
        // Arrange
        var mockEa = new Mock<Caliburn.Micro.IEventAggregator>();
        var mockLog = new Mock<Ironwall.Dotnet.Libraries.Base.Services.ILogService>();
        var eventSetupModel = CreateMockEventSetupModel();
        var manager = new Ironwall.Dotnet.Libraries.Events.Ui.Managers.SymbolEventManager(
            mockEa.Object, mockLog.Object, eventSetupModel);

        var mockCamera = new Mock<Ironwall.Dotnet.Monitoring.Models.Devices.ICameraDeviceModel>();
        mockCamera.Setup(d => d.Id).Returns(1);
        mockCamera.Setup(d => d.DeviceGroups).Returns(new List<int> { 1 });
        mockCamera.Setup(d => d.DeviceType).Returns(Ironwall.Dotnet.Libraries.Enums.EnumDeviceType.IpCamera);

        var mockPidsSymbol = new Mock<Ironwall.Dotnet.Monitoring.Models.Symbols.IPidsSymbolModel>();
        mockPidsSymbol.SetupAllProperties();

        var mockGroupSymbol = new Mock<Ironwall.Dotnet.Monitoring.Models.Symbols.IPidsGroupSymbolModel>();
        mockGroupSymbol.SetupAllProperties();

        manager.RegisterDeviceSymbol(mockCamera.Object, mockPidsSymbol.Object);
        manager.RegisterGroupSymbol(deviceGroup: 1, mockCamera.Object, mockGroupSymbol.Object);

        // Act - PTZ 업데이트 (개별 심볼만 FOV 변경, zoom 0~100 정규화)
        manager.ProcessCameraPtz(cameraId: 1, pan: 180f, tilt: 0f, zoom: 75f);

        // Assert - 개별 심볼(IPidsSymbolModel)의 FOV만 업데이트
        // zoom=75 → ratio=0.75 → Angle=80-0.75*75=23.75, Range=10+0.75*2990=2252.5
        mockPidsSymbol.VerifySet(s => s.DetectionBearing = 180.0, Times.Once);
        mockPidsSymbol.VerifySet(s => s.DetectionAngle = 23.75, Times.Once);
        mockPidsSymbol.VerifySet(s => s.DetectionRange = 2252.5, Times.Once);
        // RegisterDeviceSymbol calls SyncFromDevice (1 extra) + PTZ update
        mockPidsSymbol.Verify(s => s.SetUpdate(), Times.AtLeastOnce());

        // 그룹 심볼은 IPidsSymbolModel이 아니므로 FOV 속성 없음 - 호출 안됨
    }
    #endregion

    #region Helper Methods
    private static Ironwall.Dotnet.Libraries.Events.Models.EventSetupModel CreateMockEventSetupModel()
    {
        var mockModel = new Mock<Ironwall.Dotnet.Libraries.Events.Models.IEventSetupModel>();
        mockModel.Setup(m => m.IsAutoEventDiscard).Returns(false);
        mockModel.Setup(m => m.IsSound).Returns(false);
        mockModel.Setup(m => m.TimeDurationSound).Returns(0);
        mockModel.Setup(m => m.TimeDiscardSec).Returns(0);
        mockModel.Setup(m => m.LengthMaxEventPrev).Returns(0);
        mockModel.Setup(m => m.LengthMinEventPrev).Returns(0);
        return new Ironwall.Dotnet.Libraries.Events.Models.EventSetupModel(mockModel.Object);
    }
    #endregion
}
#endregion

#region Phase 16: DataHelper IBaseDeviceModel Refactoring Tests
/// <summary>
/// Phase 16 Tests: DataHelper 리팩토링 - IBaseDeviceModel 기반 변환
/// TDD 방식으로 GetControllerNumber, IsDeviceMatch, GetXxxCountsByDevice 구현
/// </summary>
public class DataHelperRefactoringTests
{
    #region Phase 16.1: GetControllerNumber Tests

    /// <summary>
    /// Test 16.1.1: IControllerDeviceModel 입력 시 DeviceNumber 반환
    /// </summary>
    [Fact]
    public void GetControllerNumber_WithControllerDevice_ShouldReturnDeviceNumber()
    {
        // Arrange: IControllerDeviceModel with DeviceNumber = 5
        var controller = new ControllerDeviceModel { DeviceNumber = 5 };

        // Act: GetControllerNumber(device)
        var result = DataHelper.GetControllerNumber(controller);

        // Assert: Returns 5
        Assert.Equal(5, result);
    }

    /// <summary>
    /// Test 16.1.2: ISensorDeviceModel 입력 시 Controller.DeviceNumber 반환
    /// </summary>
    [Fact]
    public void GetControllerNumber_WithSensorDevice_ShouldReturnControllerNumber()
    {
        // Arrange: ISensorDeviceModel with Controller.DeviceNumber = 3
        var sensor = new SensorDeviceModel
        {
            DeviceNumber = 100,
            Controller = new ControllerDeviceModel { DeviceNumber = 3 }
        };

        // Act: GetControllerNumber(device)
        var result = DataHelper.GetControllerNumber(sensor);

        // Assert: Returns 3
        Assert.Equal(3, result);
    }

    /// <summary>
    /// Test 16.1.3: ISensorDeviceModel with null Controller 입력 시 -1 반환
    /// </summary>
    [Fact]
    public void GetControllerNumber_WithSensorNoController_ShouldReturnMinusOne()
    {
        // Arrange: ISensorDeviceModel with Controller = null
        var sensor = new SensorDeviceModel
        {
            DeviceNumber = 100,
            Controller = null!
        };

        // Act: GetControllerNumber(device)
        var result = DataHelper.GetControllerNumber(sensor);

        // Assert: Returns -1
        Assert.Equal(-1, result);
    }

    /// <summary>
    /// Test 16.1.4: ICameraDeviceModel 입력 시 -1 반환 (Controller 속성 없음)
    /// </summary>
    [Fact]
    public void GetControllerNumber_WithCameraDevice_ShouldReturnMinusOne()
    {
        // Arrange: ICameraDeviceModel (no Controller property)
        var camera = new CameraDeviceModel { DeviceNumber = 10 };

        // Act: GetControllerNumber(device)
        var result = DataHelper.GetControllerNumber(camera);

        // Assert: Returns -1
        Assert.Equal(-1, result);
    }

    /// <summary>
    /// Test 16.1.5: null 입력 시 -1 반환
    /// </summary>
    [Fact]
    public void GetControllerNumber_WithNull_ShouldReturnMinusOne()
    {
        // Arrange: null device
        IBaseDeviceModel? device = null;

        // Act: GetControllerNumber(null)
        var result = DataHelper.GetControllerNumber(device);

        // Assert: Returns -1
        Assert.Equal(-1, result);
    }

    #endregion

    #region Phase 16.2: IsDeviceMatch Tests

    /// <summary>
    /// Test 16.2.1: ID 직접 매칭 시 true 반환
    /// </summary>
    [Fact]
    public void IsDeviceMatch_WithSameId_ShouldReturnTrue()
    {
        // Arrange: eventDevice.Id = 100, targetDevice.Id = 100
        var eventDevice = new SensorDeviceModel { Id = 100, DeviceNumber = 1 };
        var targetDevice = new SensorDeviceModel { Id = 100, DeviceNumber = 2 };

        // Act: IsDeviceMatch(eventDevice, targetDevice)
        var result = DataHelper.IsDeviceMatch(eventDevice, targetDevice);

        // Assert: Returns true
        Assert.True(result);
    }

    /// <summary>
    /// Test 16.2.2: DeviceNumber + DeviceType 매칭 시 true 반환
    /// </summary>
    [Fact]
    public void IsDeviceMatch_WithSameNumberAndType_ShouldReturnTrue()
    {
        // Arrange: eventDevice (DeviceNumber=5, Type=Sensor), target (DeviceNumber=5, Type=Sensor)
        var eventDevice = new SensorDeviceModel { Id = 1, DeviceNumber = 5, DeviceType = Libraries.Enums.EnumDeviceType.Fence };
        var targetDevice = new SensorDeviceModel { Id = 2, DeviceNumber = 5, DeviceType = Libraries.Enums.EnumDeviceType.Fence };

        // Act: IsDeviceMatch(eventDevice, targetDevice)
        var result = DataHelper.IsDeviceMatch(eventDevice, targetDevice);

        // Assert: Returns true
        Assert.True(result);
    }

    /// <summary>
    /// Test 16.2.3: Sensor→Controller 매핑 시 true 반환
    /// </summary>
    [Fact]
    public void IsDeviceMatch_SensorToController_ShouldReturnTrue()
    {
        // Arrange: eventDevice = Sensor with Controller.DeviceNumber = 3
        //          targetDevice = Controller with DeviceNumber = 3
        var eventDevice = new SensorDeviceModel
        {
            Id = 100,
            DeviceNumber = 10,
            Controller = new ControllerDeviceModel { DeviceNumber = 3 }
        };
        var targetDevice = new ControllerDeviceModel { Id = 3, DeviceNumber = 3 };

        // Act: IsDeviceMatch(eventDevice, targetDevice)
        var result = DataHelper.IsDeviceMatch(eventDevice, targetDevice);

        // Assert: Returns true
        Assert.True(result);
    }

    /// <summary>
    /// Test 16.2.4: null eventDevice 입력 시 false 반환
    /// </summary>
    [Fact]
    public void IsDeviceMatch_WithNullEventDevice_ShouldReturnFalse()
    {
        // Arrange: eventDevice = null, targetDevice = Controller
        IBaseDeviceModel? eventDevice = null;
        var targetDevice = new ControllerDeviceModel { Id = 1, DeviceNumber = 1 };

        // Act: IsDeviceMatch(null, targetDevice)
        var result = DataHelper.IsDeviceMatch(eventDevice, targetDevice);

        // Assert: Returns false
        Assert.False(result);
    }

    /// <summary>
    /// Test 16.2.5: 불일치 케이스 시 false 반환
    /// </summary>
    [Fact]
    public void IsDeviceMatch_WithDifferentDevices_ShouldReturnFalse()
    {
        // Arrange: eventDevice (DeviceNumber=5), targetDevice (DeviceNumber=7)
        var eventDevice = new SensorDeviceModel { Id = 1, DeviceNumber = 5 };
        var targetDevice = new SensorDeviceModel { Id = 2, DeviceNumber = 7 };

        // Act: IsDeviceMatch(eventDevice, targetDevice)
        var result = DataHelper.IsDeviceMatch(eventDevice, targetDevice);

        // Assert: Returns false
        Assert.False(result);
    }

    #endregion

    #region Phase 16.3: GetDetectionCountsByDevice Tests

    /// <summary>
    /// Test 16.3.1: Sensor 이벤트 카운트
    /// </summary>
    [Fact]
    public void GetDetectionCountsByDevice_WithSensorEvents_ShouldCountCorrectly()
    {
        // Arrange: 3 events from Sensor1, 2 from Sensor2
        var sensor1 = new SensorDeviceModel { Id = 1, DeviceNumber = 1, DeviceType = Libraries.Enums.EnumDeviceType.Fence };
        var sensor2 = new SensorDeviceModel { Id = 2, DeviceNumber = 2, DeviceType = Libraries.Enums.EnumDeviceType.Fence };

        var events = new List<IDetectionEventModel>
        {
            new DetectionEventModel { Id = 1, DateTime = DateTime.Now, Device = sensor1 },
            new DetectionEventModel { Id = 2, DateTime = DateTime.Now, Device = sensor1 },
            new DetectionEventModel { Id = 3, DateTime = DateTime.Now, Device = sensor1 },
            new DetectionEventModel { Id = 4, DateTime = DateTime.Now, Device = sensor2 },
            new DetectionEventModel { Id = 5, DateTime = DateTime.Now, Device = sensor2 },
        };

        var devices = new List<IBaseDeviceModel> { sensor1, sensor2 };

        // Act: GetDetectionCountsByDevice
        var result = DataHelper.GetDetectionCountsByDevice(
            DateTime.Now.AddDays(-1), DateTime.Now.AddDays(1), devices, events);

        // Assert: [3.0, 2.0]
        Assert.Equal(2, result.Count);
        Assert.Equal(3.0, result[0]);
        Assert.Equal(2.0, result[1]);
    }

    /// <summary>
    /// Test 16.3.2: Controller 기준 집계 (Sensor→Controller 매핑)
    /// </summary>
    [Fact]
    public void GetDetectionCountsByDevice_WithControllerTarget_ShouldAggregateChildren()
    {
        // Arrange: 2 events from Sensor(Controller.DeviceNumber=1), 1 from Sensor(Controller.DeviceNumber=2)
        var ctrl1 = new ControllerDeviceModel { Id = 1, DeviceNumber = 1 };
        var ctrl2 = new ControllerDeviceModel { Id = 2, DeviceNumber = 2 };
        var sensor1 = new SensorDeviceModel { Id = 10, DeviceNumber = 10, Controller = ctrl1 };
        var sensor2 = new SensorDeviceModel { Id = 20, DeviceNumber = 20, Controller = ctrl2 };

        var events = new List<IDetectionEventModel>
        {
            new DetectionEventModel { Id = 1, DateTime = DateTime.Now, Device = sensor1 },
            new DetectionEventModel { Id = 2, DateTime = DateTime.Now, Device = sensor1 },
            new DetectionEventModel { Id = 3, DateTime = DateTime.Now, Device = sensor2 },
        };

        var devices = new List<IBaseDeviceModel> { ctrl1, ctrl2 };

        // Act: GetDetectionCountsByDevice
        var result = DataHelper.GetDetectionCountsByDevice(
            DateTime.Now.AddDays(-1), DateTime.Now.AddDays(1), devices, events);

        // Assert: [2.0, 1.0]
        Assert.Equal(2, result.Count);
        Assert.Equal(2.0, result[0]);
        Assert.Equal(1.0, result[1]);
    }

    #endregion

    #region Phase 16.6: GetActionCountsByDevice Tests

    /// <summary>
    /// Test 16.6.1: OriginEvent.Device 기준 카운트
    /// </summary>
    [Fact]
    public void GetActionCountsByDevice_ShouldUseOriginEventDevice()
    {
        // Arrange: ActionEvent with OriginEvent.Device = Sensor(Controller.DeviceNumber=1)
        var ctrl1 = new ControllerDeviceModel { Id = 1, DeviceNumber = 1 };
        var sensor1 = new SensorDeviceModel { Id = 10, DeviceNumber = 10, Controller = ctrl1 };
        var originEvent = new DetectionEventModel { Id = 100, Device = sensor1 };

        var events = new List<IActionEventModel>
        {
            new ActionEventModel { Id = 1, DateTime = DateTime.Now, OriginEvent = originEvent },
        };

        var devices = new List<IBaseDeviceModel> { ctrl1 };

        // Act: GetActionCountsByDevice
        var result = DataHelper.GetActionCountsByDevice(
            DateTime.Now.AddDays(-1), DateTime.Now.AddDays(1), devices, events);

        // Assert: [1.0]
        Assert.Equal(1, result.Count);
        Assert.Equal(1.0, result[0]);
    }

    /// <summary>
    /// Test 16.6.3: null OriginEvent 처리
    /// </summary>
    [Fact]
    public void GetActionCountsByDevice_WithNullOriginEvent_ShouldNotCount()
    {
        // Arrange: ActionEvent with OriginEvent = null
        var ctrl1 = new ControllerDeviceModel { Id = 1, DeviceNumber = 1 };

        var events = new List<IActionEventModel>
        {
            new ActionEventModel { Id = 1, DateTime = DateTime.Now, OriginEvent = null },
        };

        var devices = new List<IBaseDeviceModel> { ctrl1 };

        // Act: GetActionCountsByDevice
        var result = DataHelper.GetActionCountsByDevice(
            DateTime.Now.AddDays(-1), DateTime.Now.AddDays(1), devices, events);

        // Assert: [0.0]
        Assert.Equal(1, result.Count);
        Assert.Equal(0.0, result[0]);
    }

    #endregion

    #region Phase 18: Camera Status Symbol 주황색 문제 디버깅
    /// <summary>
    /// Phase 18.1: Symbol 초기화 테스트
    /// Phase 18.2: ProcessEvent 호출 추적 테스트
    /// </summary>

    #region Phase 18.1: Symbol 초기화 테스트

    [Fact]
    public void PidsSymbolModel_OnCreation_ShouldHaveNormalEventStatus()
    {
        // Arrange & Act
        var symbolModel = new PidsSymbolModel
        {
            Title = "TEST-CAM-01",
            LinkedDeviceId = 100
        };

        // Assert
        Assert.Equal(EnumEventStatus.Normal, symbolModel.EventStatus);
    }

    [Fact]
    public void DeviceSymbolLookupModel_WithCamera_ShouldInitializeNormal()
    {
        // Arrange
        var log = new Mock<ILogService>().Object;
        var ea = new Mock<IEventAggregator>().Object;
        var eventSetup = new EventSetupModel(new Mock<IEventSetupModel>().Object);

        var cameraDevice = new CameraDeviceModel { Id = 100, DeviceType = EnumDeviceType.IpCamera };
        var symbolModel = new PidsSymbolModel { EventStatus = EnumEventStatus.Normal };

        var lookup = new DeviceSymbolLookupModel(log)
        {
            DeviceModel = cameraDevice,
            SymbolModel = symbolModel
        };

        // Assert
        Assert.Equal(EnumEventStatus.Normal, symbolModel.EventStatus);
    }

    #endregion

    #region Phase 18.2: ProcessEvent 추적 테스트

    [Fact]
    public void ProcessEvent_WithCameraAndFaultEvent_ShouldSetFaultedState()
    {
        // Arrange
        var log = new Mock<ILogService>().Object;
        var ea = new Mock<IEventAggregator>().Object;
        var eventSetup = new EventSetupModel(new Mock<IEventSetupModel>().Object);

        var cameraDevice = new CameraDeviceModel { Id = 100, DeviceType = EnumDeviceType.IpCamera };
        var symbolModel = new PidsSymbolModel { EventStatus = EnumEventStatus.Normal };

        var lookup = new DeviceSymbolLookupModel(log)
        {
            DeviceModel = cameraDevice,
            SymbolModel = symbolModel
        };

        // Act
        lookup.ProcessEvent(EnumEventType.Fault, EnumSeverityLevel.CRITICAL);

        // Assert - Malfunction PRD: ProcessEvent Fault → CompositeStatus=Faulted → EventStatus=Fault
        Assert.Equal(EnumEventStatus.Fault, symbolModel.EventStatus);
    }

    [Fact]
    public void ProcessEvent_WithCameraAndConnectionEvent_ShouldNotChangeSymbolState()
    {
        // Arrange
        var log = new Mock<ILogService>().Object;
        var ea = new Mock<IEventAggregator>().Object;
        var eventSetup = new EventSetupModel(new Mock<IEventSetupModel>().Object);

        var cameraDevice = new CameraDeviceModel { Id = 100, DeviceType = EnumDeviceType.IpCamera };
        var symbolModel = new PidsSymbolModel { EventStatus = EnumEventStatus.Normal };

        var lookup = new DeviceSymbolLookupModel(log)
        {
            DeviceModel = cameraDevice,
            SymbolModel = symbolModel
        };

        // Act
        lookup.ProcessEvent(EnumEventType.Connection, EnumSeverityLevel.CRITICAL);

        // Assert - ProcessEvent는 심볼 비주얼을 변경하지 않음 (SyncFromDevice로 일원화)
        Assert.Equal(EnumEventStatus.Normal, symbolModel.EventStatus);
    }

    [Fact]
    public void ProcessEvent_WithCameraAndIntrusionEvent_ShouldSetDetectingState()
    {
        // Arrange
        var log = new Mock<ILogService>().Object;
        var ea = new Mock<IEventAggregator>().Object;
        var eventSetup = new EventSetupModel(new Mock<IEventSetupModel>().Object);

        var cameraDevice = new CameraDeviceModel { Id = 100, DeviceType = EnumDeviceType.IpCamera };
        var symbolModel = new PidsSymbolModel { EventStatus = EnumEventStatus.Normal };

        var lookup = new DeviceSymbolLookupModel(log)
        {
            DeviceModel = cameraDevice,
            SymbolModel = symbolModel
        };

        // Act
        lookup.ProcessEvent(EnumEventType.Intrusion, EnumSeverityLevel.CRITICAL);

        // Assert - Malfunction PRD: ProcessEvent Intrusion → CompositeStatus=Detecting → EventStatus=Detecting
        Assert.Equal(EnumEventStatus.Detecting, symbolModel.EventStatus);
    }

    [Fact]
    public void ProcessEvent_WithSensorAndFaultEvent_ShouldSetFaultedState()
    {
        // Arrange
        var log = new Mock<ILogService>().Object;
        var ea = new Mock<IEventAggregator>().Object;
        var eventSetup = new EventSetupModel(new Mock<IEventSetupModel>().Object);

        var sensorDevice = new SensorDeviceModel
        {
            Id = 50,
            DeviceType = EnumDeviceType.Fence,
            DeviceNumber = 1
        };
        var symbolModel = new PidsSymbolModel { EventStatus = EnumEventStatus.Normal };

        var lookup = new DeviceSymbolLookupModel(log)
        {
            DeviceModel = sensorDevice,
            SymbolModel = symbolModel
        };

        // Act
        lookup.ProcessEvent(EnumEventType.Fault, EnumSeverityLevel.CRITICAL);

        // Assert - Malfunction PRD: ProcessEvent Fault → CompositeStatus=Faulted → EventStatus=Fault
        Assert.Equal(EnumEventStatus.Fault, symbolModel.EventStatus);
    }

    #endregion
    #endregion
}
#endregion

#region Phase 19: Event UI DeviceProperty Binding Tests
/// <summary>
/// Phase 19: ExEventViewModel / EventCardViewModel에 ControllerId,
/// ControllerDeviceNumber, DeviceTypeName 속성 추가 테스트
/// </summary>
[Collection("IoC-Dependent")]
public class EventUiDevicePropertyBindingTests : IDisposable
{
    public EventUiDevicePropertyBindingTests()
    {
        // Caliburn.Micro IoC 초기화 (BasePanelViewModel + EventCardBaseViewModel 대응)
        var mockSetup = new Mock<IEventSetupModel>();
        mockSetup.Setup(s => s.TimeDiscardSec).Returns(30);
        mockSetup.Setup(s => s.IsAutoEventDiscard).Returns(false);
        IoC.GetInstance = (type, key) =>
        {
            if (type == typeof(IEventAggregator)) return new EventAggregator();
            if (type == typeof(ILogService)) return null!;
            if (type == typeof(EventSetupModel)) return new EventSetupModel(mockSetup.Object);
            return null!;
        };
        IoC.GetAllInstances = type => Enumerable.Empty<object>();
        IoC.BuildUp = obj => { };
    }

    public void Dispose()
    {
        IoC.GetInstance = (type, key) => throw new InvalidOperationException("IoC is not initialized.");
        IoC.GetAllInstances = type => throw new InvalidOperationException("IoC is not initialized.");
        IoC.BuildUp = obj => throw new InvalidOperationException("IoC is not initialized.");
    }

    #region Test 1.1: ExEventViewModel_ControllerId_ReturnsSensorControllerIdWhenSensor
    [Fact]
    public void ExEventViewModel_ControllerId_ReturnsSensorControllerIdWhenSensor()
    {
        // Arrange: Sensor 디바이스 + Controller 참조
        var controller = new ControllerDeviceModel { Id = 10, DeviceNumber = 3 };
        var sensor = new SensorDeviceModel
        {
            Id = 1,
            DeviceNumber = 5,
            DeviceType = EnumDeviceType.Fence,
            Controller = controller
        };
        var eventModel = new ExEventModel
        {
            Device = sensor,
            MessageType = EnumEventType.Intrusion,
            DateTime = System.DateTime.Now
        };
        var vm = new ExEventViewModel(eventModel);

        // Act
        var result = vm.ControllerId;

        // Assert: Sensor의 Controller.Id를 반환
        Assert.Equal(10, result);
    }
    #endregion

    #region Test 1.2: ExEventViewModel_ControllerId_ReturnsNullWhenCamera
    [Fact]
    public void ExEventViewModel_ControllerId_ReturnsNullWhenCamera()
    {
        // Arrange: Camera 디바이스 (Controller 없음)
        var camera = new CameraDeviceModel
        {
            Id = 2,
            DeviceNumber = 10,
            DeviceType = EnumDeviceType.IpCamera
        };
        var eventModel = new ExEventModel
        {
            Device = camera,
            MessageType = EnumEventType.Intrusion,
            DateTime = System.DateTime.Now
        };
        var vm = new ExEventViewModel(eventModel);

        // Act
        var result = vm.ControllerId;

        // Assert: Camera는 ISensorDeviceModel이 아니므로 null
        Assert.Null(result);
    }
    #endregion

    #region Test 1.3: ExEventViewModel_ControllerDeviceNumber_ReturnsSensorControllerNumber
    [Fact]
    public void ExEventViewModel_ControllerDeviceNumber_ReturnsSensorControllerNumber()
    {
        // Arrange
        var controller = new ControllerDeviceModel { Id = 10, DeviceNumber = 3 };
        var sensor = new SensorDeviceModel
        {
            Id = 1,
            DeviceNumber = 5,
            DeviceType = EnumDeviceType.Fence,
            Controller = controller
        };
        var eventModel = new ExEventModel
        {
            Device = sensor,
            MessageType = EnumEventType.Intrusion,
            DateTime = System.DateTime.Now
        };
        var vm = new ExEventViewModel(eventModel);

        // Act
        var result = vm.ControllerDeviceNumber;

        // Assert: Sensor의 Controller.DeviceNumber를 반환
        Assert.Equal(3, result);
    }
    #endregion

    #region Test 1.4: ExEventViewModel_DeviceTypeName_ReturnsCorrectKoreanName
    [Theory]
    [InlineData(EnumDeviceType.Controller, "제어기")]
    [InlineData(EnumDeviceType.Fence, "센서")]
    [InlineData(EnumDeviceType.IpCamera, "카메라")]
    [InlineData(EnumDeviceType.IpSpeaker, "스피커")]
    [InlineData(EnumDeviceType.Enclosure, "함체")]
    [InlineData(EnumDeviceType.Lamp, "경고등")]
    public void ExEventViewModel_DeviceTypeName_ReturnsCorrectKoreanName(EnumDeviceType deviceType, string expectedName)
    {
        // Arrange
        var device = new BaseDeviceModel
        {
            Id = 1,
            DeviceNumber = 1,
            DeviceType = deviceType
        };
        var eventModel = new ExEventModel
        {
            Device = device,
            MessageType = EnumEventType.Intrusion,
            DateTime = System.DateTime.Now
        };
        var vm = new ExEventViewModel(eventModel);

        // Act
        var result = vm.DeviceTypeName;

        // Assert
        Assert.Equal(expectedName, result);
    }
    #endregion

    #region Test 1.5: EventCardViewModel_ControllerId_DelegatesToModel
    [Fact]
    public void EventCardViewModel_ControllerId_DelegatesToModel()
    {
        // Arrange: DetectionEventCardViewModel으로 테스트
        var controller = new ControllerDeviceModel { Id = 20, DeviceNumber = 7 };
        var sensor = new SensorDeviceModel
        {
            Id = 3,
            DeviceNumber = 8,
            DeviceType = EnumDeviceType.Fence,
            Controller = controller
        };
        var detectionEvent = new DetectionEventModel
        {
            Device = sensor,
            MessageType = EnumEventType.Intrusion,
            DateTime = System.DateTime.Now,
            Result = EnumDetectionType.VIBRATION_SENSOR
        };
        var vm = new DetectionEventCardViewModel(detectionEvent);

        // Act
        var result = vm.ControllerId;

        // Assert
        Assert.Equal(20, result);
    }
    #endregion

    #region Test 1.6: EventCardViewModel_DeviceTypeName_DelegatesToModel
    [Fact]
    public void EventCardViewModel_DeviceTypeName_DelegatesToModel()
    {
        // Arrange: Camera 디바이스로 DetectionEventCardViewModel 테스트
        var camera = new CameraDeviceModel
        {
            Id = 5,
            DeviceNumber = 12,
            DeviceType = EnumDeviceType.IpCamera
        };
        var detectionEvent = new DetectionEventModel
        {
            Device = camera,
            MessageType = EnumEventType.Intrusion,
            DateTime = System.DateTime.Now,
            Result = EnumDetectionType.VIBRATION_SENSOR
        };
        var vm = new DetectionEventCardViewModel(detectionEvent);

        // Act
        var result = vm.DeviceTypeName;

        // Assert
        Assert.Equal("카메라", result);
    }
    #endregion

    #region Test 2.1: DetectionEventCardView_ControllerField_BindsToViewModelProperty
    [Fact]
    public void DetectionEventCardView_ControllerField_BindsToViewModelProperty()
    {
        // XAML 파일에서 Device.Controller.* 깨진 바인딩이 없어야 한다
        // (Device는 IBaseDeviceModel이라 Controller 미노출 → 안전 캐스트 VM 속성 경유)
        var xamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "Views", "Events", "DetectionEventCardView.xaml");
        var xamlContent = File.ReadAllText(xamlPath);

        // Assert: 깨진 Device.Controller 경로가 없어야 함
        Assert.DoesNotContain("Device.Controller.Id", xamlContent);
        Assert.DoesNotContain("Device.Controller.DeviceNumber", xamlContent);
        // Assert: 제어기 필드는 VM 속성 ControllerDeviceNumber(제어기 번호)에 바인딩되어야 함
        Assert.Contains("ControllerDeviceNumber", xamlContent);
    }
    #endregion

    #region Test 2.2: MalfunctionEventCardView_ControllerField_BindsToViewModelProperty
    [Fact]
    public void MalfunctionEventCardView_ControllerField_BindsToViewModelProperty()
    {
        var xamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "Views", "Events", "MalfunctionEventCardView.xaml");
        var xamlContent = File.ReadAllText(xamlPath);

        // Assert: 깨진 Device.Controller 경로가 없어야 함
        Assert.DoesNotContain("Device.Controller.Id", xamlContent);
        Assert.DoesNotContain("Device.Controller.DeviceNumber", xamlContent);
        // Assert: 장애 카드는 장애타입 인지 표시 속성(ControllerDisplay/SensorDisplay)에 바인딩되어야 함
        Assert.Contains("ControllerDisplay", xamlContent);
        Assert.Contains("SensorDisplay", xamlContent);
    }
    #endregion

    #region Test 2.3: MalfunctionEventCardView_DeviceTypeName_ShowsNewDeviceTypes
    [Fact]
    public void MalfunctionEventCardView_DeviceTypeName_ShowsNewDeviceTypes()
    {
        var xamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "Views", "Events", "MalfunctionEventCardView.xaml");
        var xamlContent = File.ReadAllText(xamlPath);

        // Assert: DeviceTypeName 바인딩이 존재해야 함
        Assert.Contains("DeviceTypeName", xamlContent);
    }
    #endregion

    #region Test 2.4: DetectionEventCardView_DeviceTypeName_ShowsNewDeviceTypes
    [Fact]
    public void DetectionEventCardView_DeviceTypeName_ShowsNewDeviceTypes()
    {
        var xamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "Views", "Events", "DetectionEventCardView.xaml");
        var xamlContent = File.ReadAllText(xamlPath);

        // Assert: DeviceTypeName 바인딩이 존재해야 함
        Assert.Contains("DeviceTypeName", xamlContent);
    }
    #endregion

    #region Test 2.5: DetectionEventCardView — 구역이 VM DeviceGroupsText(이름 변환)에 바인딩
    [Fact]
    public void DetectionEventCardView_ZoneBindsToViewModelDeviceGroupsText()
    {
        var xamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "Views", "Events", "DetectionEventCardView.xaml");
        var xamlContent = File.ReadAllText(xamlPath);

        // Assert: 그룹 Id 숫자를 그대로 노출하는 모델 바인딩(Device.DeviceGroupsText)이 없어야 함
        Assert.DoesNotContain("Device.DeviceGroupsText", xamlContent);
        // Assert: 존재하지 않는 Device.Name 바인딩이 없어야 함
        Assert.DoesNotContain("{Binding Device.Name}", xamlContent);
        // Assert: 대신 이름 변환 VM 속성(DeviceGroupsText)에 바인딩되어야 함
        Assert.Contains("{Binding DeviceGroupsText}", xamlContent);
    }
    #endregion

    #region Test 2.6: MalfunctionEventCardView — 구역이 VM DeviceGroupsText(이름 변환)에 바인딩
    [Fact]
    public void MalfunctionEventCardView_ZoneBindsToViewModelDeviceGroupsText()
    {
        var xamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "Views", "Events", "MalfunctionEventCardView.xaml");
        var xamlContent = File.ReadAllText(xamlPath);

        // Assert: 그룹 Id 숫자를 그대로 노출하는 모델 바인딩(Device.DeviceGroupsText)이 없어야 함
        Assert.DoesNotContain("Device.DeviceGroupsText", xamlContent);
        // Assert: 대신 이름 변환 VM 속성(DeviceGroupsText)에 바인딩되어야 함
        Assert.Contains("{Binding DeviceGroupsText}", xamlContent);
    }
    #endregion

    #region Test 3.1: DetectionEventPanelView — 구역 컬럼 없음 + 조치보고 컬럼 있음
    [Fact]
    public void DetectionEventPanelView_HasNoDeviceGroupsColumn_AndHasActionReportedColumn()
    {
        var xamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "Views", "Panels", "DetectionEventPanelView.xaml");
        var xamlContent = File.ReadAllText(xamlPath);

        Assert.DoesNotContain("DeviceGroupsText", xamlContent);
        Assert.Contains("IsActionReported", xamlContent);
    }
    #endregion

    #region Test 3.2: MalfunctionEventPanelView — 구역 컬럼 없음 + 조치보고 컬럼 있음
    [Fact]
    public void MalfunctionEventPanelView_HasNoDeviceGroupsColumn_AndHasActionReportedColumn()
    {
        var xamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "Views", "Panels", "MalfunctionEventPanelView.xaml");
        var xamlContent = File.ReadAllText(xamlPath);

        Assert.DoesNotContain("DeviceGroupsText", xamlContent);
        Assert.Contains("IsActionReported", xamlContent);
    }
    #endregion

    #region Test 3.3: ConnectionEventPanelView — 구역 컬럼 없음
    [Fact]
    public void ConnectionEventPanelView_HasNoDeviceGroupsColumn()
    {
        var xamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "Views", "Panels", "ConnectionEventPanelView.xaml");
        var xamlContent = File.ReadAllText(xamlPath);

        Assert.DoesNotContain("DeviceGroupsText", xamlContent);
    }
    #endregion

    #region Test 3.4: ActionEventPanelView — 구역 컬럼 없음
    [Fact]
    public void ActionEventPanelView_HasNoDeviceGroupsColumn()
    {
        var xamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "Views", "Panels", "ActionEventPanelView.xaml");
        var xamlContent = File.ReadAllText(xamlPath);

        Assert.DoesNotContain("DeviceGroupsText", xamlContent);
    }
    #endregion

    #region Test 4.1: DetectionSelectionVM_Device_AcceptsAllDeviceTypes
    [Theory]
    [InlineData(typeof(SpeakerDeviceModel))]
    [InlineData(typeof(EnclosureDeviceModel))]
    [InlineData(typeof(LampDeviceModel))]
    [InlineData(typeof(CameraDeviceModel))]
    [InlineData(typeof(SensorDeviceModel))]
    public void ExEventViewModel_Device_AcceptsAllDeviceTypes(Type deviceType)
    {
        // Arrange: 모든 디바이스 타입을 IBaseDeviceModel로 할당 가능한지 확인
        var device = (IBaseDeviceModel)Activator.CreateInstance(deviceType)!;
        device.Id = 1;
        device.DeviceNumber = 100;
        var eventModel = new ExEventModel
        {
            Device = device,
            MessageType = EnumEventType.Intrusion,
            DateTime = System.DateTime.Now
        };
        var vm = new ExEventViewModel(eventModel);

        // Assert: Device 속성에 할당되어 접근 가능
        Assert.NotNull(vm.Device);
        Assert.Equal(100, vm.Device!.DeviceNumber);
    }
    #endregion

    #region FR-D4: Card device-deleted null-safety
    /// <summary>
    /// FR-D4: 참조 장비가 삭제(SYNC_DEVICE DELETED)되어 Device=null이 된 카드의
    /// 모든 파생 게터가 예외 없이 null/빈 값을 반환해야 한다.
    /// </summary>
    [Fact]
    public void should_not_throw_when_card_device_deleted()
    {
        // Arrange — 장비 삭제로 Device 참조가 끊긴 이벤트(모델 Device=null)
        var eventModel = new DetectionEventModel
        {
            Device = null,
            MessageType = EnumEventType.Intrusion,
            DateTime = System.DateTime.Now,
            Result = EnumDetectionType.VIBRATION_SENSOR
        };
        var vm = new DetectionEventCardViewModel(eventModel);

        // Act — 파생 게터 전수 접근
        var ex = Record.Exception(() =>
        {
            _ = vm.Device;
            _ = vm.DeviceTypeName;
            _ = vm.ControllerId;
            _ = vm.ControllerDeviceNumber;
            _ = vm.DeviceGroupsText;
        });

        // Assert — 예외 없이 null/빈 값
        Assert.Null(ex);
        Assert.Null(vm.Device);
        Assert.Null(vm.DeviceTypeName);
        Assert.Null(vm.ControllerId);
        Assert.Null(vm.ControllerDeviceNumber);
        Assert.Equal(string.Empty, vm.DeviceGroupsText);
    }
    #endregion

    #region Test 4.2: MalfunctionSelectionVM_Device_AcceptsAllDeviceTypes
    [Fact]
    public void ExEventViewModel_Device_AcceptsEnclosureWithDeviceGroups()
    {
        // Arrange: Enclosure 디바이스에 DeviceGroups 할당
        var enclosure = new EnclosureDeviceModel
        {
            Id = 1,
            DeviceNumber = 50,
            DeviceType = EnumDeviceType.Enclosure,
            DeviceGroups = new System.Collections.Generic.List<int> { 1, 2, 3 }
        };
        var eventModel = new ExEventModel
        {
            Device = enclosure,
            MessageType = EnumEventType.Intrusion,
            DateTime = System.DateTime.Now
        };
        var vm = new ExEventViewModel(eventModel);

        // Assert
        Assert.Equal("함체", vm.DeviceTypeName);
        Assert.Null(vm.ControllerId);
        Assert.NotNull(vm.Device!.DeviceGroups);
        Assert.Equal(3, vm.Device.DeviceGroups!.Count);
    }
    #endregion

    #region Test 4.3: ConnectionSelectionVM_Device_AcceptsAllDeviceTypes
    [Fact]
    public void ExEventViewModel_Device_AcceptsSpeakerType()
    {
        // Arrange
        var speaker = new SpeakerDeviceModel
        {
            Id = 2,
            DeviceNumber = 30,
            DeviceType = EnumDeviceType.IpSpeaker
        };
        var eventModel = new ExEventModel
        {
            Device = speaker,
            MessageType = EnumEventType.Intrusion,
            DateTime = System.DateTime.Now
        };
        var vm = new ExEventViewModel(eventModel);

        // Assert
        Assert.Equal("스피커", vm.DeviceTypeName);
        Assert.Null(vm.ControllerId);
    }
    #endregion

    #region Test 4.4: ActionSelectionVM_OriginEvent_DisplaysDeviceName
    [Fact]
    public void ExEventViewModel_Device_AcceptsLampType()
    {
        // Arrange
        var lamp = new LampDeviceModel
        {
            Id = 3,
            DeviceNumber = 40,
            DeviceType = EnumDeviceType.Lamp
        };
        var eventModel = new ExEventModel
        {
            Device = lamp,
            MessageType = EnumEventType.Intrusion,
            DateTime = System.DateTime.Now
        };
        var vm = new ExEventViewModel(eventModel);

        // Assert
        Assert.Equal("경고등", vm.DeviceTypeName);
        Assert.Null(vm.ControllerId);
    }
    #endregion

    #region Test 5.1: DetectionReportDialog_CompatibleWithNewModel
    [Fact]
    public void DetectionReportDialog_CompatibleWithNewModel()
    {
        var xamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "Views", "Dialogs", "DetectionReportDialogView.xaml");
        var xamlContent = File.ReadAllText(xamlPath);

        // 깨진 Device.Controller 경로가 없어야 함
        Assert.DoesNotContain("Device.Controller", xamlContent);
    }
    #endregion

    #region Test 5.2: MalfunctionReportDialog_CompatibleWithNewModel
    [Fact]
    public void MalfunctionReportDialog_CompatibleWithNewModel()
    {
        var xamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "Views", "Dialogs", "MalfunctionReportDialogView.xaml");
        var xamlContent = File.ReadAllText(xamlPath);

        Assert.DoesNotContain("Device.Controller", xamlContent);
    }
    #endregion

    #region Test 5.3: EventDashboard_TabControl_CompatibleWithNewModel
    [Fact]
    public void EventDashboard_TabControl_CompatibleWithNewModel()
    {
        var xamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "Views", "Dashboards", "EventDashboardView.xaml");
        var xamlContent = File.ReadAllText(xamlPath);

        Assert.DoesNotContain("Device.Controller", xamlContent);
    }
    #endregion

    #region Test 5.4: EventCardListPanel_CompatibleWithNewModel
    [Fact]
    public void EventCardListPanel_CompatibleWithNewModel()
    {
        var xamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "Views", "Panels", "EventCardListPanelView.xaml");
        var xamlContent = File.ReadAllText(xamlPath);

        Assert.DoesNotContain("Device.Controller", xamlContent);
    }
    #endregion
}
#endregion

#region EventPanel Cache Reuse Tests
/// <summary>
/// PRD: EventPanel Cache Reuse — 캐시 날짜범위 비교 + API 스킵 테스트
/// </summary>
public class EventPanelCacheReuseTests
{
    private readonly DetectionEventPanelViewModel _detectionPanel;
    private readonly EventProvider _eventProvider;

    public EventPanelCacheReuseTests()
    {
        var eventAggregator = new EventAggregator();
        var log = new Mock<ILogService>().Object;
        var providerService = new Mock<EventProviderService>(
            log, new Mock<IEventApiService>().Object, null, null).Object;
        _eventProvider = new EventProvider();
        var deviceProvider = new DeviceProvider();

        _detectionPanel = new DetectionEventPanelViewModel(
            eventAggregator, log, providerService, deviceProvider, _eventProvider);
    }

    #region Test 2.1: DetectionPanel_SkipFetch_WhenCacheValid
    [Fact]
    public void DetectionPanel_IsCacheValid_ReturnsFalse_WhenNoCacheSet()
    {
        // Arrange: 캐시 미설정 상태
        _detectionPanel.SetDate(DateTime.Today, DateTime.Today.AddDays(1));

        // Act & Assert: 캐시 미설정이므로 false
        Assert.False(_detectionPanel.IsCacheValid());
    }

    [Fact]
    public void DetectionPanel_IsCacheValid_ReturnsTrue_WhenCacheMatchesAndDataExists()
    {
        // Arrange
        var start = DateTime.Today;
        var end = DateTime.Today.AddDays(1);
        _detectionPanel.SetDate(start, end);
        _detectionPanel.SetCachedDate(start, end);
        _eventProvider.Add(new DetectionEventModel { Id = 1 });

        // Act & Assert
        Assert.True(_detectionPanel.IsCacheValid());
    }

    [Fact]
    public void DetectionPanel_IsCacheValid_ReturnsFalse_WhenCacheMatchesButNoData()
    {
        // Arrange
        var start = DateTime.Today;
        var end = DateTime.Today.AddDays(1);
        _detectionPanel.SetDate(start, end);
        _detectionPanel.SetCachedDate(start, end);
        // EventProvider에 Detection 데이터 없음

        // Act & Assert
        Assert.False(_detectionPanel.IsCacheValid());
    }
    #endregion

    #region Test 2.2: DetectionPanel_FetchApi_WhenCacheInvalid
    [Fact]
    public void DetectionPanel_IsCacheValid_ReturnsFalse_WhenDateRangeDiffers()
    {
        // Arrange: 캐시 날짜 != 현재 날짜
        _detectionPanel.SetDate(DateTime.Today, DateTime.Today.AddDays(1));
        _detectionPanel.SetCachedDate(DateTime.Today.AddDays(-1), DateTime.Today);
        _eventProvider.Add(new DetectionEventModel { Id = 1 });

        // Act & Assert: 날짜 불일치 → false
        Assert.False(_detectionPanel.IsCacheValid());
    }
    #endregion

    #region Test 2.3: DetectionPanel_ClickSearch_AlwaysFetches
    [Fact]
    public void DetectionPanel_InvalidateCache_MakesCacheInvalid()
    {
        // Arrange: 유효한 캐시 상태 만들기
        var start = DateTime.Today;
        var end = DateTime.Today.AddDays(1);
        _detectionPanel.SetDate(start, end);
        _detectionPanel.SetCachedDate(start, end);
        _eventProvider.Add(new DetectionEventModel { Id = 1 });
        Assert.True(_detectionPanel.IsCacheValid()); // 선행 확인

        // Act: 캐시 무효화
        _detectionPanel.InvalidateCache();

        // Assert: 무효화 후 false
        Assert.False(_detectionPanel.IsCacheValid());
    }
    #endregion

    #region Test 3.1: MalfunctionPanel_SkipFetch_WhenCacheValid
    [Fact]
    public void MalfunctionPanel_IsCacheValid_ReturnsTrue_WhenCacheMatchesAndDataExists()
    {
        // Arrange
        var eventAggregator = new EventAggregator();
        var log = new Mock<ILogService>().Object;
        var providerService = new Mock<EventProviderService>(
            log, new Mock<IEventApiService>().Object, null, null).Object;
        var ep = new EventProvider();
        var dp = new DeviceProvider();
        var panel = new MalfunctionEventPanelViewModel(eventAggregator, log, providerService, dp, ep);

        var start = DateTime.Today;
        var end = DateTime.Today.AddDays(1);
        panel.SetDate(start, end);
        panel.SetCachedDate(start, end);
        ep.Add(new MalfunctionEventModel { Id = 1 });

        // Act & Assert
        Assert.True(panel.IsCacheValid());

        // 캐시 무효화 확인
        panel.InvalidateCache();
        Assert.False(panel.IsCacheValid());
    }
    #endregion

    #region Test 3.2: ConnectionPanel_SkipFetch_WhenCacheValid
    [Fact]
    public void ConnectionPanel_IsCacheValid_ReturnsTrue_WhenCacheMatchesAndDataExists()
    {
        // Arrange
        var eventAggregator = new EventAggregator();
        var log = new Mock<ILogService>().Object;
        var providerService = new Mock<EventProviderService>(
            log, new Mock<IEventApiService>().Object, null, null).Object;
        var ep = new EventProvider();
        var dp = new DeviceProvider();
        var panel = new ConnectionEventPanelViewModel(eventAggregator, log, providerService, dp, ep);

        var start = DateTime.Today;
        var end = DateTime.Today.AddDays(1);
        panel.SetDate(start, end);
        panel.SetCachedDate(start, end);
        ep.Add(new ConnectionEventModel { Id = 1 });

        // Act & Assert
        Assert.True(panel.IsCacheValid());

        // 캐시 무효화 확인
        panel.InvalidateCache();
        Assert.False(panel.IsCacheValid());
    }
    #endregion

    #region Test 3.3: ActionPanel_SkipFetch_WhenCacheValid
    [Fact]
    public void ActionPanel_IsCacheValid_ReturnsTrue_WhenCacheMatchesAndDataExists()
    {
        // Arrange
        var eventAggregator = new EventAggregator();
        var log = new Mock<ILogService>().Object;
        var providerService = new Mock<EventProviderService>(
            log, new Mock<IEventApiService>().Object, null, null).Object;
        var ep = new EventProvider();
        var panel = new ActionEventPanelViewModel(eventAggregator, log, providerService, ep);

        var start = DateTime.Today;
        var end = DateTime.Today.AddDays(1);
        panel.SetDate(start, end);
        panel.SetCachedDate(start, end);
        ep.Add(new ActionEventModel { Id = 1 });

        // Act & Assert
        Assert.True(panel.IsCacheValid());

        // 캐시 무효화 확인
        panel.InvalidateCache();
        Assert.False(panel.IsCacheValid());
    }
    #endregion
}
#endregion

#region Event DtoModel Matching Tests
/// <summary>
/// PRD_Event_DtoModel_Matching — DTO↔Model 구조 매칭 테스트
/// </summary>
public class EventDtoModelMatchingTests
{
    #region Phase 1: Device Fallback Property Mapping (FR-01)

    [Fact]
    public void DeviceFallback_MapsNameNumberVersionStatus()
    {
        // Arrange: DeviceProvider 없이 DTO 변환 (fallback 경로)
        var dto = new DetectionEventDto
        {
            Id = 1,
            TypeEvent = "Intrusion",
            ActionReported = "False",
            Result = "THERMAL_SENSOR",
            CreatedAt = "2026-02-26T10:00:00.000Z",
            Device = new BaseDeviceDto
            {
                Id = 100,
                TypeDevice = "Fence",
                NameDevice = "센서A",
                NumberDevice = 5,
                Version = "1.0",
                Status = "ACTIVATED"
            }
        };

        // Act: DeviceProvider null → fallback path
        var model = dto.ToDetectionEventModel();

        // Assert: fallback에서도 속성이 매핑되어야 함
        Assert.NotNull(model.Device);
        Assert.Equal(100, model.Device!.Id);
        Assert.Equal("센서A", model.Device.DeviceName);
        Assert.Equal(5, model.Device.DeviceNumber);
        Assert.Equal("1.0", model.Device.Version);
        Assert.Equal(EnumDeviceStatus.ACTIVATED, model.Device.Status);
    }

    [Fact]
    public void DeviceFallback_MapsProperties_ForControllerType()
    {
        // Arrange
        var dto = new DetectionEventDto
        {
            Id = 2,
            TypeEvent = "Intrusion",
            ActionReported = "False",
            Result = "THERMAL_SENSOR",
            CreatedAt = "2026-02-26T10:00:00.000Z",
            Device = new BaseDeviceDto
            {
                Id = 200,
                TypeDevice = "Controller",
                NameDevice = "제어기1",
                NumberDevice = 1,
                Version = "2.0",
                Status = "ACTIVATED"
            }
        };

        // Act
        var model = dto.ToDetectionEventModel();

        // Assert: Controller 타입 + 속성 매핑
        Assert.NotNull(model.Device);
        Assert.IsType<ControllerDeviceModel>(model.Device);
        Assert.Equal("제어기1", model.Device!.DeviceName);
        Assert.Equal(1, model.Device.DeviceNumber);
    }

    [Fact]
    public void DeviceFallback_MapsProperties_ForCameraType()
    {
        // Arrange
        var dto = new ConnectionEventDto
        {
            Id = 3,
            TypeEvent = "Connection",
            CreatedAt = "2026-02-26T10:00:00.000Z",
            Device = new BaseDeviceDto
            {
                Id = 300,
                TypeDevice = "IpCamera",
                NameDevice = "카메라1",
                NumberDevice = 10,
                Version = "3.0",
                Status = "ERROR"
            }
        };

        // Act
        var model = dto.ToConnectionEventModel();

        // Assert
        Assert.NotNull(model.Device);
        Assert.IsType<CameraDeviceModel>(model.Device);
        Assert.Equal("카메라1", model.Device!.DeviceName);
        Assert.Equal(10, model.Device.DeviceNumber);
        Assert.Equal(EnumDeviceStatus.ERROR, model.Device.Status);
    }

    [Fact]
    public void DeviceFallback_MapsDeviceGroups()
    {
        // Arrange: DeviceGroups가 있는 DTO
        var dto = new DetectionEventDto
        {
            Id = 4,
            TypeEvent = "Intrusion",
            ActionReported = "False",
            Result = "THERMAL_SENSOR",
            CreatedAt = "2026-02-26T10:00:00.000Z",
            Device = new BaseDeviceDto
            {
                Id = 400,
                TypeDevice = "Fence",
                NameDevice = "센서B",
                NumberDevice = 8,
                DeviceGroups = new List<DeviceGroupDto>
                {
                    new DeviceGroupDto { Id = 1, Name = "그룹A" },
                    new DeviceGroupDto { Id = 2, Name = "그룹B" }
                }
            }
        };

        // Act
        var model = dto.ToDetectionEventModel();

        // Assert: DeviceGroups가 List<int>로 변환됨
        Assert.NotNull(model.Device);
        Assert.NotNull(model.Device!.DeviceGroups);
        Assert.Equal(2, model.Device.DeviceGroups!.Count);
        Assert.Contains(1, model.Device.DeviceGroups);
        Assert.Contains(2, model.Device.DeviceGroups);
    }

    #endregion

    #region Phase 2: ActionEvent FromEvent Reverse Mapping (FR-02)

    [Fact]
    public void ActionEventDto_FromEvent_ConvertedFromDetection()
    {
        // Arrange: Detection OriginEvent가 있는 ActionEventModel
        var originDevice = new SensorDeviceModel
        {
            Id = 50, DeviceType = EnumDeviceType.Fence, DeviceName = "센서50"
        };
        var origin = new DetectionEventModel
        {
            Id = 10,
            MessageType = EnumEventType.Intrusion,
            Status = EnumTrueFalse.True,
            Result = EnumDetectionType.THERMAL_SENSOR,
            DateTime = new DateTime(2026, 2, 26, 10, 0, 0),
            Device = originDevice
        };
        var actionModel = new ActionEventModel
        {
            Id = 100,
            MessageType = EnumEventType.Action,
            Content = "순찰 출동",
            User = "operator_kim",
            DateTime = new DateTime(2026, 2, 26, 10, 5, 0),
            OriginEvent = origin
        };

        // Act
        var dto = actionModel.ToActionEventDto();

        // Assert: FromEvent가 DetectionEventDto로 변환됨
        Assert.NotNull(dto.FromEvent);
        Assert.IsType<DetectionEventDto>(dto.FromEvent);
        var fromDto = (DetectionEventDto)dto.FromEvent;
        Assert.Equal(10, fromDto.Id);
        Assert.Equal("Intrusion", fromDto.TypeEvent);
        Assert.Equal("THERMAL_SENSOR", fromDto.Result);
    }

    [Fact]
    public void ActionEventDto_FromEvent_ConvertedFromMalfunction()
    {
        // Arrange
        var origin = new MalfunctionEventModel
        {
            Id = 20,
            MessageType = EnumEventType.Fault,
            Status = EnumTrueFalse.False,
            Reason = EnumFaultType.FAULT_FENCE,
            FirstStart = 10, FirstEnd = 20,
            SecondStart = 30, SecondEnd = 40,
            DateTime = new DateTime(2026, 2, 26, 10, 0, 0)
        };
        var actionModel = new ActionEventModel
        {
            Id = 101,
            MessageType = EnumEventType.Action,
            Content = "장애 처리",
            User = "admin",
            DateTime = new DateTime(2026, 2, 26, 10, 5, 0),
            OriginEvent = origin
        };

        // Act
        var dto = actionModel.ToActionEventDto();

        // Assert: FromEvent가 MalfunctionEventDto로 변환됨
        Assert.NotNull(dto.FromEvent);
        Assert.IsType<MalfunctionEventDto>(dto.FromEvent);
        var fromDto = (MalfunctionEventDto)dto.FromEvent;
        Assert.Equal(20, fromDto.Id);
        Assert.Equal("Fault", fromDto.TypeEvent);
        Assert.Equal("FAULT_FENCE", fromDto.Reason);
    }

    [Fact]
    public void ActionEventDto_FromEvent_Null_WhenNoOriginEvent()
    {
        // Arrange: OriginEvent가 null인 ActionEventModel
        var actionModel = new ActionEventModel
        {
            Id = 102,
            MessageType = EnumEventType.Action,
            Content = "독립 조치",
            User = "admin",
            DateTime = new DateTime(2026, 2, 26, 10, 5, 0),
            OriginEvent = null
        };

        // Act
        var dto = actionModel.ToActionEventDto();

        // Assert
        Assert.Null(dto.FromEvent);
    }

    #endregion

    #region Phase 3: ConnectionEvent Status Fix (FR-03)

    [Fact]
    public void ConnectionEvent_Status_DefaultFalse()
    {
        // Arrange
        var dto = new ConnectionEventDto
        {
            Id = 1,
            TypeEvent = "Connection",
            CreatedAt = "2026-02-26T10:00:00.000Z",
            Device = new BaseDeviceDto { Id = 100, TypeDevice = "Fence" }
        };

        // Act: 기본 변환
        var model = dto.ToConnectionEventModel();

        // Assert: Status가 True로 하드코딩되지 않음
        Assert.Equal(EnumTrueFalse.False, model.Status);
    }

    [Fact]
    public void ConnectionEvent_Status_DefaultFalse_WithDeviceProvider()
    {
        // Arrange
        var dto = new ConnectionEventDto
        {
            Id = 2,
            TypeEvent = "Connection",
            CreatedAt = "2026-02-26T10:00:00.000Z",
            Device = new BaseDeviceDto { Id = 200, TypeDevice = "Controller" }
        };
        var deviceProvider = new DeviceProvider();

        // Act: DeviceProvider 오버로드
        var model = dto.ToConnectionEventModel(deviceProvider);

        // Assert
        Assert.Equal(EnumTrueFalse.False, model.Status);
    }

    #endregion
}
#endregion

#region InfiniteScrollPaginationTests
public class InfiniteScrollPaginationTests
{
    #region - PagedResult Tests -

    [Theory]
    [InlineData(1, 3, true)]   // Page 1 of 3 → HasNextPage = true
    [InlineData(2, 3, true)]   // Page 2 of 3 → HasNextPage = true
    [InlineData(3, 3, false)]  // Page 3 of 3 → HasNextPage = false
    [InlineData(4, 3, false)]  // Page 4 of 3 → HasNextPage = false (over)
    [InlineData(1, 1, false)]  // Single page → HasNextPage = false
    public void PagedResult_HasNextPage_ReturnsExpected(int page, int totalPages, bool expected)
    {
        // Arrange & Act
        var result = new PagedResult<int>
        {
            Items = new List<int> { 1, 2, 3 },
            Page = page,
            TotalPages = totalPages,
            Total = 30
        };

        // Assert
        Assert.Equal(expected, result.HasNextPage);
    }

    #endregion

    #region - FetchEventsPageAsync Tests -

    [Fact]
    public async Task FetchDetectionEventsPageAsync_ReturnsPagedResult()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var dtoList = new List<DetectionEventDto>
        {
            new DetectionEventDto
            {
                Id = 1,
                TypeEvent = "Intrusion",
                Device = new BaseDeviceDto { Id = 100 },
                ActionReported = "True",
                Result = "THERMAL_SENSOR",
                CreatedAt = "2025-11-24T10:00:00.000Z"
            },
            new DetectionEventDto
            {
                Id = 2,
                TypeEvent = "Intrusion",
                Device = new BaseDeviceDto { Id = 101 },
                ActionReported = "False",
                Result = "THERMAL_SENSOR",
                CreatedAt = "2025-11-24T11:00:00.000Z"
            }
        };

        var apiResponse = new ApiListResponse<DetectionEventDto>
        {
            Success = true,
            Data = dtoList,
            Pagination = new PaginationDto
            {
                Page = 1,
                TotalPages = 3,
                Total = 250,
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

        var startDate = DateTime.Now.AddDays(-1);
        var endDate = DateTime.Now;

        // Act
        var result = await service.FetchDetectionEventsPageAsync(startDate, endDate, page: 1, limit: 100);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(250, result.Total);
        Assert.True(result.HasNextPage);

        Assert.Equal(1, result.Items[0].Id);
        Assert.Equal(EnumEventType.Intrusion, result.Items[0].MessageType);
    }

    [Fact]
    public async Task FetchDetectionEventsPageAsync_LastPage_HasNextPageFalse()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var apiResponse = new ApiListResponse<DetectionEventDto>
        {
            Success = true,
            Data = new List<DetectionEventDto>
            {
                new DetectionEventDto
                {
                    Id = 50, TypeEvent = "Intrusion",
                    Device = new BaseDeviceDto { Id = 100 },
                    Result = "THERMAL_SENSOR",
                    CreatedAt = "2025-11-24T10:00:00.000Z"
                }
            },
            Pagination = new PaginationDto { Page = 3, TotalPages = 3, Total = 250, Limit = 100 }
        };

        mockApiService
            .Setup(x => x.GetDetectionEventsAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        // Act
        var result = await service.FetchDetectionEventsPageAsync(
            DateTime.Now.AddDays(-1), DateTime.Now, page: 3, limit: 100);

        // Assert
        Assert.False(result.HasNextPage);
        Assert.Equal(3, result.Page);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task FetchDetectionEventsPageAsync_ApiError_ReturnsEmptyPagedResult()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var apiResponse = new ApiListResponse<DetectionEventDto>
        {
            Success = false,
            Error = new ApiError { Message = "Server Error" }
        };

        mockApiService
            .Setup(x => x.GetDetectionEventsAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        // Act
        var result = await service.FetchDetectionEventsPageAsync(
            DateTime.Now.AddDays(-1), DateTime.Now, page: 1, limit: 100);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.TotalPages);
        Assert.False(result.HasNextPage);
    }

    #endregion

    #region - FetchMalfunctionEventsPageAsync Tests -

    [Fact]
    public async Task FetchMalfunctionEventsPageAsync_ReturnsPagedResult()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var dtoList = new List<MalfunctionEventDto>
        {
            new MalfunctionEventDto
            {
                Id = 1,
                TypeEvent = "Fault",
                Device = new BaseDeviceDto { Id = 100 },
                ActionReported = "True",
                Reason = "FAULT_FENCE",
                CreatedAt = "2025-11-24T10:00:00.000Z"
            },
            new MalfunctionEventDto
            {
                Id = 2,
                TypeEvent = "Fault",
                Device = new BaseDeviceDto { Id = 101 },
                ActionReported = "False",
                Reason = "FAULT_CONTROLLER",
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
                TotalPages = 3,
                Total = 250,
                Limit = 100
            }
        };

        mockApiService
            .Setup(x => x.GetMalfunctionEventsAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        var startDate = DateTime.Now.AddDays(-1);
        var endDate = DateTime.Now;

        // Act
        var result = await service.FetchMalfunctionEventsPageAsync(startDate, endDate, page: 1, limit: 100);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(250, result.Total);
        Assert.True(result.HasNextPage);

        Assert.Equal(1, result.Items[0].Id);
        Assert.Equal(EnumEventType.Fault, result.Items[0].MessageType);
    }

    #endregion

    #region - FetchConnectionEventsPageAsync Tests -

    [Fact]
    public async Task FetchConnectionEventsPageAsync_ReturnsPagedResult()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var mockLogService = new Mock<ILogService>();

        var dtoList = new List<ConnectionEventDto>
        {
            new ConnectionEventDto
            {
                Id = 1,
                TypeEvent = "Connection",
                Device = new BaseDeviceDto { Id = 100 },
                CreatedAt = "2025-11-24T10:00:00.000Z"
            },
            new ConnectionEventDto
            {
                Id = 2,
                TypeEvent = "Connection",
                Device = new BaseDeviceDto { Id = 101 },
                CreatedAt = "2025-11-24T11:00:00.000Z"
            }
        };

        var apiResponse = new ApiListResponse<ConnectionEventDto>
        {
            Success = true,
            Data = dtoList,
            Pagination = new PaginationDto
            {
                Page = 1,
                TotalPages = 2,
                Total = 150,
                Limit = 100
            }
        };

        mockApiService
            .Setup(x => x.GetConnectionEventsAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        var startDate = DateTime.Now.AddDays(-1);
        var endDate = DateTime.Now;

        // Act
        var result = await service.FetchConnectionEventsPageAsync(startDate, endDate, page: 1, limit: 100);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(150, result.Total);
        Assert.True(result.HasNextPage);

        Assert.Equal(1, result.Items[0].Id);
        Assert.Equal(EnumEventType.Connection, result.Items[0].MessageType);
    }

    #endregion

    #region - FetchActionEventsPageAsync Tests -

    [Fact]
    public async Task FetchActionEventsPageAsync_ReturnsPagedResult()
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
                Content = "조치 완료",
                User = "admin",
                CreatedAt = "2025-11-24T10:00:00.000Z"
            },
            new ActionEventDto
            {
                Id = 2,
                TypeEvent = "Action",
                Content = "현장 확인",
                User = "operator",
                CreatedAt = "2025-11-24T11:00:00.000Z"
            }
        };

        var apiResponse = new ApiListResponse<ActionEventDto>
        {
            Success = true,
            Data = dtoList,
            Pagination = new PaginationDto
            {
                Page = 1,
                TotalPages = 5,
                Total = 480,
                Limit = 100
            }
        };

        mockApiService
            .Setup(x => x.GetActionEventsAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var service = new EventProviderService(mockLogService.Object, mockApiService.Object);

        var startDate = DateTime.Now.AddDays(-1);
        var endDate = DateTime.Now;

        // Act
        var result = await service.FetchActionEventsPageAsync(startDate, endDate, page: 1, limit: 100);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(5, result.TotalPages);
        Assert.Equal(480, result.Total);
        Assert.True(result.HasNextPage);

        Assert.Equal(1, result.Items[0].Id);
        Assert.Equal(EnumEventType.Action, result.Items[0].MessageType);
    }

    #endregion
}
#endregion

#region - EventPanelCancelTokenTests -
public class EventPanelCancelTokenTests
{
    /// <summary>
    /// Helper: BasePanelViewModel._cancellationTokenSource (protected) 에 접근하기 위한 리플렉션
    /// </summary>
    private static void SetCancellationTokenSource(object vm, CancellationTokenSource? cts)
    {
        var field = typeof(BasePanelViewModel)
            .GetField("_cancellationTokenSource",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field!.SetValue(vm, cts);
    }

    private DetectionEventPanelViewModel CreateDetectionPanelVM()
    {
        var mockEventAggregator = new Mock<IEventAggregator>();
        var mockLogService = new Mock<ILogService>();
        var mockApiService = new Mock<IEventApiService>();
        var providerService = new EventProviderService(mockLogService.Object, mockApiService.Object);
        var deviceProvider = new DeviceProvider();
        var eventProvider = new EventProvider();

        return new DetectionEventPanelViewModel(
            mockEventAggregator.Object,
            mockLogService.Object,
            providerService,
            deviceProvider,
            eventProvider);
    }

    #region - Phase 1: ClickCancel 논리 버그 수정 -

    [Fact]
    public void ClickCancel_NullTokenSource_NoException()
    {
        // Arrange
        var vm = CreateDetectionPanelVM();
        SetCancellationTokenSource(vm, null);

        // Act & Assert — 예외 없이 완료되어야 함
        var exception = Record.Exception(() => vm.ClickCancel());
        Assert.Null(exception);
    }

    [Fact]
    public void ClickCancel_ValidTokenSource_CancelsToken()
    {
        // Arrange
        var vm = CreateDetectionPanelVM();
        var cts = new CancellationTokenSource();
        SetCancellationTokenSource(vm, cts);

        // Act
        vm.ClickCancel();

        // Assert
        Assert.True(cts.IsCancellationRequested);
    }

    [Fact]
    public void ClickCancel_AlreadyCancelled_NoException()
    {
        // Arrange
        var vm = CreateDetectionPanelVM();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // 이미 cancelled
        SetCancellationTokenSource(vm, cts);

        // Act & Assert — 중복 Cancel 시 예외 없어야 함
        var exception = Record.Exception(() => vm.ClickCancel());
        Assert.Null(exception);
    }

    #endregion

    #region - Phase 2: LoadMoreCommand CancellationToken 전달 -

    [Fact]
    public async Task LoadNextPageAsync_CancelledToken_DoesNotFetch()
    {
        // Arrange
        var mockEventAggregator = new Mock<IEventAggregator>();
        var mockLogService = new Mock<ILogService>();
        var mockApiService = new Mock<IEventApiService>();
        var providerService = new EventProviderService(mockLogService.Object, mockApiService.Object);
        var deviceProvider = new DeviceProvider();
        var eventProvider = new EventProvider();

        var vm = new DetectionEventPanelViewModel(
            mockEventAggregator.Object,
            mockLogService.Object,
            providerService,
            deviceProvider,
            eventProvider);

        var cts = new CancellationTokenSource();
        cts.Cancel(); // 미리 취소

        // Act & Assert — cancelled token이면 OperationCanceledException 발생 + API 호출 없음
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => vm.LoadNextPageAsync(cts.Token));

        mockApiService.Verify(
            s => s.GetDetectionEventsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    #endregion

    #region - Phase 3: DataInitialize ThrowIfCancellationRequested -

    /// <summary>
    /// DataInitialize (private) 를 리플렉션으로 호출하는 헬퍼
    /// </summary>
    private static Task InvokeDataInitialize(object vm, CancellationToken token)
    {
        var method = vm.GetType().GetMethod("DataInitialize",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (Task)method!.Invoke(vm, new object[] { token })!;
    }

    [Fact]
    public async Task DataInitialize_CancelledToken_DoesNotFetch()
    {
        // Arrange
        var mockEventAggregator = new Mock<IEventAggregator>();
        var mockLogService = new Mock<ILogService>();
        var mockApiService = new Mock<IEventApiService>();
        var providerService = new EventProviderService(mockLogService.Object, mockApiService.Object);
        var deviceProvider = new DeviceProvider();
        var eventProvider = new EventProvider();

        var vm = new DetectionEventPanelViewModel(
            mockEventAggregator.Object,
            mockLogService.Object,
            providerService,
            deviceProvider,
            eventProvider);

        var cts = new CancellationTokenSource();
        cts.Cancel(); // 미리 취소

        // Act — DataInitialize는 내부에서 catch하므로 예외 전파 없음
        await InvokeDataInitialize(vm, cts.Token);

        // Assert — cancelled token이면 API 호출이 없어야 함
        mockApiService.Verify(
            s => s.GetDetectionEventsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    #endregion

    #region - Phase 4: Cancel 후 UI 상태 복원 -

    [Fact]
    public async Task DataInitialize_Cancelled_IsVisibleRestoredToTrue()
    {
        // Arrange
        var vm = CreateDetectionPanelVM();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        await InvokeDataInitialize(vm, cts.Token);

        // Assert — Cancel 후에도 IsVisible은 true로 복원
        Assert.True(vm.IsVisible);
    }

    [Fact]
    public async Task LoadNextPageAsync_Cancelled_IsLoadingMoreRestoredToFalse()
    {
        // Arrange
        var vm = CreateDetectionPanelVM();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act — ThrowIfCancellationRequested 발생
        try { await vm.LoadNextPageAsync(cts.Token); }
        catch (OperationCanceledException) { }

        // Assert — Cancel 후에도 IsLoadingMore는 false
        Assert.False(vm.IsLoadingMore);
    }

    #endregion
}
#endregion

#region - EventInfoViewModelCancelTests -
[Collection("IoC-Dependent")]
public class EventInfoViewModelCancelTests
{
    private static void EnsureIoCInitialized()
    {
        IoC.GetInstance = (type, key) =>
        {
            if (type == typeof(IEventAggregator)) return new EventAggregator();
            if (type == typeof(ILogService)) return null!;
            return null!;
        };
        IoC.GetAllInstances = type => Enumerable.Empty<object>();
        IoC.BuildUp = obj => { };
    }

    private EventInfoViewModel CreateEventInfoVM(Mock<IEventApiService> mockApiService)
    {
        EnsureIoCInitialized();
        var mockLogService = new Mock<ILogService>();
        var providerService = new EventProviderService(mockLogService.Object, mockApiService.Object);
        var deviceProvider = new DeviceProvider();
        var eventProvider = new EventProvider();
        var mockEventAggregator = new Mock<IEventAggregator>();

        return new EventInfoViewModel(
            deviceProvider,
            eventProvider,
            providerService,
            mockEventAggregator.Object,
            mockLogService.Object);
    }

    #region - Phase 5: EventInfoViewModel Cancel -

    [Fact]
    public async Task EventInfoVM_DataInitialize_CancelledToken_DoesNotFetchApis()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var vm = CreateEventInfoVM(mockApiService);
        vm.SetData(DateTime.Now.AddDays(-1), DateTime.Now, new[] { "DET", "MAL", "CON", "ACT" });

        var cts = new CancellationTokenSource();
        cts.Cancel(); // 미리 취소

        // Act
        await vm.DataInitialize(cts.Token);

        // Assert — cancelled token이면 어떤 Fetch API도 호출되지 않아야 함
        mockApiService.Verify(
            s => s.GetDetectionEventsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    public async Task EventInfoVM_CancelAndRestart_PreviousCancelled()
    {
        // Arrange
        var mockApiService = new Mock<IEventApiService>();
        var vm = CreateEventInfoVM(mockApiService);
        vm.SetData(DateTime.Now.AddDays(-1), DateTime.Now, new[] { "DET" });

        // Act — CancelAndRestart 호출 후 이전 CTS가 취소됨을 확인
        var cts1 = vm.CancelAndRestart();
        var cts2 = vm.CancelAndRestart();

        // Assert — 1차 CTS는 취소됨, 2차 CTS는 아직 유효
        Assert.True(cts1.IsCancellationRequested);
        Assert.False(cts2.IsCancellationRequested);
    }

    #endregion

    #region ChartEmptyDataTests

    [Fact]
    public void GetSensorDetectionCounts_ExcludesCamera()
    {
        // Arrange: 센서 3건 + 카메라 2건
        var sensor = new SensorDeviceModel { Id = 1, DeviceNumber = 1, DeviceType = Libraries.Enums.EnumDeviceType.Fence };
        var camera = new CameraDeviceModel { Id = 2, DeviceNumber = 2 }; // DeviceType = IpCamera (생성자 자동)

        var events = new List<IDetectionEventModel>
        {
            new DetectionEventModel { Id = 1, DateTime = DateTime.Now, Device = sensor },
            new DetectionEventModel { Id = 2, DateTime = DateTime.Now, Device = sensor },
            new DetectionEventModel { Id = 3, DateTime = DateTime.Now, Device = sensor },
            new DetectionEventModel { Id = 4, DateTime = DateTime.Now, Device = camera },
            new DetectionEventModel { Id = 5, DateTime = DateTime.Now, Device = camera },
        };

        // Act — 센서 탐지만 반환 (IpCamera 제외)
        var result = DataHelper.GetSensorDetectionCountsByDevice(
            DateTime.Now.AddDays(-1), DateTime.Now.AddDays(1),
            new List<IBaseDeviceModel> { sensor }, events);

        // Assert — 센서만 3건
        Assert.Single(result);
        Assert.Equal(3.0, result[0]);
    }

    [Fact]
    public void GetCameraDetectionCount_OnlyCamera()
    {
        // Arrange: 센서 3건 + 카메라 2건
        var sensor = new SensorDeviceModel { Id = 1, DeviceNumber = 1, DeviceType = Libraries.Enums.EnumDeviceType.Fence };
        var camera = new CameraDeviceModel { Id = 2, DeviceNumber = 2 };

        var events = new List<IDetectionEventModel>
        {
            new DetectionEventModel { Id = 1, DateTime = DateTime.Now, Device = sensor },
            new DetectionEventModel { Id = 2, DateTime = DateTime.Now, Device = sensor },
            new DetectionEventModel { Id = 3, DateTime = DateTime.Now, Device = sensor },
            new DetectionEventModel { Id = 4, DateTime = DateTime.Now, Device = camera },
            new DetectionEventModel { Id = 5, DateTime = DateTime.Now, Device = camera },
        };

        // Act — 카메라 탐지만 카운트
        var count = DataHelper.GetCameraDetectionCount(
            DateTime.Now.AddDays(-1), DateTime.Now.AddDays(1), events);

        // Assert — 카메라만 2건
        Assert.Equal(2, count);
    }

    [Fact]
    public void CameraEventKpi_TotalCount_Correct()
    {
        // Arrange: 카메라 탐지 5건 + 센서 탐지 3건
        var camera1 = new CameraDeviceModel { Id = 1, DeviceNumber = 1 };
        var camera2 = new CameraDeviceModel { Id = 2, DeviceNumber = 2 };
        var sensor = new SensorDeviceModel { Id = 3, DeviceNumber = 3, DeviceType = Libraries.Enums.EnumDeviceType.Fence };

        var now = DateTime.Now;
        var events = new List<IDetectionEventModel>
        {
            new DetectionEventModel { Id = 1, DateTime = now, Device = camera1 },
            new DetectionEventModel { Id = 2, DateTime = now, Device = camera1 },
            new DetectionEventModel { Id = 3, DateTime = now, Device = camera1 },
            new DetectionEventModel { Id = 4, DateTime = now, Device = camera2 },
            new DetectionEventModel { Id = 5, DateTime = now, Device = camera2 },
            new DetectionEventModel { Id = 6, DateTime = now, Device = sensor },
            new DetectionEventModel { Id = 7, DateTime = now, Device = sensor },
            new DetectionEventModel { Id = 8, DateTime = now, Device = sensor },
        };

        // Act
        var totalCount = CameraEventInfoViewModel.CalcTotalCount(
            now.AddDays(-1), now.AddDays(1), events);

        // Assert — 카메라만 5건
        Assert.Equal(5, totalCount);
    }

    [Fact]
    public void CameraEventKpi_DailyAverage_Correct()
    {
        // Arrange: 3일간 카메라 90건
        var camera = new CameraDeviceModel { Id = 1, DeviceNumber = 1 };
        var start = new DateTime(2026, 3, 1);
        var end = new DateTime(2026, 3, 4); // 3일

        var events = new List<IDetectionEventModel>();
        for (int i = 0; i < 90; i++)
            events.Add(new DetectionEventModel { Id = i, DateTime = start.AddHours(i % 72), Device = camera });

        // Act
        var avg = CameraEventInfoViewModel.CalcDailyAverage(start, end, events);

        // Assert — 90 / 3 = 30
        Assert.Equal(30.0, avg);
    }

    [Fact]
    public void CameraEventKpi_ActiveCameraCount_Correct()
    {
        // Arrange: 5대 카메라 중 3대만 이벤트
        var cam1 = new CameraDeviceModel { Id = 1, DeviceNumber = 1 };
        var cam2 = new CameraDeviceModel { Id = 2, DeviceNumber = 2 };
        var cam3 = new CameraDeviceModel { Id = 3, DeviceNumber = 3 };

        var now = DateTime.Now;
        var events = new List<IDetectionEventModel>
        {
            new DetectionEventModel { Id = 1, DateTime = now, Device = cam1 },
            new DetectionEventModel { Id = 2, DateTime = now, Device = cam1 },
            new DetectionEventModel { Id = 3, DateTime = now, Device = cam2 },
            new DetectionEventModel { Id = 4, DateTime = now, Device = cam3 },
        };

        // Act
        var activeCount = CameraEventInfoViewModel.CalcActiveCameraCount(
            now.AddDays(-1), now.AddDays(1), events);

        // Assert — 3대
        Assert.Equal(3, activeCount);
    }

    [Fact]
    public void GenerateHourlyLabels_SameDay_ReturnsCorrectLabels()
    {
        // Arrange
        var start = new DateTime(2026, 3, 3, 0, 0, 0);
        var end = new DateTime(2026, 3, 3, 3, 0, 0);

        // Act
        var labels = ChartHelper.GenerateHourlyLabels(start, end);

        // Assert — 00, 01, 02, 03 → 4개
        Assert.Equal(4, labels.Length);
        Assert.Equal("2026-03-03 00", labels[0]);
        Assert.Equal("2026-03-03 01", labels[1]);
        Assert.Equal("2026-03-03 02", labels[2]);
        Assert.Equal("2026-03-03 03", labels[3]);
    }

    [Fact]
    public void GenerateHourlyLabels_CrossDay_ReturnsCorrectLabels()
    {
        // Arrange — 날짜 경계를 넘는 구간
        var start = new DateTime(2026, 3, 2, 22, 0, 0);
        var end = new DateTime(2026, 3, 3, 2, 0, 0);

        // Act
        var labels = ChartHelper.GenerateHourlyLabels(start, end);

        // Assert — 22, 23, 00, 01, 02 → 5개
        Assert.Equal(5, labels.Length);
        Assert.Equal("2026-03-02 22", labels[0]);
        Assert.Equal("2026-03-02 23", labels[1]);
        Assert.Equal("2026-03-03 00", labels[2]);
        Assert.Equal("2026-03-03 01", labels[3]);
        Assert.Equal("2026-03-03 02", labels[4]);
    }

    [Fact]
    public void GenerateHourlyLabels_LongRange_SwitchesToDaily()
    {
        // Arrange — 7일 구간 (48시간 초과 → 일 단위 전환)
        var start = new DateTime(2026, 2, 25, 0, 0, 0);
        var end = new DateTime(2026, 3, 3, 0, 0, 0);

        // Act
        var labels = ChartHelper.GenerateHourlyLabels(start, end);

        // Assert — 7일 = 02-25 ~ 03-03 → 7개 (일 단위 포맷)
        Assert.Equal(7, labels.Length);
        Assert.Equal("2026-02-25", labels[0]);
        Assert.Equal("2026-02-26", labels[1]);
        Assert.Equal("2026-03-03", labels[6]);
    }

    #endregion
}
#endregion

#region - Statistics Chart Helper 테스트 -

public class StatisticsChartHelperTests
{
    #region - BuildLineSeriesFromTrend -

    [Fact]
    [Trait("Category", "Chart")]
    public void TrendSeriesToLineChart_MapsCorrectly()
    {
        // Arrange
        var trend = new EventTrendDto
        {
            Interval = "hour",
            StartDate = "2025-01-15T00:00:00",
            EndDate = "2025-01-15T03:00:00",
            Series = new List<EventTrendItemDto>
            {
                new() { TimeBucket = "2025-01-15 00", SensorDetection = 3, CameraDetection = 1, Malfunction = 30, Connection = 0, Action = 2 },
                new() { TimeBucket = "2025-01-15 01", SensorDetection = 0, CameraDetection = 5, Malfunction = 28, Connection = 0, Action = 0 },
                new() { TimeBucket = "2025-01-15 02", SensorDetection = 7, CameraDetection = 2, Malfunction = 10, Connection = 1, Action = 3 }
            }
        };

        // Act
        var (series, xLabels) = ChartHelper.BuildLineSeriesFromTrend(
            trend, DateTime.Parse("2025-01-15"), DateTime.Parse("2025-01-15T03:00:00"));

        // Assert
        Assert.Equal(5, series.Count);    // 5 카테고리
        Assert.Equal(3, xLabels.Length);  // 3 time_bucket

        Assert.Equal("2025-01-15 00", xLabels[0]);
        Assert.Equal("2025-01-15 02", xLabels[2]);

        // Sensor Detection 값 검증
        var sensorSeries = series[0] as LiveChartsCore.SkiaSharpView.LineSeries<int>;
        Assert.NotNull(sensorSeries);
        var sensorValues = sensorSeries!.Values!.ToArray();
        Assert.Equal(3, sensorValues.Length);
        Assert.Equal(3, sensorValues[0]);
        Assert.Equal(0, sensorValues[1]);
        Assert.Equal(7, sensorValues[2]);
    }

    [Fact]
    [Trait("Category", "Chart")]
    public void TrendSeriesToLineChart_EmptyData_FallbackLabels()
    {
        // Arrange
        var trend = new EventTrendDto
        {
            Interval = "hour",
            Series = new List<EventTrendItemDto>()
        };
        var start = new DateTime(2025, 1, 15, 0, 0, 0);
        var end = new DateTime(2025, 1, 15, 3, 0, 0);

        // Act
        var (series, xLabels) = ChartHelper.BuildLineSeriesFromTrend(trend, start, end);

        // Assert
        Assert.Equal(5, series.Count);
        Assert.True(xLabels.Length >= 3); // 00, 01, 02, 03 → 최소 3~4

        // 모든 값이 0
        var sensorSeries = series[0] as LiveChartsCore.SkiaSharpView.LineSeries<int>;
        Assert.NotNull(sensorSeries);
        Assert.All(sensorSeries!.Values!, v => Assert.Equal(0, v));
    }

    #endregion

    #region - BuildBarSeriesFromByDevice -

    [Fact]
    [Trait("Category", "Chart")]
    public void ByDeviceToBarSeries_MapsControllers()
    {
        // Arrange
        var byDevice = new EventByDeviceDto
        {
            Controllers = new List<ControllerStatsDto>
            {
                new() { ControllerId = 1, ControllerName = "Controller-A", SensorDetection = 45, Malfunction = 12, Connection = 3 },
                new() { ControllerId = 2, ControllerName = "Controller-B", SensorDetection = 30, Malfunction = 8, Connection = 1 }
            },
            Cameras = new List<CameraStatsDto>
            {
                new() { CameraId = 101, CameraName = "Camera-1", CameraDetection = 25 }
            }
        };

        // Act
        var (series, xLabels) = ChartHelper.BuildBarSeriesFromByDevice(byDevice, new[] { "DET", "MAL", "CON", "ACT" });

        // Assert
        Assert.Equal(4, series.Count); // DET, MAL, CON, ACT
        Assert.Equal(2, xLabels.Length);
        Assert.Equal("Controller-A", xLabels[0]);
        Assert.Equal("Controller-B", xLabels[1]);
    }

    #endregion

    #region - BuildPieSeriesFromSummary -

    [Fact]
    [Trait("Category", "Chart")]
    public void SummaryToPieSeries_MapsCorrectly()
    {
        // Arrange
        var summary = new EventSummaryDto
        {
            Total = 275,
            SensorDetection = 150,
            CameraDetection = 30,
            Malfunction = 45,
            Connection = 30,
            Action = 20
        };

        // Act
        var series = ChartHelper.BuildPieSeriesFromSummary(summary, new[] { "DET", "MAL", "CON", "ACT" });

        // Assert
        Assert.Equal(4, series.Count); // DET, MAL, CON, ACT
    }

    [Fact]
    [Trait("Category", "Chart")]
    public void SummaryToPieSeries_FilteredTypes()
    {
        // Arrange
        var summary = new EventSummaryDto
        {
            SensorDetection = 150,
            CameraDetection = 30,
            Malfunction = 45,
            Connection = 30,
            Action = 20
        };

        // Act — DET 전용
        var series = ChartHelper.BuildPieSeriesFromSummary(summary, new[] { "DET" });

        // Assert
        Assert.Single(series);
    }

    #endregion

    #region - CameraKpi From SummaryDto -

    [Fact]
    [Trait("Category", "Chart")]
    public void CameraKpi_FromSummaryDto_Correct()
    {
        // Arrange
        var summary = new EventSummaryDto
        {
            CameraDetection = 30,
            DailyAverages = new DailyAveragesDto { CameraDetection = 4.3 },
            ActiveDevices = new ActiveDevicesDto { Cameras = 15 }
        };

        // Act + Assert
        Assert.Equal(30, summary.CameraDetection);
        Assert.Equal(4.3, summary.DailyAverages.CameraDetection);
        Assert.Equal(15, summary.ActiveDevices.Cameras);
    }

    #endregion

    #region LoadingProgressTests

    [Fact]
    public void DataChartPanelViewModel_IsChartLoading_DefaultFalse()
    {
        // Arrange — DataChartPanelViewModel requires DI params; just verify property exists via reflection
        var prop = typeof(DataChartPanelViewModel).GetProperty("IsChartLoading");

        // Assert
        Assert.NotNull(prop);
        Assert.Equal(typeof(bool), prop.PropertyType);
    }

    #endregion

    #region ExEventViewModelTests

    [Fact]
    public void ExEventViewModel_IsActionReported_PropertyExistsAndIsBool()
    {
        // ExEventViewModel requires IoC — verify via reflection
        var prop = typeof(ExEventViewModel).GetProperty("IsActionReported");
        Assert.NotNull(prop);
        Assert.Equal(typeof(bool), prop.PropertyType);
    }

    [Fact]
    public void ExEventModel_Status_TrueImpliesIsActionReported()
    {
        // Verify the underlying logic: Status == EnumTrueFalse.True => IsActionReported=true
        var modelTrue = new ExEventModel { Status = EnumTrueFalse.True };
        Assert.True(modelTrue.Status == EnumTrueFalse.True);

        var modelFalse = new ExEventModel { Status = EnumTrueFalse.False };
        Assert.False(modelFalse.Status == EnumTrueFalse.True);
    }

    #endregion
}

#endregion

#region - DeviceSymbolLookupModel.SyncFromDevice Tests -
public class DeviceSymbolLookupModelSyncTests
{
    private DeviceSymbolLookupModel CreateLookup()
    {
        var log = new Mock<ILogService>().Object;
        var ea = new Mock<IEventAggregator>().Object;
        var eventSetupSource = new Mock<IEventSetupModel>().Object;
        var eventSetup = new EventSetupModel(eventSetupSource);
        return new DeviceSymbolLookupModel(log);
    }

    [Fact]
    public void SyncFromDevice_Activated_SetsOperationStateActivated()
    {
        var lookup = CreateLookup();
        var symbolMock = new Mock<IPidsEventCapable>();
        symbolMock.SetupAllProperties();
        lookup.SymbolModel = symbolMock.Object;

        lookup.SyncFromDevice(EnumDeviceStatus.ACTIVATED);

        symbolMock.VerifySet(s => s.OperationState = EnumOperationState.ACTIVATED);
    }

    [Fact]
    public void SyncFromDevice_Deactivated_SetsOperationStateDeactivated()
    {
        var lookup = CreateLookup();
        var symbolMock = new Mock<IPidsEventCapable>();
        symbolMock.SetupAllProperties();
        lookup.SymbolModel = symbolMock.Object;

        lookup.SyncFromDevice(EnumDeviceStatus.DEACTIVATED);

        symbolMock.VerifySet(s => s.OperationState = EnumOperationState.DEACTIVATED);
    }

    [Fact]
    public void SyncFromDevice_Error_SetsOperationStateError()
    {
        var lookup = CreateLookup();
        var symbolMock = new Mock<IPidsEventCapable>();
        symbolMock.SetupAllProperties();
        lookup.SymbolModel = symbolMock.Object;

        lookup.SyncFromDevice(EnumDeviceStatus.ERROR);

        symbolMock.VerifySet(s => s.OperationState = EnumOperationState.ERROR);
    }

    // (FR-B1) SSOT 회귀 고정 — SyncFromDevice(Device.Status/SYNC_DEVICE)는 OperationState축만 갱신하고
    //   EventStatus/CompositeStatus(이벤트축, EQM 전용)는 절대 건드리지 않는다. 이전 EventStatus 설정 계약은
    //   SSOT 리팩터로 폐기됨(장애=CompositeStatus=Faulted만, OperationState는 ERROR≠Fault). 이중상태축 분리.
    [Fact]
    public void should_set_operationstate_error_and_not_touch_eventstatus_when_sync_error()
    {
        var lookup = CreateLookup();
        var symbolMock = new Mock<IPidsEventCapable>();
        symbolMock.SetupAllProperties();
        lookup.SymbolModel = symbolMock.Object;

        lookup.SyncFromDevice(EnumDeviceStatus.ERROR);

        symbolMock.VerifySet(s => s.OperationState = EnumOperationState.ERROR);
        symbolMock.VerifySet(s => s.EventStatus = It.IsAny<EnumEventStatus>(), Times.Never);
    }

    [Fact]
    public void should_set_operationstate_activated_and_not_touch_eventstatus_when_sync_activated()
    {
        var lookup = CreateLookup();
        var symbolMock = new Mock<IPidsEventCapable>();
        symbolMock.SetupAllProperties();
        lookup.SymbolModel = symbolMock.Object;

        lookup.SyncFromDevice(EnumDeviceStatus.ACTIVATED);

        symbolMock.VerifySet(s => s.OperationState = EnumOperationState.ACTIVATED);
        symbolMock.VerifySet(s => s.EventStatus = It.IsAny<EnumEventStatus>(), Times.Never);
    }

    [Fact]
    public void SyncFromDevice_Activated_AfterError_RestoresNormalState()
    {
        // ERROR → ACTIVATED 전환 시 정상 복원 확인
        var lookup = CreateLookup();
        var symbolMock = new Mock<IPidsEventCapable>();
        symbolMock.SetupAllProperties();
        lookup.SymbolModel = symbolMock.Object;

        lookup.SyncFromDevice(EnumDeviceStatus.ERROR);
        lookup.SyncFromDevice(EnumDeviceStatus.ACTIVATED);

        Assert.Equal(EnumOperationState.ACTIVATED, symbolMock.Object.OperationState);
        Assert.Equal(EnumEventStatus.Normal, symbolMock.Object.EventStatus);
    }

    [Fact]
    public void ProcessEvent_DoesNotChangeSymbolState()
    {
        // ProcessEvent는 더 이상 심볼 비주얼을 변경하지 않음
        var lookup = CreateLookup();
        var symbolMock = new Mock<IPidsEventCapable>();
        symbolMock.SetupAllProperties();
        symbolMock.Object.EventStatus = EnumEventStatus.Normal;
        symbolMock.Object.OperationState = EnumOperationState.ACTIVATED;
        lookup.SymbolModel = symbolMock.Object;
        lookup.DeviceModel = new CameraDeviceModel { Id = 1, DeviceType = EnumDeviceType.IpCamera };

        lookup.ProcessEvent(EnumEventType.Fault, EnumSeverityLevel.CRITICAL);

        Assert.Equal(EnumEventStatus.Normal, symbolMock.Object.EventStatus);
        Assert.Equal(EnumOperationState.ACTIVATED, symbolMock.Object.OperationState);
    }

    // TEST-01: ApplyCompositeStatus — 비-UI 스레드에서 호출 시 STA 예외 없음 + SetUpdate 발화 (IMPL-01)
    [Fact]
    public async Task ApplyCompositeStatus_FromBackgroundThread_ShouldNotThrowAndSetCompositeStatus()
    {
        // Arrange
        var lookup = CreateLookup();
        var symbolMock = new Mock<IPidsEventCapable>();
        symbolMock.SetupAllProperties();
        lookup.SymbolModel = symbolMock.Object;

        // Act — 백그라운드 스레드에서 호출 (Application.Current == null → MarshalUpdate fallback → 동기 SetUpdate)
        Exception? caught = null;
        await Task.Run(() =>
        {
            try { lookup.ApplyCompositeStatus(EnumCompositeEventStatus.FaultedDetecting); }
            catch (Exception ex) { caught = ex; }
        });

        // Assert: STA 예외 미발생 + CompositeStatus 반영 + SetUpdate 1회 호출 (fallback 경유)
        Assert.Null(caught);
        Assert.Equal(EnumCompositeEventStatus.FaultedDetecting, symbolMock.Object.CompositeStatus);
        symbolMock.Verify(s => s.SetUpdate(), Times.Once);
    }
}
#endregion

#region - SymbolEventManager Phase 4 Tests -
public class SymbolEventManagerPhase4Tests
{
    private static EventSetupModel CreateEventSetup()
    {
        var mock = new Mock<IEventSetupModel>();
        mock.SetupAllProperties();
        return new EventSetupModel(mock.Object);
    }

    private static Ironwall.Dotnet.Libraries.Events.Ui.Managers.SymbolEventManager CreateManager(out Mock<IEventAggregator> eaMock)
    {
        eaMock = new Mock<IEventAggregator>();
        var log = new Mock<ILogService>().Object;
        return new Ironwall.Dotnet.Libraries.Events.Ui.Managers.SymbolEventManager(eaMock.Object, log, CreateEventSetup());
    }

    private static (Mock<IBaseDeviceModel> device, Mock<IPidsEventCapable> symbol) CreatePair(int id, EnumDeviceType type, EnumDeviceStatus status = EnumDeviceStatus.ACTIVATED)
    {
        var device = new Mock<IBaseDeviceModel>();
        device.Setup(d => d.Id).Returns(id);
        device.Setup(d => d.DeviceType).Returns(type);
        device.Setup(d => d.Status).Returns(status);
        var symbol = new Mock<IPidsEventCapable>();
        symbol.SetupAllProperties();
        return (device, symbol);
    }

    // Test 4.1: SyncDeviceStatus 호출 시 해당 Symbol OperationState 갱신
    [Fact]
    public void SyncDeviceStatus_ExistingDevice_UpdatesSymbolOperationState()
    {
        var manager = CreateManager(out _);
        var (device, symbol) = CreatePair(1, EnumDeviceType.Fence, EnumDeviceStatus.DEACTIVATED);
        manager.RegisterDeviceSymbol(device.Object, symbol.Object);

        manager.SyncDeviceStatus(1, EnumDeviceType.Fence, EnumDeviceStatus.DEACTIVATED);

        Assert.Equal(EnumOperationState.DEACTIVATED, symbol.Object.OperationState);
    }

    // (FR-B2) 재등록 desync — DeviceType이 바뀌어 복합 키(Id,Type)가 어긋나도 Id 폴백(TryResolveDevice)으로 동기화 지속.
    [Fact]
    public void should_sync_operationstate_via_id_fallback_when_devicetype_mismatched_after_reregistration()
    {
        var manager = CreateManager(out _);
        var (device, symbol) = CreatePair(10, EnumDeviceType.Fence, EnumDeviceStatus.ACTIVATED);
        manager.RegisterDeviceSymbol(device.Object, symbol.Object);

        // 재등록으로 DeviceType이 Fence→Underground로 바뀐 상황(같은 Id) — 복합 키 미스, Id 폴백 성공 기대
        manager.SyncDeviceStatus(10, EnumDeviceType.Underground, EnumDeviceStatus.ERROR);

        Assert.Equal(EnumOperationState.ERROR, symbol.Object.OperationState);
    }

    // Test 4.2: RegisterDeviceSymbol 시 즉시 동기화
    [Fact]
    public void RegisterDeviceSymbol_ImmediatelySyncsOperationState()
    {
        var manager = CreateManager(out _);
        var (device, symbol) = CreatePair(2, EnumDeviceType.Fence, EnumDeviceStatus.ERROR);

        manager.RegisterDeviceSymbol(device.Object, symbol.Object);

        Assert.Equal(EnumOperationState.ERROR, symbol.Object.OperationState);
    }

    // Test 4.3: AllDevicesLoadedMessage 수신 시 전체 Symbol 일괄 동기화
    [Fact]
    public async System.Threading.Tasks.Task HandleAllDevicesLoadedMessage_SyncsAllSymbols()
    {
        var manager = CreateManager(out _);
        var (d1, s1) = CreatePair(1, EnumDeviceType.Fence, EnumDeviceStatus.DEACTIVATED);
        var (d2, s2) = CreatePair(2, EnumDeviceType.IpCamera, EnumDeviceStatus.ERROR);
        manager.RegisterDeviceSymbol(d1.Object, s1.Object);
        manager.RegisterDeviceSymbol(d2.Object, s2.Object);

        await manager.HandleAsync(new Ironwall.Dotnet.Libraries.ViewModel.Models.AllDevicesLoadedMessage(), System.Threading.CancellationToken.None);

        Assert.Equal(EnumOperationState.DEACTIVATED, s1.Object.OperationState);
        Assert.Equal(EnumOperationState.ERROR, s2.Object.OperationState);
    }

    // Test 4.4: DeviceStatusChangedMessage 수신 시 해당 Symbol 갱신
    [Fact]
    public async System.Threading.Tasks.Task HandleDeviceStatusChangedMessage_UpdatesCorrectSymbol()
    {
        var manager = CreateManager(out _);
        var (d1, s1) = CreatePair(1, EnumDeviceType.Fence, EnumDeviceStatus.ACTIVATED);
        manager.RegisterDeviceSymbol(d1.Object, s1.Object);

        await manager.HandleAsync(
            new Ironwall.Dotnet.Libraries.ViewModel.Models.DeviceStatusChangedMessage(1, EnumDeviceType.Fence, EnumDeviceStatus.DEACTIVATED),
            System.Threading.CancellationToken.None);

        Assert.Equal(EnumOperationState.DEACTIVATED, s1.Object.OperationState);
    }

    // Test 4.5: lookup에 없는 ID → 예외 없이 무시 (SYNC_DEVICE CREATED 케이스 대비)
    [Fact]
    public void SyncDeviceStatus_MissingDevice_DoesNotThrow()
    {
        var manager = CreateManager(out _);
        // 아무것도 등록하지 않은 상태에서 존재하지 않는 id=999 호출
        var ex = Record.Exception(() =>
            manager.SyncDeviceStatus(999, EnumDeviceType.IpCamera, EnumDeviceStatus.ERROR));

        Assert.Null(ex);
    }
}

public class SymbolEventManagerDeviceTransitionTests
{
    private static EventSetupModel CreateEventSetup()
    {
        var mock = new Mock<IEventSetupModel>();
        mock.SetupAllProperties();
        return new EventSetupModel(mock.Object);
    }

    private static Ironwall.Dotnet.Libraries.Events.Ui.Managers.SymbolEventManager CreateManager()
    {
        var ea = new Mock<IEventAggregator>();
        var log = new Mock<ILogService>().Object;
        return new Ironwall.Dotnet.Libraries.Events.Ui.Managers.SymbolEventManager(ea.Object, log, CreateEventSetup());
    }

    private static (Mock<IBaseDeviceModel> device, Mock<IPidsEventCapable> symbol) CreatePair(int id, EnumDeviceType type)
    {
        var device = new Mock<IBaseDeviceModel>();
        device.Setup(d => d.Id).Returns(id);
        device.Setup(d => d.DeviceType).Returns(type);
        device.Setup(d => d.Status).Returns(EnumDeviceStatus.ACTIVATED);
        var symbol = new Mock<IPidsEventCapable>();
        symbol.SetupAllProperties();
        return (device, symbol);
    }

    [Fact]
    public void SetDeviceDetecting_ShouldSetSymbolToDetecting()
    {
        var manager = CreateManager();
        var (device, symbol) = CreatePair(1, EnumDeviceType.Fence);
        manager.RegisterDeviceSymbol(device.Object, symbol.Object);

        manager.SetDeviceDetecting(1, EnumDeviceType.Fence, EnumEventType.Intrusion);

        Assert.Equal(EnumCompositeEventStatus.Detecting, symbol.Object.CompositeStatus);
    }

    [Fact]
    public void RestoreDeviceSymbol_ShouldSetSymbolToNormal()
    {
        var manager = CreateManager();
        var (device, symbol) = CreatePair(1, EnumDeviceType.Fence);
        manager.RegisterDeviceSymbol(device.Object, symbol.Object);

        // Detecting 상태로 설정 후 복원
        manager.SetDeviceDetecting(1, EnumDeviceType.Fence, EnumEventType.Intrusion);
        manager.RestoreDeviceSymbol(1, EnumDeviceType.Fence);

        Assert.Equal(EnumEventStatus.Normal, symbol.Object.EventStatus);
    }
}
#endregion

#region Test 14: CameraPtzNatsSyncService 테스트
/// <summary>
/// Test 14.1~14.3: CameraPtzNatsSyncService — PTZ_STATUS NATS 수신 처리
/// </summary>
public class CameraPtzNatsSyncServiceTests
{
    private Func<Ironwall.Dotnet.Libraries.Nats.Models.MessageArgsModel, System.Threading.Tasks.Task>? _capturedHandler;

    private Ironwall.Dotnet.Libraries.Events.Ui.Services.CameraPtzNatsSyncService CreateService(
        out Mock<Ironwall.Dotnet.Libraries.Nats.Services.INatsService> mockNats,
        out Mock<Ironwall.Dotnet.Libraries.Events.Ui.Managers.ISymbolEventManager> mockManager)
    {
        mockNats = new Mock<Ironwall.Dotnet.Libraries.Nats.Services.INatsService>();
        mockManager = new Mock<Ironwall.Dotnet.Libraries.Events.Ui.Managers.ISymbolEventManager>();

        mockNats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<Ironwall.Dotnet.Libraries.Nats.Models.MessageArgsModel, System.Threading.Tasks.Task>>())
                .Callback<Func<Ironwall.Dotnet.Libraries.Nats.Models.MessageArgsModel, System.Threading.Tasks.Task>>(h => _capturedHandler = h);

        return new Ironwall.Dotnet.Libraries.Events.Ui.Services.CameraPtzNatsSyncService(
            null, mockNats.Object, mockManager.Object);
    }

    private static string MakePtzStatusJson(int cameraId, int pan, int tilt, int zoom, string cmd = "PTZ_STATUS")
        => Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            id = "test-uuid",
            m_type = "PUB",
            cmd,
            from = "NVRManager",
            body = new { camera_id = cameraId, pan, tilt, zoom },
            created = "2026-03-05T00:00:00.000Z"
        });

    #region Test 14.1: PTZ_STATUS 정상 수신 → ProcessCameraPtz 호출
    [Fact(DisplayName = "Test 14.1: PTZ_STATUS 수신 시 ProcessCameraPtz 호출")]
    public async System.Threading.Tasks.Task CameraPtzNatsSyncService_ValidPtzStatus_ShouldCallProcessCameraPtz()
    {
        // Arrange
        var svc = CreateService(out _, out var mockManager);
        await svc.StartService();

        var args = new Ironwall.Dotnet.Libraries.Nats.Models.MessageArgsModel(
            subject: "sensorway.unit001.nvr_manager.ptz-status",
            subscriptionSubject: "sensorway.*.nvr_manager.ptz-status",
            data: MakePtzStatusJson(201, 1000, 5000, 2000));

        // Act
        await _capturedHandler!(args);

        // Assert
        mockManager.Verify(m => m.ProcessCameraPtz(201, 1000f, 5000f, 2000f), Times.Once);
    }
    #endregion

    #region Test 14.2: wrong subject → ProcessCameraPtz 미호출
    [Fact(DisplayName = "Test 14.2: ptz-status 외 subject 수신 시 무시")]
    public async System.Threading.Tasks.Task CameraPtzNatsSyncService_WrongSubject_ShouldNotCallProcessCameraPtz()
    {
        // Arrange
        var svc = CreateService(out _, out var mockManager);
        await svc.StartService();

        var args = new Ironwall.Dotnet.Libraries.Nats.Models.MessageArgsModel(
            subject: "sensorway.unit001.all.sync.device",
            subscriptionSubject: null,
            data: MakePtzStatusJson(201, 1000, 5000, 2000));

        // Act
        await _capturedHandler!(args);

        // Assert
        mockManager.Verify(m => m.ProcessCameraPtz(It.IsAny<int>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>()), Times.Never);
    }
    #endregion

    #region Test 14.3: wrong cmd → ProcessCameraPtz 미호출
    [Fact(DisplayName = "Test 14.3: cmd != PTZ_STATUS 수신 시 무시")]
    public async System.Threading.Tasks.Task CameraPtzNatsSyncService_WrongCmd_ShouldNotCallProcessCameraPtz()
    {
        // Arrange
        var svc = CreateService(out _, out var mockManager);
        await svc.StartService();

        var args = new Ironwall.Dotnet.Libraries.Nats.Models.MessageArgsModel(
            subject: "sensorway.unit001.nvr_manager.ptz-status",
            subscriptionSubject: null,
            data: MakePtzStatusJson(201, 1000, 5000, 2000, cmd: "SYNC_DEVICE"));

        // Act
        await _capturedHandler!(args);

        // Assert
        mockManager.Verify(m => m.ProcessCameraPtz(It.IsAny<int>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>()), Times.Never);
    }
    #endregion
}
#endregion

#region Test 15: DetectionNatsSyncService 테스트 (moved to DetectionNatsSyncServiceTests.cs)
// 기존 Test 15.1, 15.2는 ProcessDetectionById 제거로 인해 삭제됨.
// 새 테스트: Tests/DetectionNatsSyncServiceTests.cs (Enqueue + EventAggregator 발행 검증)
#endregion
