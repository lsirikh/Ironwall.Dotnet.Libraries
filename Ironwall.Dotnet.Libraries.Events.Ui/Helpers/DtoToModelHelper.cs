using Ironwall.Dotnet.Libraries.Api.Messages.Events;
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
            DateTime = DateTime.Parse(dto.Datetime).ToUniversalTime(),
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
            Datetime = model.DateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            TypeEvent = model.MessageType.ToString(),
            GroupEvent = model.EventGroup ?? string.Empty,
            ActionReported = model.Status == EnumTrueFalse.True ? "True" : "False",
            Result = model.Result.ToString(),
            Sensor = model.Device?.Id ?? 0
        };
    }
}
