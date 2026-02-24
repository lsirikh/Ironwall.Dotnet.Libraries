using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Servers;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;

/// <summary>
/// DTO ↔ Model 변환 Helper
/// <para>ApiService가 반환하는 DTO를 ViewModel에서 사용하는 Model로 변환</para>
/// </summary>
public static class DtoToModelHelper
{
    // ────────────────────────── DTO → Model 변환 ──────────────────────────

    /// <summary>
    /// CameraDeviceDto → CameraDeviceModel 변환
    /// </summary>
    public static CameraDeviceModel ToCameraDeviceModel(this CameraDeviceDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        var model = new CameraDeviceModel
        {
            Id = dto.Id,
            DeviceNumber = dto.NumberDevice,
            DeviceGroups = dto.DeviceGroups?.Select(g => g.Id).ToList(),
            DeviceName = dto.NameDevice,
            DeviceType = ParseDeviceType(dto.TypeDevice),
            Version = dto.Version ?? string.Empty,
            Status = ParseDeviceStatus(dto.Status),
            IpAddress = dto.IpAddress ?? string.Empty,
            Port = dto.IpPort,
            Username = dto.UserName,
            Password = dto.UserPassword,
            RtspUri = dto.RtspUri,
            RtspPort = dto.RtspPort ?? 0,
            Mode = ParseCameraMode(dto.Mode),
            Category = ParseCameraType(dto.Category),
            IsRecord = dto.IsRecord
        };

        if (dto.HardwareSpec != null)
            model.Identification = ToCameraInfoModel(dto.HardwareSpec);
        if (dto.Urls != null)
            model.Urls = ToCameraUrlsModel(dto.Urls);

        return model;
    }

    // ────────────────────────── Enum 파싱 헬퍼 ──────────────────────────

    /// <summary>
    /// String → EnumDeviceType 변환
    /// <para>"IpCamera" → EnumDeviceType.IpCamera</para>
    /// </summary>
    private static EnumDeviceType ParseDeviceType(string? typeDevice)
    {
        if (string.IsNullOrEmpty(typeDevice))
            return EnumDeviceType.NONE;

        return Enum.TryParse<EnumDeviceType>(typeDevice, true, out var result)
            ? result
            : EnumDeviceType.NONE;
    }

    /// <summary>
    /// String → EnumDeviceStatus 변환
    /// <para>"ACTIVATED" → EnumDeviceStatus.ACTIVATED</para>
    /// </summary>
    private static EnumDeviceStatus ParseDeviceStatus(string? status)
    {
        if (string.IsNullOrEmpty(status))
            return EnumDeviceStatus.DEACTIVATED;

        return Enum.TryParse<EnumDeviceStatus>(status, true, out var result)
            ? result
            : EnumDeviceStatus.DEACTIVATED;
    }

    /// <summary>
    /// String → EnumCameraMode 변환
    /// <para>"ONVIF" → EnumCameraMode.ONVIF</para>
    /// </summary>
    private static EnumCameraMode ParseCameraMode(string? mode)
    {
        if (string.IsNullOrEmpty(mode))
            return EnumCameraMode.NONE;

        return Enum.TryParse<EnumCameraMode>(mode, true, out var result)
            ? result
            : EnumCameraMode.NONE;
    }

    /// <summary>
    /// String → EnumCameraType 변환
    /// <para>"PTZ" → EnumCameraType.PTZ</para>
    /// </summary>
    private static EnumCameraType ParseCameraType(string? category)
    {
        if (string.IsNullOrEmpty(category))
            return EnumCameraType.NONE;

        return Enum.TryParse<EnumCameraType>(category, true, out var result)
            ? result
            : EnumCameraType.NONE;
    }

    // ────────────────────────── Model → DTO 변환 ──────────────────────────

    /// <summary>
    /// CameraDeviceModel → CameraDeviceDto 변환
    /// </summary>
    public static CameraDeviceDto ToCameraDeviceDto(this CameraDeviceModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        return new CameraDeviceDto
        {
            Id = model.Id,
            NumberDevice = model.DeviceNumber,
            DeviceGroups = model.DeviceGroups?.Select(id => new DeviceGroupDto { Id = id }).ToList(),
            NameDevice = model.DeviceName ?? string.Empty,
            TypeDevice = model.DeviceType.ToString(),
            Version = model.Version ?? string.Empty,
            Status = model.Status.ToString(),
            IpAddress = model.IpAddress ?? string.Empty,
            IpPort = model.Port,
            UserName = model.Username ?? string.Empty,
            UserPassword = model.Password ?? string.Empty,
            RtspUri = model.RtspUri ?? string.Empty,
            RtspPort = model.RtspPort,
            Mode = model.Mode.ToString(),
            Category = model.Category.ToString()
        };
    }

    /// <summary>
    /// SensorDeviceDto → SensorDeviceModel 변환
    /// </summary>
    public static SensorDeviceModel ToSensorDeviceModel(this SensorDeviceDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        var model = new SensorDeviceModel
        {
            Id = dto.Id,
            DeviceNumber = dto.NumberDevice,
            DeviceGroups = dto.DeviceGroups?.Select(g => g.Id).ToList(),
            DeviceName = dto.NameDevice,
            DeviceType = ParseDeviceType(dto.TypeDevice),
            Version = dto.Version ?? string.Empty,
            Status = ParseDeviceStatus(dto.Status)
        };

        // Controller 정보가 포함된 경우 변환
        if (dto.Controller != null)
        {
            model.Controller = dto.Controller.ToControllerDeviceModel();
        }

        return model;
    }

    /// <summary>
    /// SensorDeviceModel → SensorDeviceDto 변환
    /// </summary>
    public static SensorDeviceDto ToSensorDeviceDto(this SensorDeviceModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        var dto = new SensorDeviceDto
        {
            Id = model.Id,
            NumberDevice = model.DeviceNumber,
            DeviceGroups = model.DeviceGroups?.Select(id => new DeviceGroupDto { Id = id }).ToList(),
            NameDevice = model.DeviceName ?? string.Empty,
            TypeDevice = model.DeviceType.ToString(),
            Version = model.Version ?? string.Empty,
            Status = model.Status.ToString(),
            ControllerId = model.Controller?.Id ?? 0
        };

        // Controller 정보가 있는 경우 변환
        if (model.Controller != null)
        {
            dto.Controller = ((ControllerDeviceModel)model.Controller).ToControllerDeviceDto();
        }

        return dto;
    }

    /// <summary>
    /// ControllerDeviceDto → ControllerDeviceModel 변환
    /// </summary>
    public static ControllerDeviceModel ToControllerDeviceModel(this ControllerDeviceDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        return new ControllerDeviceModel
        {
            Id = dto.Id,
            DeviceNumber = dto.NumberDevice,
            DeviceGroups = dto.DeviceGroups?.Select(g => g.Id).ToList(),
            DeviceName = dto.NameDevice,
            DeviceType = ParseDeviceType(dto.TypeDevice),
            Version = dto.Version ?? string.Empty,
            Status = ParseDeviceStatus(dto.Status),
            IpAddress = dto.IpAddress ?? string.Empty,
            Port = dto.IpPort
        };
    }

    /// <summary>
    /// ControllerDeviceModel → ControllerDeviceDto 변환
    /// </summary>
    public static ControllerDeviceDto ToControllerDeviceDto(this ControllerDeviceModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        return new ControllerDeviceDto
        {
            Id = model.Id,
            NumberDevice = model.DeviceNumber,
            DeviceGroups = model.DeviceGroups?.Select(id => new DeviceGroupDto { Id = id }).ToList(),
            NameDevice = model.DeviceName ?? string.Empty,
            TypeDevice = model.DeviceType.ToString(),
            Version = model.Version ?? string.Empty,
            Status = model.Status.ToString(),
            IpAddress = model.IpAddress ?? string.Empty,
            IpPort = model.Port
        };
    }

    // ────────────────────────── Speaker ──────────────────────────

    public static SpeakerDeviceModel ToSpeakerDeviceModel(this SpeakerDeviceDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        var model = new SpeakerDeviceModel
        {
            Id = dto.Id,
            DeviceNumber = dto.NumberDevice,
            DeviceGroups = dto.DeviceGroups?.Select(g => g.Id).ToList(),
            DeviceName = dto.NameDevice,
            DeviceType = ParseDeviceType(dto.TypeDevice),
            Version = dto.Version ?? string.Empty,
            Status = ParseDeviceStatus(dto.Status),
            SpeakerType = dto.SpeakerType ?? "NORMAL",
            Description = dto.Description
        };

        if (dto.Server != null)
        {
            model.Server = new ServerModel
            {
                Id = dto.Server.Id,
                CategoryId = dto.Server.CategoryId,
                Name = dto.Server.Name,
                Status = dto.Server.Status,
                IpAddress = dto.Server.IpAddress,
                Port = dto.Server.Port,
                Hostname = dto.Server.Hostname,
                UserName = dto.Server.UserName,
                UserPassword = dto.Server.UserPassword
            };
        }

        return model;
    }

    public static SpeakerDeviceDto ToSpeakerDeviceDto(this SpeakerDeviceModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        var dto = new SpeakerDeviceDto
        {
            Id = model.Id,
            NumberDevice = model.DeviceNumber,
            DeviceGroups = model.DeviceGroups?.Select(id => new DeviceGroupDto { Id = id }).ToList(),
            NameDevice = model.DeviceName ?? string.Empty,
            TypeDevice = model.DeviceType.ToString(),
            Version = model.Version ?? string.Empty,
            Status = model.Status.ToString(),
            SpeakerType = model.SpeakerType ?? "NORMAL",
            Description = model.Description
        };

        if (model.Server != null)
        {
            dto.Server = new ServerDto
            {
                Id = model.Server.Id,
                CategoryId = model.Server.CategoryId,
                Name = model.Server.Name,
                Status = model.Server.Status,
                IpAddress = model.Server.IpAddress,
                Port = model.Server.Port,
                Hostname = model.Server.Hostname,
                UserName = model.Server.UserName,
                UserPassword = model.Server.UserPassword
            };
        }

        return dto;
    }

    // ────────────────────────── Enclosure ──────────────────────────

    public static EnclosureDeviceModel ToEnclosureDeviceModel(this EnclosureDeviceDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        return new EnclosureDeviceModel
        {
            Id = dto.Id,
            DeviceNumber = dto.NumberDevice,
            DeviceGroups = dto.DeviceGroups?.Select(g => g.Id).ToList(),
            DeviceName = dto.NameDevice,
            DeviceType = ParseDeviceType(dto.TypeDevice),
            Version = dto.Version ?? string.Empty,
            Status = ParseDeviceStatus(dto.Status),
            DoorStatus = dto.DoorStatus ?? "CLOSED",
            HeaterEnabled = dto.HeaterEnabled,
            FanEnabled = dto.FanEnabled
        };
    }

    public static EnclosureDeviceDto ToEnclosureDeviceDto(this EnclosureDeviceModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        return new EnclosureDeviceDto
        {
            Id = model.Id,
            NumberDevice = model.DeviceNumber,
            DeviceGroups = model.DeviceGroups?.Select(id => new DeviceGroupDto { Id = id }).ToList(),
            NameDevice = model.DeviceName ?? string.Empty,
            TypeDevice = model.DeviceType.ToString(),
            Version = model.Version ?? string.Empty,
            Status = model.Status.ToString(),
            DoorStatus = model.DoorStatus ?? "CLOSED",
            HeaterEnabled = model.HeaterEnabled,
            FanEnabled = model.FanEnabled
        };
    }

    // ────────────────────────── Camera Sub-Model 변환 ──────────────────────────

    public static CameraInfoModel ToCameraInfoModel(HardwareSpecDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        return new CameraInfoModel
        {
            Name = dto.Name,
            Location = dto.Location,
            Manufacturer = dto.Manufacturer,
            Model = dto.Model,
            Hardware = dto.Hardware,
            Firmware = dto.Firmware,
            DeviceId = dto.DeviceId,
            MacAddress = dto.MacAddress,
            OnvifVersion = dto.OnvifVersion
        };
    }

    public static CameraUrlsModel ToCameraUrlsModel(CameraUrlsDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        return new CameraUrlsModel
        {
            HomepageUrl = dto.Homepage?.Url,
            OnvifDeviceService = dto.Onvif?.DeviceService,
            RtspMain = dto.Streams?.Rtsp?.Main,
            RtspSub = dto.Streams?.Rtsp?.Sub,
            WebrtcMain = dto.Streams?.Webrtc?.Main,
            SnapshotCh1 = dto.Snapshot?.Ch1
        };
    }

    public static CameraSettingModel ToCameraSettingModel(CameraSettingDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        return new CameraSettingModel
        {
            Id = dto.Id,
            CameraId = dto.CameraId,
            WeatherMode = dto.WeatherMode,
            CameraMode = dto.CameraMode,
            Heater = dto.Heater,
            Fan = dto.Fan,
            Headlight = dto.Headlight,
            DayNightMode = dto.DayNightMode,
            FocusMode = dto.FocusMode,
            IrisMode = dto.IrisMode,
            Tracking = dto.Tracking,
            Palette = dto.Palette
        };
    }

    public static CameraPositionModel ToCameraPositionModel(GeolocationDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        return new CameraPositionModel
        {
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Altitude = dto.Altitude
        };
    }

    // ────────────────────────── Lamp ──────────────────────────

    public static LampDeviceModel ToLampDeviceModel(this LampDeviceDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        return new LampDeviceModel
        {
            Id = dto.Id,
            DeviceNumber = dto.NumberDevice,
            DeviceGroups = dto.DeviceGroups?.Select(g => g.Id).ToList(),
            DeviceName = dto.NameDevice,
            DeviceType = ParseDeviceType(dto.TypeDevice),
            Version = dto.Version ?? string.Empty,
            Status = ParseDeviceStatus(dto.Status),
            IpAddress = dto.IpAddress ?? string.Empty,
            IpPort = dto.IpPort,
            UserName = dto.UserName,
            UserPassword = dto.UserPassword,
            Description = dto.Description
        };
    }

    public static LampDeviceDto ToLampDeviceDto(this LampDeviceModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        return new LampDeviceDto
        {
            Id = model.Id,
            NumberDevice = model.DeviceNumber,
            DeviceGroups = model.DeviceGroups?.Select(id => new DeviceGroupDto { Id = id }).ToList(),
            NameDevice = model.DeviceName ?? string.Empty,
            TypeDevice = model.DeviceType.ToString(),
            Version = model.Version ?? string.Empty,
            Status = model.Status.ToString(),
            IpAddress = model.IpAddress ?? string.Empty,
            IpPort = model.IpPort,
            UserName = model.UserName,
            UserPassword = model.UserPassword,
            Description = model.Description
        };
    }
}
