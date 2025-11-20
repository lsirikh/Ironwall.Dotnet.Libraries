using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;

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

        // GREEN: 전체 변환 로직 구현
        return new CameraDeviceModel
        {
            Id = dto.Id,
            DeviceNumber = dto.NumberDevice,
            DeviceGroup = dto.GroupDevice,
            DeviceName = dto.NameDevice,
            DeviceType = ParseDeviceType(dto.TypeDevice),
            Version = dto.Version ?? string.Empty,
            Status = ParseDeviceStatus(dto.Status),
            IpAddress = dto.IpAddress ?? string.Empty,
            Port = dto.IpPort,
            Username = dto.UserName,
            Password = dto.UserPassword,
            RtspUri = dto.RtspUri,
            RtspPort = dto.RtspPort,
            Mode = ParseCameraMode(dto.Mode),
            Category = ParseCameraType(dto.Category)
        };
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
            GroupDevice = model.DeviceGroup,
            NameDevice = model.DeviceName,
            TypeDevice = model.DeviceType.ToString(),
            Version = model.Version,
            Status = model.Status.ToString(),
            IpAddress = model.IpAddress,
            IpPort = model.Port,
            UserName = model.Username,
            UserPassword = model.Password,
            RtspUri = model.RtspUri,
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
            DeviceGroup = dto.GroupDevice,
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
            GroupDevice = model.DeviceGroup,
            NameDevice = model.DeviceName,
            TypeDevice = model.DeviceType.ToString(),
            Version = model.Version,
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
            DeviceGroup = dto.GroupDevice,
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
            GroupDevice = model.DeviceGroup,
            NameDevice = model.DeviceName,
            TypeDevice = model.DeviceType.ToString(),
            Version = model.Version,
            Status = model.Status.ToString(),
            IpAddress = model.IpAddress,
            IpPort = model.Port
        };
    }
}
