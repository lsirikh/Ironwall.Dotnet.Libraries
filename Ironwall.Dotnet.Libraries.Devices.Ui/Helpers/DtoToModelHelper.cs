using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Servers;
using Newtonsoft.Json.Linq;

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
            IpPort = dto.IpPort,
            UserName = dto.UserName,
            UserPassword = dto.UserPassword,
            Mode = ParseCameraMode(dto.Mode),
            Category = ParseCameraType(dto.Category),
            IsRecord = dto.IsRecord
        };

        MapGeolocationToModel(dto, model);

        if (dto.HardwareSpec != null)
            model.HardwareSpec = ToCameraInfoModel(dto.HardwareSpec);
        if (dto.Urls != null)
            model.Urls = ToCameraUrlsModel(dto.Urls);

        return model;
    }

    // ────────────────────────── Geolocation 공통 매핑 ──────────────────────────

    private static void MapGeolocationToModel(BaseDeviceDto dto, BaseDeviceModel model)
    {
        model.IsEnable = dto.IsEnable;
        if (dto.Geolocation != null)
        {
            model.Location = dto.Geolocation.Location;
            model.Latitude = dto.Geolocation.Latitude;
            model.Longitude = dto.Geolocation.Longitude;
            model.Heading = dto.Geolocation.Heading;   // v4.4: 설치 방위 → 심볼 BaseBearing 구동(메모리)
            model.Altitude = dto.Geolocation.Altitude; // v4.4: 설치 고도(optional)
        }
    }

    private static void MapGeolocationToDto(BaseDeviceModel model, BaseDeviceDto dto)
    {
        dto.IsEnable = model.IsEnable;
        if (!string.IsNullOrEmpty(model.Location) || model.Latitude != 0 || model.Longitude != 0
            || model.Heading.HasValue || model.Altitude.HasValue)
        {
            dto.Geolocation = new GeolocationDto
            {
                Location = model.Location,
                Latitude = model.Latitude,
                Longitude = model.Longitude,
                Heading = model.Heading,    // BLOCKER-1 핫픽스: 저장 시 방위각 API 전송(NullValueHandling.Ignore)
                Altitude = model.Altitude   // 설치 고도(optional, null 시 직렬화 생략)
            };
        }
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

        var dto = new CameraDeviceDto
        {
            Id = model.Id,
            NumberDevice = model.DeviceNumber,
            GroupIds = model.DeviceGroups,
            NameDevice = model.DeviceName ?? string.Empty,
            TypeDevice = model.DeviceType.ToString(),
            Version = model.Version ?? string.Empty,
            Status = model.Status.ToString(),
            IpAddress = model.IpAddress ?? string.Empty,
            IpPort = model.IpPort,
            UserName = model.UserName ?? string.Empty,
            UserPassword = model.UserPassword ?? string.Empty,
            Mode = model.Mode.ToString(),
            Category = model.Category.ToString(),
            IsRecord = model.IsRecord
        };
        MapGeolocationToDto(model, dto);
        if (model.Urls != null)
            dto.Urls = ToCameraUrlsDto(model.Urls);
        if (model.HardwareSpec != null)
            dto.HardwareSpec = ToHardwareSpecDto(model.HardwareSpec);   // 라운드트립(H1 보강 + max_detection_range 저장)
        return dto;
    }

    /// <summary>CameraInfoModel(HardwareSpec) → HardwareSpecDto (model→dto).</summary>
    public static HardwareSpecDto ToHardwareSpecDto(ICameraInfoModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        return new HardwareSpecDto
        {
            Name = model.Name ?? string.Empty,
            Location = model.Location ?? string.Empty,
            Manufacturer = model.Manufacturer ?? string.Empty,
            Model = model.Model ?? string.Empty,
            Hardware = model.Hardware ?? string.Empty,
            Firmware = model.Firmware ?? string.Empty,
            DeviceId = model.DeviceId ?? string.Empty,
            MacAddress = model.MacAddress ?? string.Empty,
            OnvifVersion = model.OnvifVersion ?? string.Empty,
            MaxDetectionRange = model.MaxDetectionRange
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

        MapGeolocationToModel(dto, model);

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
            GroupIds = model.DeviceGroups,
            NameDevice = model.DeviceName ?? string.Empty,
            TypeDevice = model.DeviceType.ToString(),
            Version = model.Version ?? string.Empty,
            Status = model.Status.ToString(),
            ControllerId = model.Controller?.Id ?? 0
        };

        MapGeolocationToDto(model, dto);

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

        var model = new ControllerDeviceModel
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
        MapGeolocationToModel(dto, model);
        return model;
    }

    /// <summary>
    /// ControllerDeviceModel → ControllerDeviceDto 변환
    /// </summary>
    public static ControllerDeviceDto ToControllerDeviceDto(this ControllerDeviceModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        var dto = new ControllerDeviceDto
        {
            Id = model.Id,
            NumberDevice = model.DeviceNumber,
            GroupIds = model.DeviceGroups,
            NameDevice = model.DeviceName ?? string.Empty,
            TypeDevice = model.DeviceType.ToString(),
            Version = model.Version ?? string.Empty,
            Status = model.Status.ToString(),
            IpAddress = model.IpAddress ?? string.Empty,
            IpPort = model.Port
        };
        MapGeolocationToDto(model, dto);
        return dto;
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

        MapGeolocationToModel(dto, model);

        if (dto.Server != null)
            model.Server = dto.Server.ToServerModel();

        return model;
    }

    /// <summary>ServerDto → ServerModel (방송서버 — nested 읽기 + ServerProvider 적재 공용).
    /// ThresholdConfig(JObject↔ServerThresholdConfigModel)는 드롭다운/표시에 불필요하여 미매핑(기존 nested 읽기도 동일).</summary>
    public static ServerModel ToServerModel(this ServerDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        return new ServerModel
        {
            Id = dto.Id,
            CategoryId = dto.CategoryId,
            Name = dto.Name,
            Status = dto.Status,
            IpAddress = dto.IpAddress,
            Port = dto.Port,
            Hostname = dto.Hostname,
            UserName = dto.UserName,
            UserPassword = dto.UserPassword
        };
    }

    public static SpeakerDeviceDto ToSpeakerDeviceDto(this SpeakerDeviceModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        var dto = new SpeakerDeviceDto
        {
            Id = model.Id,
            NumberDevice = model.DeviceNumber,
            GroupIds = model.DeviceGroups,
            NameDevice = model.DeviceName ?? string.Empty,
            TypeDevice = model.DeviceType.ToString(),
            Version = model.Version ?? string.Empty,
            Status = model.Status.ToString(),
            SpeakerType = model.SpeakerType ?? "NORMAL",
            Description = model.Description
        };

        MapGeolocationToDto(model, dto);

        // 쓰기경로 비대칭: 서버는 server_id(int)를 기대 — nested server 객체 대신 ID만 전송.
        // (ShouldSerializeServer()=>false 로 nested는 직렬화 차단). 해제 미지원 → null이면 Ignore로 생략.
        dto.ServerId = model.Server?.Id;

        return dto;
    }

    // ────────────────────────── Enclosure ──────────────────────────

    public static EnclosureDeviceModel ToEnclosureDeviceModel(this EnclosureDeviceDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        var model = new EnclosureDeviceModel
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
        // 임계값(threshold_config JObject) → 강타입 모델 (이전엔 드롭 → 재조회 시 임계값 소실)
        model.ThresholdConfig = dto.ThresholdConfig?.ToObject<EnclosureThresholdConfigModel>();
        MapGeolocationToModel(dto, model);
        return model;
    }

    public static EnclosureDeviceDto ToEnclosureDeviceDto(this EnclosureDeviceModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        var dto = new EnclosureDeviceDto
        {
            Id = model.Id,
            NumberDevice = model.DeviceNumber,
            GroupIds = model.DeviceGroups,
            NameDevice = model.DeviceName ?? string.Empty,
            TypeDevice = model.DeviceType.ToString(),
            Version = model.Version ?? string.Empty,
            Status = model.Status.ToString(),
            DoorStatus = model.DoorStatus ?? "CLOSED",
            HeaterEnabled = model.HeaterEnabled,
            FanEnabled = model.FanEnabled
        };
        // 강타입 임계값 모델 → threshold_config JObject (이전엔 드롭 → 저장 무효).
        // (리뷰 M1) 전 필드 null인 '빈 임계값'은 미전송 — 다이얼로그가 주입한 빈 객체가 서버 값을 null로 덮어쓰는 것 방지.
        var tc = model.ThresholdConfig;
        if (tc != null && (tc.TempHigh.HasValue || tc.TempLow.HasValue
            || tc.HumidityHigh.HasValue || tc.CurrentHigh.HasValue || tc.VoltageLow.HasValue || tc.VibrationHigh.HasValue))
            dto.ThresholdConfig = JObject.FromObject(tc);
        MapGeolocationToDto(model, dto);
        return dto;
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
            OnvifVersion = dto.OnvifVersion,
            MaxDetectionRange = dto.MaxDetectionRange
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

    public static CameraUrlsDto ToCameraUrlsDto(ICameraUrlsModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        var dto = new CameraUrlsDto();

        if (!string.IsNullOrEmpty(model.HomepageUrl))
            dto.Homepage = new CameraHomepageDto { Url = model.HomepageUrl };

        if (!string.IsNullOrEmpty(model.OnvifDeviceService))
            dto.Onvif = new CameraOnvifDto { DeviceService = model.OnvifDeviceService };

        if (!string.IsNullOrEmpty(model.RtspMain) || !string.IsNullOrEmpty(model.RtspSub) || !string.IsNullOrEmpty(model.WebrtcMain))
        {
            dto.Streams = new CameraStreamsDto();
            if (!string.IsNullOrEmpty(model.RtspMain) || !string.IsNullOrEmpty(model.RtspSub))
                dto.Streams.Rtsp = new CameraRtspDto { Main = model.RtspMain ?? string.Empty, Sub = model.RtspSub ?? string.Empty };
            if (!string.IsNullOrEmpty(model.WebrtcMain))
                dto.Streams.Webrtc = new CameraWebrtcDto { Main = model.WebrtcMain };
        }

        if (!string.IsNullOrEmpty(model.SnapshotCh1))
            dto.Snapshot = new CameraSnapshotDto { Ch1 = model.SnapshotCh1 };

        return dto;
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

    /// <summary>CameraSettingModel → CameraSettingDto (카메라 setting 저장 PUT/PATCH 본문)</summary>
    public static CameraSettingDto ToCameraSettingDto(this CameraSettingModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        return new CameraSettingDto
        {
            Id = model.Id,
            CameraId = model.CameraId,
            WeatherMode = model.WeatherMode,
            CameraMode = model.CameraMode,
            Heater = model.Heater,
            Fan = model.Fan,
            Headlight = model.Headlight,
            DayNightMode = model.DayNightMode,
            FocusMode = model.FocusMode,
            IrisMode = model.IrisMode,
            Tracking = model.Tracking,
            Palette = model.Palette
        };
    }

    public static CameraPositionModel ToCameraPositionModel(GeolocationDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        return new CameraPositionModel
        {
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Altitude = dto.Altitude ?? 0   // CameraPositionModel.Altitude는 non-nullable, 미설정 시 0
        };
    }

    // ────────────────────────── DeviceGroup ──────────────────────────

    public static DeviceGroupModel ToDeviceGroupModel(this DeviceGroupDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        return new DeviceGroupModel
        {
            Id = dto.Id,
            Name = dto.Name ?? string.Empty,
            Description = dto.Description,
            DeviceCount = dto.DeviceCount
        };
    }

    public static DeviceGroupDto ToDeviceGroupDto(this IDeviceGroupModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        return new DeviceGroupDto
        {
            Id = model.Id,
            Name = model.Name ?? string.Empty,
            Description = model.Description,
            DeviceCount = model.DeviceCount
        };
    }

    // ────────────────────────── Lamp ──────────────────────────

    public static LampDeviceModel ToLampDeviceModel(this LampDeviceDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        var model = new LampDeviceModel
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
        MapGeolocationToModel(dto, model);
        return model;
    }

    public static LampDeviceDto ToLampDeviceDto(this LampDeviceModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        var dto = new LampDeviceDto
        {
            Id = model.Id,
            NumberDevice = model.DeviceNumber,
            GroupIds = model.DeviceGroups,
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
        MapGeolocationToDto(model, dto);
        return dto;
    }
}
