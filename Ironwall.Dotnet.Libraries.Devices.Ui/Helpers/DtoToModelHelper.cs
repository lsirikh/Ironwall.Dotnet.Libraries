using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
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
            DeviceType = ParseDeviceType(dto),
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
        DeviceAxesMapper.MapToModel(dto, model, EnumDeviceCategory.Camera, dto.Category, dto.HardwareSpec);

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
    /// 장비 DTO → 옛 종류(<see cref="EnumDeviceType"/>). 정본은 <see cref="DeviceTypeResolver"/> 다(Events.Ui 와 공용).
    /// <para>① <c>type_device</c>(6.3 전문)가 읽히면 그것 — 6.3 결과는 종전과 같다.
    /// ② 못 읽으면 판별자 <c>category_device</c> 로 복원(7.0+). sensor 는 의도적 미매핑 → <c>NONE</c>.</para>
    /// </summary>
    private static EnumDeviceType ParseDeviceType(BaseDeviceDto dto)
        => DeviceTypeResolver.Resolve(dto) ?? EnumDeviceType.NONE;

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
    /// 문 위치 스칼라(<c>gate_status</c>·<c>door_status</c>) 정규화 — <b>모르는 것을 <c>CLOSED</c> 로 덮지 않는다</b>.
    /// </summary>
    /// <remarks>
    /// <para><b>왜</b> — 서버 v2.0 이 문 위치를 스칼라에서 <b>부품 상태</b>로 옮겼다. 8.0.1 라이브 응답 실측
    /// (2026-09-18, <c>GET /api/devices/gates|enclosures?view=full</c>): <c>gate_status</c>·<c>door_status</c> 키가
    /// <b>응답에 아예 없다</b>. 값의 새 자리는 <c>device_status.components.&lt;key&gt;.state</c> 이고
    /// <b>key 는 종류마다 다르다</b> — 함체 <c>door</c>(<c>DOOR_SENSOR</c>) · 통문 <c>actuator</c>(<c>DOOR_ACTUATOR</c>).
    /// 없는 값을 <c>CLOSED</c> 로 채우면 <b>문이 열려 있어도 화면은 닫힘</b>이고, 개폐 명령이 NATS 로 전환된 지금
    /// 운용자가 두 번 누른다. 422 도 예외도 없는 조용한 거짓이다.</para>
    /// <para><b>무엇을 돌려주나</b> — 값이 없으면 <c>CLOSED</c> 가 아니라 <b>빈 문자열</b>(= 모름)이다.
    /// 모델 프로퍼티가 non-nullable <c>string</c> 이라 null 대신 빈 값으로 표현하며,
    /// <c>DoorStateMachine.FromServer("")</c> 는 <see cref="Ironwall.Dotnet.Libraries.Enums.EnumDoorState"/>.Unknown 을
    /// 돌려주므로 지도·3D 개폐 형태는 "닫힘 형태 + '?' 라벨"로 정직하게 그려진다(설계된 경로).</para>
    /// </remarks>
    private static string NormalizeDoorScalar(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim();

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
            Category = DeviceAxesMapper.TypeAxisForWrite(model.TypeAxisCode, model.Category),
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
            DeviceType = ParseDeviceType(dto),
            Version = dto.Version ?? string.Empty,
            Status = ParseDeviceStatus(dto.Status)
        };

        MapGeolocationToModel(dto, model);
        DeviceAxesMapper.MapToModel(dto, model, EnumDeviceCategory.Sensor, dto.TypeDevice, dto.HardwareSpec);

        // Controller 정보가 포함된 경우 변환.
        // 중첩 controller 객체가 없고 controller_id(FK)만 온 경우엔 Id만 seed →
        // NavigationMappingHelper.SetupBidirectionalReferences가 실제 FK로 재링크(orphan/제어기번호 유실 방지).
        if (dto.Controller != null)
        {
            model.Controller = dto.Controller.ToControllerDeviceModel();
        }
        else if (dto.ControllerId > 0)
        {
            model.Controller = new ControllerDeviceModel { Id = dto.ControllerId };
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
            TypeDevice = DeviceAxesMapper.TypeAxisForWrite(model.TypeAxisCode, model.DeviceType),
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
            DeviceType = ParseDeviceType(dto),
            Version = dto.Version ?? string.Empty,
            Status = ParseDeviceStatus(dto.Status),
            IpAddress = dto.IpAddress ?? string.Empty,
            Port = dto.IpPort
        };
        MapGeolocationToModel(dto, model);
        DeviceAxesMapper.MapToModel(dto, model, EnumDeviceCategory.Controller, dto.TypeDevice, dto.HardwareSpec);
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
            TypeDevice = DeviceAxesMapper.TypeAxisForWrite(model.TypeAxisCode, model.DeviceType),
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
            DeviceType = ParseDeviceType(dto),
            Version = dto.Version ?? string.Empty,
            Status = ParseDeviceStatus(dto.Status),
            SpeakerType = dto.SpeakerType ?? "NORMAL",
            Description = dto.Description
        };

        MapGeolocationToModel(dto, model);
        DeviceAxesMapper.MapToModel(dto, model, EnumDeviceCategory.Speaker, dto.TypeSpeaker, dto.HardwareSpec);

        if (dto.Server != null)
            model.Server = dto.Server.ToServerModel();
        // v7.0 은 중첩 `server` 를 없앴다 — 기본 응답에는 `server_id` 뿐이고 확장은 `?include=server` 다
        // (서버 app/routers/speakers.py 머리말 D4). 중첩만 보면 그 판본에서 소속이 통째로 사라져
        // "이미 그 서버" 판정과 되돌리기가 눈을 감는다. id 만 온 경우 최소 모델로 채운다.
        else if (dto.ServerId is > 0)
            model.Server = new ServerModel { Id = dto.ServerId.Value };

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

    // ────────────────────────── Gate (통문, 서버 v6.3) ──────────────────────────

    /// <summary>
    /// 통문 DTO → 모델. <c>urls</c>·<c>link_info</c> 는 서버가 자유 JSONB 라 <b>원본 JSON 문자열</b>로 보존한다
    /// (스키마가 바뀌어도 값이 유실되지 않게 — 파싱은 소비처 책임).
    /// </summary>
    public static GateDeviceModel ToGateDeviceModel(this GateDeviceDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        var model = new GateDeviceModel
        {
            Id = dto.Id,
            DeviceNumber = dto.NumberDevice,
            DeviceGroups = dto.DeviceGroups?.Select(g => g.Id).ToList(),
            DeviceName = dto.NameDevice,
            DeviceType = ParseDeviceType(dto),
            Version = dto.Version ?? string.Empty,
            Status = ParseDeviceStatus(dto.Status),
            // (문 위치) 미상은 CLOSED 가 아니다 — NormalizeDoorScalar remarks 참조.
            // ⚠ 아직 스칼라만 읽는다. GateDeviceDto 에 device_status(축) 프로퍼티가 없어
            //   components.actuator.state 를 읽을 통로가 Devices.Ui 에 없다 → DTO 뷰 생기면 우선순위 배선 필요.
            GateStatus = NormalizeDoorScalar(dto.GateStatus),
            UrlsJson = dto.Urls?.ToString(Newtonsoft.Json.Formatting.None),
            LinkInfoJson = dto.LinkInfo?.ToString(Newtonsoft.Json.Formatting.None),
        };
        MapGeolocationToModel(dto, model);
        DeviceAxesMapper.MapToModel(dto, model, EnumDeviceCategory.Gate, dto.TypeGate, dto.HardwareSpec);
        return model;
    }

    /// <summary>
    /// GateDeviceModel → 쓰기 DTO (device-console-v8 FR-08).
    /// <para><b>문 위치(<c>gate_status</c>)는 싣지 않는다</b> — 관측값이다. 개폐 명령으로도 바뀌지 않고 매니저 보고로만 바뀐다.</para>
    /// <para><c>type_gate</c> 는 값이 있을 때만 — 형상 축은 생략하면 서버가 <c>Unknown</c> 을 배정하고(8.0.1 실측),
    /// <c>null</c> 을 실으면 422 다. 결선(<c>urls</c>·<c>link_info</c>)은 받은 원본 JSON 을 그대로 되돌린다.</para>
    /// </summary>
    public static GateDeviceDto ToGateDeviceDto(this GateDeviceModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        var dto = new GateDeviceDto
        {
            Id = model.Id,
            NumberDevice = model.DeviceNumber,
            GroupIds = model.DeviceGroups,
            NameDevice = model.DeviceName ?? string.Empty,
            TypeDevice = model.DeviceType.ToString(),
            Version = model.Version ?? string.Empty,
            Status = model.Status.ToString(),
            TypeGate = string.IsNullOrWhiteSpace(model.TypeAxisCode) ? null : model.TypeAxisCode!.Trim(),
            Urls = ParseJsonObjectOrNull(model.UrlsJson),
            LinkInfo = ParseJsonObjectOrNull(model.LinkInfoJson),
        };
        MapGeolocationToDto(model, dto);
        return dto;
    }

    private static Newtonsoft.Json.Linq.JObject? ParseJsonObjectOrNull(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return Newtonsoft.Json.Linq.JObject.Parse(json); }
        catch (Newtonsoft.Json.JsonException) { return null; }   // 원본이 객체가 아니면 싣지 않는다(부분 PATCH 라 서버 값은 보존된다)
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
            DeviceType = ParseDeviceType(dto),
            Version = dto.Version ?? string.Empty,
            Status = ParseDeviceStatus(dto.Status),
            // (문 위치) 미상은 CLOSED 가 아니다 — NormalizeDoorScalar remarks 참조.
            // ⚠ 스칼라만 읽는다. EnclosureDeviceDto 에 device_status(축) 프로퍼티가 없어
            //   components.door.state 를 읽을 통로가 Devices.Ui 에 없다 → DTO 뷰 생기면 우선순위 배선 필요.
            DoorStatus = NormalizeDoorScalar(dto.DoorStatus),
            HeaterEnabled = dto.HeaterEnabled,
            FanEnabled = dto.FanEnabled
        };
        // 임계값(threshold_config JObject) → 강타입 모델 (이전엔 드롭 → 재조회 시 임계값 소실)
        model.ThresholdConfig = dto.ThresholdConfig?.ToObject<EnclosureThresholdConfigModel>();
        MapGeolocationToModel(dto, model);
        DeviceAxesMapper.MapToModel(dto, model, EnumDeviceCategory.Enclosure, dto.TypeEnclosure, dto.HardwareSpec);
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
            // (쓰기 경로) 6.3 평면 키는 닫힌 어휘(CLOSED·OPEN)라 빈 값은 422 다 — 미상이면 CLOSED 로 채운다.
            // 읽기와 달리 여기서 CLOSED 를 넣어도 화면 거짓말이 되지 않는다(표시는 모델이 담당).
            // 7.0+ 에서는 DeviceApiService 가 UseAxisWrite=true 로 켜 이 키 자체를 직렬화하지 않는다.
            DoorStatus = string.IsNullOrWhiteSpace(model.DoorStatus) ? "CLOSED" : model.DoorStatus,
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
            DeviceType = ParseDeviceType(dto),
            Version = dto.Version ?? string.Empty,
            Status = ParseDeviceStatus(dto.Status),
            IpAddress = dto.IpAddress ?? string.Empty,
            IpPort = dto.IpPort,
            UserName = dto.UserName,
            UserPassword = dto.UserPassword,
            Description = dto.Description
        };
        MapGeolocationToModel(dto, model);
        DeviceAxesMapper.MapToModel(dto, model, EnumDeviceCategory.Lamp, dto.TypeLamp, dto.HardwareSpec);
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
