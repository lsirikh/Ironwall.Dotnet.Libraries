using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/// <summary>
/// DeviceType에 따라 디바이스 목록을 필터링하는 유틸리티.
/// MarkerFactory, PropertyPanelFactory 양쪽에서 공유.
/// </summary>
internal static class DeviceFilterHelper
{
    /// <summary>
    /// DeviceType에 따라 디바이스 목록을 필터링합니다.
    /// </summary>
    internal static IEnumerable<IBaseDeviceModel> FilterDevicesByType(
        IEnumerable<IBaseDeviceModel> devices,
        EnumDeviceType targetType)
    {
        return targetType switch
        {
            EnumDeviceType.Controller =>
                devices.Where(d => d.DeviceType == EnumDeviceType.Controller),

            EnumDeviceType.Multi =>
                devices.Where(d => d.DeviceType == EnumDeviceType.Multi),

            EnumDeviceType.IpCamera =>
                devices.Where(d => d.DeviceType == EnumDeviceType.IpCamera),

            EnumDeviceType.IpSpeaker =>
                devices.Where(d => d.DeviceType == EnumDeviceType.IpSpeaker),

            EnumDeviceType.Enclosure =>
                devices.Where(d => d.DeviceType == EnumDeviceType.Enclosure),

            EnumDeviceType.Lamp =>
                devices.Where(d => d.DeviceType == EnumDeviceType.Lamp),

            // Fence 계열 센서들
            EnumDeviceType.Fence or
            EnumDeviceType.Underground or
            EnumDeviceType.Contact or
            EnumDeviceType.PIR or
            EnumDeviceType.Laser or
            EnumDeviceType.Cable or
            EnumDeviceType.OpticalCable =>
                devices.Where(d =>
                    d.DeviceType == EnumDeviceType.Fence ||
                    d.DeviceType == EnumDeviceType.Underground ||
                    d.DeviceType == EnumDeviceType.Contact ||
                    d.DeviceType == EnumDeviceType.PIR ||
                    d.DeviceType == EnumDeviceType.Laser ||
                    d.DeviceType == EnumDeviceType.Cable ||
                    d.DeviceType == EnumDeviceType.OpticalCable),

            // 기타: 전체 목록
            _ => devices
        };
    }
}
