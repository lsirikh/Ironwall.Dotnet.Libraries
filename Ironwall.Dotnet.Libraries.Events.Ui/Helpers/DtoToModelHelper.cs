using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Ironwall.Dotnet.Libraries.Devices.Providers;

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
            Device = ResolveDeviceFromDto(dto.Controller, dto.Sensor, dto.TypeDevice, null)
        };
    }

    /// <summary>
    /// IDetectionEventModel → DetectionEventDto 변환 (역방향)
    /// </summary>
    public static DetectionEventDto ToDetectionEventDto(this IDetectionEventModel model)
    {
        var (controllerId, sensorId) = ResolveDeviceIds(model.Device);
        return new DetectionEventDto
        {
            Id = model.Id,
            CreatedAt = model.DateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            TypeEvent = model.MessageType.ToString(),
            GroupEvent = model.EventGroup ?? string.Empty,
            ActionReported = model.Status == EnumTrueFalse.True ? "True" : "False",
            Result = model.Result.ToString(),
            Controller = controllerId,
            Sensor = sensorId,
            TypeDevice = model.Device?.DeviceType.ToString() ?? string.Empty,
            Sequence = 0
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
            Status = dto.ActionReported == "True" ? EnumTrueFalse.True : EnumTrueFalse.False,
            Reason = Enum.Parse<EnumFaultType>(dto.Reason),
            FirstStart = dto.FirstStart,
            FirstEnd = dto.FirstEnd,
            SecondStart = dto.SecondStart,
            SecondEnd = dto.SecondEnd,
            Device = ResolveDeviceFromDto(dto.Controller, dto.Sensor, dto.TypeDevice, null)
        };
    }

    /// <summary>
    /// IMalfunctionEventModel → MalfunctionEventDto 변환 (역방향)
    /// </summary>
    public static MalfunctionEventDto ToMalfunctionEventDto(this IMalfunctionEventModel model)
    {
        var (controllerId, sensorId) = ResolveDeviceIds(model.Device);
        return new MalfunctionEventDto
        {
            Id = model.Id,
            CreatedAt = model.DateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            TypeEvent = model.MessageType.ToString(),
            GroupEvent = model.EventGroup ?? string.Empty,
            ActionReported = model.Status == EnumTrueFalse.True ? "True" : "False",
            Reason = model.Reason.ToString(),
            FirstStart = model.FirstStart,
            FirstEnd = model.FirstEnd,
            SecondStart = model.SecondStart,
            SecondEnd = model.SecondEnd,
            Controller = controllerId,
            Sensor = sensorId,
            TypeDevice = model.Device?.DeviceType.ToString() ?? string.Empty,
            Sequence = 0
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
            Device = ResolveDeviceFromDto(dto.Controller, dto.Sensor, dto.TypeDevice, null)
        };
    }

    /// <summary>
    /// IConnectionEventModel → ConnectionEventDto 변환 (역방향)
    /// </summary>
    public static ConnectionEventDto ToConnectionEventDto(this IConnectionEventModel model)
    {
        var (controllerId, sensorId) = ResolveDeviceIds(model.Device);
        return new ConnectionEventDto
        {
            Id = model.Id,
            CreatedAt = model.DateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            TypeEvent = model.MessageType.ToString(),
            GroupEvent = model.EventGroup ?? string.Empty,
            Controller = controllerId,
            Sensor = sensorId,
            TypeDevice = model.Device?.DeviceType.ToString() ?? string.Empty,
            Sequence = 0
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

    // ═══════════════════════════════════════════════════════════════════════════════
    // Device Resolution Helpers (Controller/Sensor 타입 구분)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// DTO의 Controller/Sensor 조합으로 적절한 Device 반환
    /// <para>Controller > 0 && Sensor == 0 → ControllerDeviceModel</para>
    /// <para>Sensor > 0 → SensorDeviceModel</para>
    /// </summary>
    private static IBaseDeviceModel? ResolveDeviceFromDto(
        int controller,
        int sensor,
        string? typeDevice,
        DeviceProvider? deviceProvider)
    {
        // Case 1: Sensor가 있으면 Sensor Device
        if (sensor > 0)
        {
            if (deviceProvider != null)
            {
                var sensorDevice = deviceProvider
                    .OfType<ISensorDeviceModel>()
                    .FirstOrDefault(d => d.Id == sensor);
                if (sensorDevice != null)
                    return sensorDevice;
            }
            return new SensorDeviceModel { Id = sensor };
        }

        // Case 2: Controller만 있으면 Controller Device
        if (controller > 0 && sensor == 0)
        {
            if (deviceProvider != null)
            {
                var controllerDevice = deviceProvider
                    .OfType<IControllerDeviceModel>()
                    .FirstOrDefault(d => d.Id == controller);
                if (controllerDevice != null)
                    return controllerDevice;
            }
            return new ControllerDeviceModel { Id = controller };
        }

        // Case 3: 둘 다 없음
        return null;
    }

    /// <summary>
    /// Device에서 Controller ID와 Sensor ID 추출
    /// <para>SensorDeviceModel → (Controller.Id, Sensor.Id)</para>
    /// <para>ControllerDeviceModel → (Controller.Id, 0)</para>
    /// </summary>
    private static (int ControllerId, int SensorId) ResolveDeviceIds(IBaseDeviceModel? device)
    {
        if (device == null)
            return (0, 0);

        // Case 1: SensorDeviceModel
        if (device is ISensorDeviceModel sensorDevice)
        {
            return (sensorDevice.Controller?.Id ?? 0, sensorDevice.Id);
        }

        // Case 2: ControllerDeviceModel
        if (device is IControllerDeviceModel controllerDevice)
        {
            return (controllerDevice.Id, 0);  // Controller만 있고 Sensor는 0
        }

        // Case 3: 기타 (CameraDevice 등)
        return (0, device.Id);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // DeviceProvider 통합 오버로드 (TDD로 구현)
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
            DateTime = DateTime.Parse(dto.CreatedAt ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")).ToUniversalTime(),
            MessageType = Enum.Parse<EnumEventType>(dto.TypeEvent),
            EventGroup = dto.GroupEvent,
            Status = dto.ActionReported == "True" ? EnumTrueFalse.True : EnumTrueFalse.False,
            Result = Enum.Parse<EnumDetectionType>(dto.Result),
            Device = ResolveDeviceFromDto(dto.Controller, dto.Sensor, dto.TypeDevice, deviceProvider)
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
            DateTime = DateTime.Parse(dto.CreatedAt ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")).ToUniversalTime(),
            MessageType = Enum.Parse<EnumEventType>(dto.TypeEvent),
            EventGroup = dto.GroupEvent,
            Status = dto.ActionReported == "True" ? EnumTrueFalse.True : EnumTrueFalse.False,
            Reason = Enum.Parse<EnumFaultType>(dto.Reason),
            FirstStart = dto.FirstStart,
            FirstEnd = dto.FirstEnd,
            SecondStart = dto.SecondStart,
            SecondEnd = dto.SecondEnd,
            Device = ResolveDeviceFromDto(dto.Controller, dto.Sensor, dto.TypeDevice, deviceProvider)
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
            DateTime = DateTime.Parse(dto.CreatedAt ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")).ToUniversalTime(),
            MessageType = Enum.Parse<EnumEventType>(dto.TypeEvent),
            EventGroup = dto.GroupEvent,
            Status = EnumTrueFalse.True,
            Device = ResolveDeviceFromDto(dto.Controller, dto.Sensor, dto.TypeDevice, deviceProvider)
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // EventProvider 통합 오버로드 (TDD로 구현 - OriginEvent Instantiation)
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
            DateTime = DateTime.Parse(dto.CreatedAt ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")).ToUniversalTime(),
            MessageType = EnumEventType.Action,
            Content = dto.Content,
            User = dto.User,
            OriginEvent = ResolveOriginEvent(dto.FromEvent, eventProvider, deviceProvider)
        };
    }

    /// <summary>
    /// FromEvent DTO를 EventProvider에서 조회하거나 변환
    /// <para>⚠️ 중요: GOP DB에서 각 Event 타입의 ID는 독립적이므로 타입 필터링 필수</para>
    /// <para>⚠️ 조치보고 대상: DetectionEventDto, MalfunctionEventDto만 해당 (ConnectionEventDto 제외)</para>
    /// </summary>
    private static IExEventModel? ResolveOriginEvent(
        object? fromEvent,
        Ironwall.Dotnet.Libraries.Events.Providers.EventProvider? eventProvider,
        DeviceProvider? deviceProvider)
    {
        if (fromEvent == null)
            return null;

        // FromEvent의 타입에 따라 적절한 변환 메서드 호출
        switch (fromEvent)
        {
            case DetectionEventDto detectionDto:
                if (eventProvider != null)
                {
                    // ✅ 타입 필터링 후 ID 매칭
                    var existing = eventProvider
                        .OfType<IDetectionEventModel>()
                        .FirstOrDefault(e => e.Id == detectionDto.Id);

                    if (existing != null)
                        return existing;
                }
                // Fallback: DTO 직접 변환 (DeviceProvider 전달하여 Device도 매칭)
                return detectionDto.ToDetectionEventModel(deviceProvider);

            case MalfunctionEventDto malfunctionDto:
                if (eventProvider != null)
                {
                    // ✅ 타입 필터링 후 ID 매칭
                    var existing = eventProvider
                        .OfType<IMalfunctionEventModel>()
                        .FirstOrDefault(e => e.Id == malfunctionDto.Id);

                    if (existing != null)
                        return existing;
                }
                // Fallback: DTO 직접 변환 (DeviceProvider 전달하여 Device도 매칭)
                return malfunctionDto.ToMalfunctionEventModel(deviceProvider);

            default:
                // ConnectionEventDto나 기타 타입은 조치보고 대상 아님
                return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // DetectionExEventDto 변환 (NATS 메시지 Body 구조)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// DetectionExEventDto → IDetectionEventModel 변환
    /// <para>OriginEvent를 추출하여 DetectionEventModel로 변환</para>
    /// </summary>
    /// <param name="dto">DetectionExEventDto 인스턴스</param>
    /// <returns>IDetectionEventModel 인스턴스</returns>
    /// <exception cref="ArgumentNullException">OriginEvent가 null인 경우</exception>
    public static IDetectionEventModel ToDetectionEventModel(this DetectionExEventDto dto)
    {
        if (dto.OriginEvent == null)
            throw new ArgumentNullException(nameof(dto.OriginEvent), "OriginEvent cannot be null");

        return dto.OriginEvent.ToDetectionEventModel();
    }

    /// <summary>
    /// DetectionExEventDto → IDetectionEventModel 변환 (DeviceProvider 활용)
    /// </summary>
    /// <param name="dto">DetectionExEventDto 인스턴스</param>
    /// <param name="deviceProvider">DeviceProvider (Device 매핑용)</param>
    /// <returns>IDetectionEventModel 인스턴스</returns>
    /// <exception cref="ArgumentNullException">OriginEvent가 null인 경우</exception>
    public static IDetectionEventModel ToDetectionEventModel(
        this DetectionExEventDto dto,
        DeviceProvider? deviceProvider)
    {
        if (dto.OriginEvent == null)
            throw new ArgumentNullException(nameof(dto.OriginEvent), "OriginEvent cannot be null");

        return dto.OriginEvent.ToDetectionEventModel(deviceProvider);
    }
}
