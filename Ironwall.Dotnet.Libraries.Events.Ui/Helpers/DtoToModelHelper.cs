using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Helpers;

/// <summary>
/// Event DTO ↔ Model 변환 Helper
/// Devices.Ui의 DtoToModelHelper 패턴 적용
/// </summary>
public static class DtoToModelHelper
{
    /// <summary>
    /// DetectionEventDto → IDetectionEventModel 변환
    /// </summary>
    public static IDetectionEventModel ToDetectionEventModel(this DetectionEventDto dto)
    {
        return new DetectionEventModel
        {
            Id = dto.Id,
            DateTime = DateTime.Parse(dto.CreatedAt ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")).ToUniversalTime(),
            MessageType = Enum.Parse<EnumEventType>(dto.TypeEvent),
            EventGroup = dto.GroupEvent,
            Status = dto.ActionReported == "True" ? EnumTrueFalse.True : EnumTrueFalse.False,
            Result = Enum.Parse<EnumDetectionType>(dto.Result),
            Device = new SensorDeviceModel { Id = dto.Sensor }
        };
    }

    /// <summary>
    /// IDetectionEventModel → DetectionEventDto 변환 (역방향)
    /// </summary>
    public static DetectionEventDto ToDetectionEventDto(this IDetectionEventModel model)
    {
        return new DetectionEventDto
        {
            Id = model.Id,
            CreatedAt = model.DateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            TypeEvent = model.MessageType.ToString(),
            GroupEvent = model.EventGroup ?? string.Empty,
            ActionReported = model.Status == EnumTrueFalse.True ? "True" : "False",
            Result = model.Result.ToString(),
            Sensor = model.Device?.Id ?? 0
        };
    }

    /// <summary>
    /// MalfunctionEventDto → IMalfunctionEventModel 변환
    /// </summary>
    public static IMalfunctionEventModel ToMalfunctionEventModel(this MalfunctionEventDto dto)
    {
        return new MalfunctionEventModel
        {
            Id = dto.Id,
            DateTime = DateTime.Parse(dto.CreatedAt ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")).ToUniversalTime(),
            MessageType = Enum.Parse<EnumEventType>(dto.TypeEvent),
            EventGroup = dto.GroupEvent,
            Status = dto.Status == "True" ? EnumTrueFalse.True : EnumTrueFalse.False,
            Reason = Enum.Parse<EnumFaultType>(dto.Reason),
            FirstStart = dto.FirstStart,
            FirstEnd = dto.FirstEnd,
            SecondStart = dto.SecondStart,
            SecondEnd = dto.SecondEnd,
            Device = new SensorDeviceModel { Id = dto.Sensor }
        };
    }

    /// <summary>
    /// IMalfunctionEventModel → MalfunctionEventDto 변환 (역방향)
    /// </summary>
    public static MalfunctionEventDto ToMalfunctionEventDto(this IMalfunctionEventModel model)
    {
        return new MalfunctionEventDto
        {
            Id = model.Id,
            CreatedAt = model.DateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            TypeEvent = model.MessageType.ToString(),
            GroupEvent = model.EventGroup ?? string.Empty,
            Status = model.Status == EnumTrueFalse.True ? "True" : "False",
            Reason = model.Reason.ToString(),
            FirstStart = model.FirstStart,
            FirstEnd = model.FirstEnd,
            SecondStart = model.SecondStart,
            SecondEnd = model.SecondEnd,
            Sensor = model.Device?.Id ?? 0
        };
    }

    /// <summary>
    /// ConnectionEventDto → IConnectionEventModel 변환
    /// </summary>
    public static IConnectionEventModel ToConnectionEventModel(this ConnectionEventDto dto)
    {
        return new ConnectionEventModel
        {
            Id = dto.Id,
            DateTime = DateTime.Parse(dto.CreatedAt ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")).ToUniversalTime(),
            MessageType = Enum.Parse<EnumEventType>(dto.TypeEvent),
            EventGroup = dto.GroupEvent,
            Status = EnumTrueFalse.True, // Connection events are typically status=True
            Device = new SensorDeviceModel { Id = dto.Sensor }
        };
    }

    /// <summary>
    /// IConnectionEventModel → ConnectionEventDto 변환 (역방향)
    /// </summary>
    public static ConnectionEventDto ToConnectionEventDto(this IConnectionEventModel model)
    {
        return new ConnectionEventDto
        {
            Id = model.Id,
            CreatedAt = model.DateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            TypeEvent = model.MessageType.ToString(),
            GroupEvent = model.EventGroup ?? string.Empty,
            Sensor = model.Device?.Id ?? 0
        };
    }

    /// <summary>
    /// ActionEventDto → IActionEventModel 변환
    /// </summary>
    public static IActionEventModel ToActionEventModel(this ActionEventDto dto)
    {
        return new ActionEventModel
        {
            Id = dto.Id,
            DateTime = DateTime.Parse(dto.CreatedAt ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")).ToUniversalTime(),
            MessageType = EnumEventType.Action,
            Content = dto.Content,
            User = dto.User,
            OriginEvent = null // TODO: Handle nested event conversion if needed
        };
    }

    /// <summary>
    /// IActionEventModel → ActionEventDto 변환 (역방향)
    /// </summary>
    public static ActionEventDto ToActionEventDto(this IActionEventModel model)
    {
        return new ActionEventDto
        {
            Id = model.Id,
            CreatedAt = model.DateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            TypeEvent = model.MessageType.ToString(),
            Content = model.Content ?? string.Empty,
            User = model.User ?? string.Empty,
            FromEvent = null // TODO: Handle nested event conversion if needed
        };
    }
}
