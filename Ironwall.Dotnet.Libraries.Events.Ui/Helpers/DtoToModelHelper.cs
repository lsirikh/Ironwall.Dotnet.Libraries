using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
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
            Status = EnumTrueFalse.False,
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
            DeviceId = model.Device?.Id ?? 0,   // 서버 Create는 flat device_id(FK) 필수
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
            DeviceId = model.Device?.Id ?? 0,   // 서버 Create는 flat device_id(FK) 필수
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
            DeviceId = model.Device?.Id ?? 0,   // 서버 Create는 flat device_id(FK) 필수
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
            FromEvent = ConvertOriginEventToDto(model.OriginEvent)
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Model → Replace DTO 변환 (수정 PUT 전용, 서버 *EventReplace 계약: 허용 필드만)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>IDetectionEventModel → DetectionEventReplaceDto (PUT 전용, type_event/result/detail만)</summary>
    public static DetectionEventReplaceDto ToDetectionEventReplaceDto(this IDetectionEventModel model)
        => new()
        {
            TypeEvent = model.MessageType.ToString(),
            Result = model.Result.ToString()
        };

    /// <summary>IMalfunctionEventModel → MalfunctionEventReplaceDto (PUT 전용, type_event/reason/detail만)</summary>
    public static MalfunctionEventReplaceDto ToMalfunctionEventReplaceDto(this IMalfunctionEventModel model)
        => new()
        {
            TypeEvent = model.MessageType.ToString(),
            Reason = model.Reason.ToString(),
            Detail = new MalfunctionDetailDto
            {
                FirstStart = model.FirstStart,
                FirstEnd = model.FirstEnd,
                SecondStart = model.SecondStart,
                SecondEnd = model.SecondEnd
            }
        };

    /// <summary>IConnectionEventModel → ConnectionEventReplaceDto (PUT 전용, type_event만)</summary>
    public static ConnectionEventReplaceDto ToConnectionEventReplaceDto(this IConnectionEventModel model)
        => new()
        {
            TypeEvent = model.MessageType.ToString()
        };

    /// <summary>IActionEventModel → ActionEventReplaceDto (PUT 전용, type_event/content/user만 — from_event_id 제외, 원본 연결 불변)</summary>
    public static ActionEventReplaceDto ToActionEventReplaceDto(this IActionEventModel model)
        => new()
        {
            TypeEvent = model.MessageType.ToString(),
            Content = model.Content ?? string.Empty,
            User = model.User ?? string.Empty
        };

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
            Status = EnumTrueFalse.False,
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

        // DeviceProvider가 있으면 ID + TypeDevice로 실제 Device 조회
        if (deviceProvider != null)
        {
            // TypeDevice가 있으면 ID + TypeDevice 우선 매칭
            if (!string.IsNullOrEmpty(deviceDto.TypeDevice) &&
                Enum.TryParse<EnumDeviceType>(deviceDto.TypeDevice, out var filterType))
            {
                var matched = deviceProvider.FirstOrDefault(d => d.Id == deviceDto.Id && d.DeviceType == filterType);
                if (matched != null)
                    return matched;
            }

            // TypeDevice 없거나 매칭 실패 시 ID만으로 조회
            var existing = deviceProvider.FirstOrDefault(d => d.Id == deviceDto.Id);
            if (existing != null)
                return existing;
        }

        // Fallback: DTO에서 가용 속성을 모두 매핑하여 모델 생성
        if (!string.IsNullOrEmpty(deviceDto.TypeDevice) &&
            Enum.TryParse<EnumDeviceType>(deviceDto.TypeDevice, out var deviceType))
        {
            BaseDeviceModel model = deviceType switch
            {
                EnumDeviceType.Controller => new ControllerDeviceModel(),
                EnumDeviceType.IpCamera => new CameraDeviceModel(),
                _ => new SensorDeviceModel()  // Sensor 계열 (Fence, Multi, PIR, etc.)
            };
            model.Id = deviceDto.Id;
            model.DeviceType = deviceType;
            PopulateDeviceFromDto(model, deviceDto);
            return model;
        }

        var fallback = new SensorDeviceModel { Id = deviceDto.Id };
        PopulateDeviceFromDto(fallback, deviceDto);
        return fallback;
    }

    /// <summary>
    /// DTO의 가용 속성을 BaseDeviceModel에 매핑
    /// </summary>
    private static void PopulateDeviceFromDto(BaseDeviceModel model, BaseDeviceDto dto)
    {
        model.DeviceName = dto.NameDevice;
        model.DeviceNumber = dto.NumberDevice;
        model.Version = dto.Version;
        if (!string.IsNullOrEmpty(dto.Status) &&
            Enum.TryParse<EnumDeviceStatus>(dto.Status, out var status))
        {
            model.Status = status;
        }
        model.DeviceGroups = dto.DeviceGroups?.Select(g => g.Id).ToList();
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
            NumberDevice = device.DeviceNumber,
            // 설계 Gop_Message_Broker §6.4 device 필드 준수(status/version/geolocation/controller_id).
            // device_groups는 공용 DTO 구조(name_group) 결정 대기 → 별도(미포함). group_device는 deprecated로 제거됨.
            Status = device.Status.ToString(),
            Version = device.Version ?? string.Empty,
            ControllerId = (device as ISensorDeviceModel)?.Controller?.Id,
            Geolocation = (device.Latitude != 0 || device.Longitude != 0)
                ? new GeolocationDto { Latitude = device.Latitude, Longitude = device.Longitude }
                : null
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // OriginEvent 역변환 헬퍼 (Model → DTO)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// IExEventModel → IEventDto 변환 (ActionEvent의 FromEvent 역변환용)
    /// </summary>
    private static IEventDto? ConvertOriginEventToDto(IExEventModel? originEvent)
    {
        if (originEvent == null)
            return null;

        return originEvent switch
        {
            IDetectionEventModel detection => detection.ToDetectionEventDto(),
            IMalfunctionEventModel malfunction => malfunction.ToMalfunctionEventDto(),
            IConnectionEventModel connection => connection.ToConnectionEventDto(),
            _ => null
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // OriginEvent 변환 헬퍼 (DTO → Model)
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

        switch (fromEvent)
        {
            case DetectionEventDto detectionDto:
                if (eventProvider != null)
                {
                    var existing = eventProvider
                        .OfType<IDetectionEventModel>()
                        .FirstOrDefault(e => e.Id == detectionDto.Id);
                    if (existing != null)
                        return existing;
                }
                return detectionDto.ToDetectionEventModel(deviceProvider);

            case MalfunctionEventDto malfunctionDto:
                if (eventProvider != null)
                {
                    var existing = eventProvider
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
