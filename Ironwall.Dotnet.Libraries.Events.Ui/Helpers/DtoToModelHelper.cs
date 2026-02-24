using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Helpers;

/// <summary>
/// Event DTO ↔ Model 변환 Helper
/// Nested Device 구조 기반 (Phase 8B)
/// </summary>
public static class DtoToModelHelper
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // DTO → Model 변환 (기본)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// DetectionEventDto → IDetectionEventModel 변환
    /// </summary>
    public static IDetectionEventModel ToDetectionEventModel(this DetectionEventDto dto)
    {
        return new DetectionEventModel
        {
            Id = dto.Id,
            DateTime = ParseDateTime(dto.CreatedAt),
            MessageType = Enum.Parse<EnumEventType>(dto.TypeEvent),
            Status = dto.ActionReported == "True" ? EnumTrueFalse.True : EnumTrueFalse.False,
            Result = Enum.Parse<EnumDetectionType>(dto.Result),
            Device = ConvertDeviceFromDto(dto.Device, null)
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
            DateTime = ParseDateTime(dto.CreatedAt),
            MessageType = Enum.Parse<EnumEventType>(dto.TypeEvent),
            Status = dto.ActionReported == "True" ? EnumTrueFalse.True : EnumTrueFalse.False,
            Reason = Enum.Parse<EnumFaultType>(dto.Reason),
            FirstStart = dto.Detail?.FirstStart ?? 0,
            FirstEnd = dto.Detail?.FirstEnd ?? 0,
            SecondStart = dto.Detail?.SecondStart ?? 0,
            SecondEnd = dto.Detail?.SecondEnd ?? 0,
            Device = ConvertDeviceFromDto(dto.Device, null)
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
            DateTime = ParseDateTime(dto.CreatedAt),
            MessageType = Enum.Parse<EnumEventType>(dto.TypeEvent),
            Status = EnumTrueFalse.True,
            Device = ConvertDeviceFromDto(dto.Device, null)
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
            DateTime = ParseDateTime(dto.CreatedAt),
            MessageType = EnumEventType.Action,
            Content = dto.Content,
            User = dto.User,
            OriginEvent = null
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Model → DTO 변환 (역방향)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// IDetectionEventModel → DetectionEventDto 변환
    /// </summary>
    public static DetectionEventDto ToDetectionEventDto(this IDetectionEventModel model)
    {
        return new DetectionEventDto
        {
            Id = model.Id,
            CreatedAt = model.DateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            TypeEvent = model.MessageType.ToString(),
            ActionReported = model.Status == EnumTrueFalse.True ? "True" : "False",
            Result = model.Result.ToString(),
            Device = ConvertDeviceToDto(model.Device),
            DeviceDescription = model.Device?.DeviceName
        };
    }

    /// <summary>
    /// IMalfunctionEventModel → MalfunctionEventDto 변환
    /// </summary>
    public static MalfunctionEventDto ToMalfunctionEventDto(this IMalfunctionEventModel model)
    {
        return new MalfunctionEventDto
        {
            Id = model.Id,
            CreatedAt = model.DateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            TypeEvent = model.MessageType.ToString(),
            ActionReported = model.Status == EnumTrueFalse.True ? "True" : "False",
            Reason = model.Reason.ToString(),
            Device = ConvertDeviceToDto(model.Device),
            DeviceDescription = model.Device?.DeviceName,
            Detail = new MalfunctionDetailDto
            {
                FirstStart = model.FirstStart,
                FirstEnd = model.FirstEnd,
                SecondStart = model.SecondStart,
                SecondEnd = model.SecondEnd
            }
        };
    }

    /// <summary>
    /// IConnectionEventModel → ConnectionEventDto 변환
    /// </summary>
    public static ConnectionEventDto ToConnectionEventDto(this IConnectionEventModel model)
    {
        return new ConnectionEventDto
        {
            Id = model.Id,
            CreatedAt = model.DateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            TypeEvent = model.MessageType.ToString(),
            Device = ConvertDeviceToDto(model.Device),
            DeviceDescription = model.Device?.DeviceName
        };
    }

    /// <summary>
    /// IActionEventModel → ActionEventDto 변환
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
            FromEvent = null
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // DeviceProvider 통합 오버로드
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// DetectionEventDto → IDetectionEventModel 변환 (DeviceProvider 활용)
    /// </summary>
    public static IDetectionEventModel ToDetectionEventModel(
        this DetectionEventDto dto,
        DeviceProvider? deviceProvider)
    {
        return new DetectionEventModel
        {
            Id = dto.Id,
            DateTime = ParseDateTime(dto.CreatedAt),
            MessageType = Enum.Parse<EnumEventType>(dto.TypeEvent),
            Status = dto.ActionReported == "True" ? EnumTrueFalse.True : EnumTrueFalse.False,
            Result = Enum.Parse<EnumDetectionType>(dto.Result),
            Device = ConvertDeviceFromDto(dto.Device, deviceProvider)
        };
    }

    /// <summary>
    /// MalfunctionEventDto → IMalfunctionEventModel 변환 (DeviceProvider 활용)
    /// </summary>
    public static IMalfunctionEventModel ToMalfunctionEventModel(
        this MalfunctionEventDto dto,
        DeviceProvider? deviceProvider)
    {
        return new MalfunctionEventModel
        {
            Id = dto.Id,
            DateTime = ParseDateTime(dto.CreatedAt),
            MessageType = Enum.Parse<EnumEventType>(dto.TypeEvent),
            Status = dto.ActionReported == "True" ? EnumTrueFalse.True : EnumTrueFalse.False,
            Reason = Enum.Parse<EnumFaultType>(dto.Reason),
            FirstStart = dto.Detail?.FirstStart ?? 0,
            FirstEnd = dto.Detail?.FirstEnd ?? 0,
            SecondStart = dto.Detail?.SecondStart ?? 0,
            SecondEnd = dto.Detail?.SecondEnd ?? 0,
            Device = ConvertDeviceFromDto(dto.Device, deviceProvider)
        };
    }

    /// <summary>
    /// ConnectionEventDto → IConnectionEventModel 변환 (DeviceProvider 활용)
    /// </summary>
    public static IConnectionEventModel ToConnectionEventModel(
        this ConnectionEventDto dto,
        DeviceProvider? deviceProvider)
    {
        return new ConnectionEventModel
        {
            Id = dto.Id,
            DateTime = ParseDateTime(dto.CreatedAt),
            MessageType = Enum.Parse<EnumEventType>(dto.TypeEvent),
            Status = EnumTrueFalse.True,
            Device = ConvertDeviceFromDto(dto.Device, deviceProvider)
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // EventProvider 통합 오버로드
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ActionEventDto → IActionEventModel 변환 (EventProvider 및 DeviceProvider 활용)
    /// </summary>
    public static IActionEventModel ToActionEventModel(
        this ActionEventDto dto,
        Ironwall.Dotnet.Libraries.Events.Providers.EventProvider? eventProvider,
        DeviceProvider? deviceProvider = null)
    {
        return new ActionEventModel
        {
            Id = dto.Id,
            DateTime = ParseDateTime(dto.CreatedAt),
            MessageType = EnumEventType.Action,
            Content = dto.Content,
            User = dto.User,
            OriginEvent = ResolveOriginEvent(dto.FromEvent, eventProvider, deviceProvider)
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Device 변환 헬퍼 (Nested DTO ↔ Model)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// BaseDeviceDto → IBaseDeviceModel 변환
    /// <para>DeviceProvider가 있으면 ID로 실제 Device 조회, 없으면 DTO 기반 최소 모델 생성</para>
    /// </summary>
    private static IBaseDeviceModel? ConvertDeviceFromDto(
        BaseDeviceDto? deviceDto,
        DeviceProvider? deviceProvider)
    {
        if (deviceDto == null)
            return null;

        // DeviceProvider가 있으면 ID로 실제 Device 조회
        if (deviceProvider != null)
        {
            var existing = deviceProvider.FirstOrDefault(d => d.Id == deviceDto.Id);
            if (existing != null)
                return existing;
        }

        // Fallback: DTO에서 최소 모델 생성
        if (!string.IsNullOrEmpty(deviceDto.TypeDevice) &&
            Enum.TryParse<EnumDeviceType>(deviceDto.TypeDevice, out var deviceType))
        {
            switch (deviceType)
            {
                case EnumDeviceType.Controller:
                    return new ControllerDeviceModel { Id = deviceDto.Id, DeviceType = deviceType };
                case EnumDeviceType.IpCamera:
                    return new CameraDeviceModel { Id = deviceDto.Id, DeviceType = deviceType };
                default:
                    // Sensor 계열 (Fence, Multi, PIR, etc.)
                    return new SensorDeviceModel { Id = deviceDto.Id, DeviceType = deviceType };
            }
        }

        return new SensorDeviceModel { Id = deviceDto.Id };
    }

    /// <summary>
    /// IBaseDeviceModel → BaseDeviceDto 변환
    /// </summary>
    private static BaseDeviceDto? ConvertDeviceToDto(IBaseDeviceModel? device)
    {
        if (device == null)
            return null;

        return new BaseDeviceDto
        {
            Id = device.Id,
            TypeDevice = device.DeviceType.ToString(),
            NameDevice = device.DeviceName ?? string.Empty,
            NumberDevice = device.DeviceNumber
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // OriginEvent 변환 헬퍼
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// FromEvent DTO를 EventProvider에서 조회하거나 변환
    /// </summary>
    private static IExEventModel? ResolveOriginEvent(
        object? fromEvent,
        Ironwall.Dotnet.Libraries.Events.Providers.EventProvider? eventProvider,
        DeviceProvider? deviceProvider)
    {
        if (fromEvent == null)
            return null;

        try
        {
            var eventSnapshot = eventProvider?.ToList();

            switch (fromEvent)
            {
                case DetectionEventDto detectionDto:
                    if (eventProvider != null)
                    {
                        var existing = eventSnapshot?
                            .OfType<IDetectionEventModel>()
                            .FirstOrDefault(e => e.Id == detectionDto.Id);
                        if (existing != null)
                            return existing;
                    }
                    return detectionDto.ToDetectionEventModel(deviceProvider);

                case MalfunctionEventDto malfunctionDto:
                    if (eventProvider != null)
                    {
                        var existing = eventSnapshot?
                            .OfType<IMalfunctionEventModel>()
                            .FirstOrDefault(e => e.Id == malfunctionDto.Id);
                        if (existing != null)
                            return existing;
                    }
                    return malfunctionDto.ToMalfunctionEventModel(deviceProvider);

                default:
                    return null;
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // DateTime 파싱 헬퍼
    // ═══════════════════════════════════════════════════════════════════════════════

    private static DateTime ParseDateTime(string? dateTimeString)
    {
        if (string.IsNullOrEmpty(dateTimeString))
            return DateTime.Now;

        if (DateTime.TryParse(dateTimeString, out var dateTime))
            return dateTime;

        return DateTime.Now;
    }
}
