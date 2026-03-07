using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
/****************************************************************************
   Purpose      : PIDS 마커 우클릭 상세보기 — URL 생성 및 Chrome 실행 서비스 구현
   Created By   : GHLee
   Created On   : 2026-03-05
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class DeviceDetailUrlService : IDeviceDetailUrlService
{
    private static readonly Dictionary<string, (string Node, string Device)> _deviceMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Controller"]  = ("svms-device-controller", "PIDS_CONTROLLER"),
            ["SmartSensor"] = ("svms-device-sensor",     "PIDS_SENSOR"),
            ["IpCamera"]    = ("svms-device-camera",     "IP_CAMERA"),
            ["IpSpeaker"]   = ("svms-device-speaker",    "SPEAKER"),
            ["Lamp"]        = ("svms-device-lamp",        "LAMP"),
            ["Enclosure"]   = ("svms-device-enclosure",  "ENCLOSURE"),
        };

    private readonly IMainControlWebSetupModel _setup;

    public DeviceDetailUrlService(IMainControlWebSetupModel setup)
    {
        _setup = setup;
    }

    public string BuildUrl(EnumDeviceType deviceType, int deviceId, string? panel = "detail")
    {
        var key = deviceType.ToString();
        if (!_deviceMap.TryGetValue(key, out var pair))
            return string.Empty;

        var ip = _setup.IpAddrerssWebServer.TrimEnd(':');
        var host = _setup.PortWebServer > 0
            ? $"{ip}:{_setup.PortWebServer}"
            : ip;

        var baseUrl = $"http://{host}/ssw-svms?node={pair.Node}&device={pair.Device}";
        return string.IsNullOrEmpty(panel)
            ? baseUrl
            : $"{baseUrl}&panel={panel}&panelId={deviceId}";
    }

    public void OpenInChrome(string url)
    {
        if (string.IsNullOrEmpty(url)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "chrome",
                Arguments = url,
                UseShellExecute = false
            });
        }
        catch
        {
            // Chrome 미설치 시 기본 브라우저 폴백
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
    }
}
