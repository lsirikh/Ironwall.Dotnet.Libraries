using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Services.Onvif;

/// <summary>
/// ONVIF 조회 결과 → 카메라 Draft(Id 0, 서버 미반영). 저장은 카메라 패널의 [저장] 이 기존 검증과 함께 한다(Temp-state).
/// </summary>
public static class OnvifCameraDraftFactory
{
    public static CameraDeviceModel Create(int deviceNumber, string host, int port, string? userName, string? password,
        OnvifLookupResult lookup, string? discoveredName = null)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        var category = lookup.IsPtz ? EnumCameraType.PTZ : EnumCameraType.FIXED;
        return new CameraDeviceModel
        {
            DeviceNumber = deviceNumber,
            DeviceName = DisplayName(lookup, discoveredName, host, port),
            IpAddress = host,
            IpPort = port,
            UserName = string.IsNullOrEmpty(userName) ? null : userName,
            UserPassword = string.IsNullOrEmpty(password) ? null : password,
            Mode = EnumCameraMode.ONVIF,
            Category = category,
            // 조회로 PTZ 여부를 알았으니 상세 칸의 필수 '종류 *'(type_camera)도 채운다 — [추가] 직후 바로 [등록] 할 수 있게.
            // 서버 어휘(FIXED · PTZ)가 enum 이름과 같다. 조회 전(PTZ?)에는 [추가] 가 없으므로 여기 오는 값은 늘 확정이다.
            TypeAxisCode = category.ToString(),
            HardwareSpec = new CameraInfoModel
            {
                Name = lookup.Name ?? discoveredName,
                Location = lookup.Location,
                Manufacturer = lookup.Manufacturer,
                Model = lookup.Model,
                Hardware = lookup.HardwareId,
                Firmware = lookup.Firmware,
                DeviceId = lookup.SerialNumber,
                MacAddress = lookup.MacAddress,
                OnvifVersion = lookup.OnvifVersion,
            },
            Urls = new CameraUrlsModel
            {
                OnvifDeviceService = lookup.DeviceServiceUrl,
                RtspMain = lookup.RtspMain,
                RtspSub = lookup.RtspSub,
            },
        };
    }

    /// <summary>카메라가 알린 이름 → 검색 때 이름 → "제조사 모델" → 주소.</summary>
    public static string DisplayName(OnvifLookupResult lookup, string? discoveredName, string host, int port)
    {
        if (!string.IsNullOrWhiteSpace(lookup.Name)) return lookup.Name.Trim();
        if (!string.IsNullOrWhiteSpace(discoveredName)) return discoveredName.Trim();
        var maker = string.Join(' ', new[] { lookup.Manufacturer, lookup.Model }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));
        return string.IsNullOrEmpty(maker) ? $"ONVIF {host}:{port}" : maker;
    }
}
