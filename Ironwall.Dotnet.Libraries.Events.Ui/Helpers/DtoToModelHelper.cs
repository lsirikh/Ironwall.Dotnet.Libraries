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
            Device = ResolveDevice(dto.Sensor, deviceProvider)
        };
    }

    /// <summary>
    /// Device ID를 DeviceProvider에서 조회하여 실제 Instance 반환
    /// <para>⚠️ 중요: GOP DB에서 각 Device 타입의 ID는 독립적이므로 타입 필터링 필수</para>
    /// <para>예: controllers.id=1, sensors.id=1, cameras.id=1 모두 존재 가능</para>
    /// </summary>
    /// <param name="deviceId">Device ID (DTO의 Sensor 필드)</param>
    /// <param name="deviceProvider">Device Provider (null 가능)</param>
    /// <returns>매칭된 Device Instance 또는 ID만 가진 빈 SensorDeviceModel</returns>
    private static ISensorDeviceModel ResolveDevice(int deviceId, DeviceProvider? deviceProvider)
    {
        if (deviceId <= 0)
            return new SensorDeviceModel { Id = 0 };

        if (deviceProvider == null)
            return new SensorDeviceModel { Id = deviceId };

        // ✅ DeviceProvider에서 타입 필터링 후 ID로 조회
        // OfType<ISensorDeviceModel>()로 Sensor만 먼저 필터링
        var device = deviceProvider
            .OfType<ISensorDeviceModel>()
            .FirstOrDefault(d => d.Id == deviceId);

        if (device != null)
            return device; // 실제 Device Instance

        // Fallback: Provider에 없으면 ID만 가진 객체
        return new SensorDeviceModel { Id = deviceId };
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
            Status = dto.Status == "True" ? EnumTrueFalse.True : EnumTrueFalse.False,
            Reason = Enum.Parse<EnumFaultType>(dto.Reason),
            FirstStart = dto.FirstStart,
            FirstEnd = dto.FirstEnd,
            SecondStart = dto.SecondStart,
            SecondEnd = dto.SecondEnd,
            Device = ResolveDevice(dto.Sensor, deviceProvider)
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
            Device = ResolveDevice(dto.Sensor, deviceProvider)
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
}
