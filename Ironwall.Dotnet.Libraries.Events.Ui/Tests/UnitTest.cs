using Xunit;
using Moq;
using Ironwall.Dotnet.Libraries.Api.Messages.Events;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Ironwall.Dotnet.Monitoring.Models.Devices;

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
            Datetime = "2025-11-24T10:30:00.000Z",
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
        Assert.Equal("2025-11-24T10:30:00.000Z", dto.Datetime);
        Assert.Equal("Intrusion", dto.TypeEvent);
        Assert.Equal("Zone A", dto.GroupEvent);
        Assert.Equal("True", dto.ActionReported);
        Assert.Equal("THERMAL_SENSOR", dto.Result);
        Assert.Equal(100, dto.Sensor);
    }
}

public class EventProviderServiceTests
{
    // Tests will be added following TDD cycle
}
