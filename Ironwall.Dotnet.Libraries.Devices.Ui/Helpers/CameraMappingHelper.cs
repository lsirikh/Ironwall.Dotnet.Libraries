using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.OnvifSolution.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;

/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/18/2025 1:30:49 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/*--------------------------- Window (left-right-top-bottom) ----------*/
public static class CameraMappingHelper
{
    /*------------------------------------------------------------------
     |  CameraOnvifModel → (기존) CameraDeviceModel **인-플레이스** 갱신
     |  - 새 객체를 만들지 않는다.
     |  - dest(기존 인스턴스)가 null 이면 아무 것도 하지 않고 null 반환
     ------------------------------------------------------------------*/
    public static ICameraDeviceModel? ToDeviceModel(this ICameraOnvifModel? src,
                                                   ICameraDeviceModel? dest)
    {
        if (src is null) return dest;   // 원본이 없으면 그대로
        if (dest is null) return null;   // “새로 만들지 않는다”는 요구사항

        /*────────────── 1) 네트워크 기본 ──────────────*/
        dest.IpAddress = src.IpAddress ?? "";
        dest.Port = src.Port;
        dest.RtspPort = src.PortRtsp;
        dest.Username = src.Username;
        dest.Password = src.Password;
        dest.RtspUri = src.CameraMedia?.Profiles.FirstOrDefault()?.MediaUri?.Uri;
        dest.Version = src.FirmwareVersion;
        /*────────────── 2) 분류 · 상태 ───────────────*/
        dest.Category = src.Type switch
        {
            OnvifSolution.Base.Enums.EnumCameraType.FIXED_CAMERA => EnumCameraType.FIXED,
            OnvifSolution.Base.Enums.EnumCameraType.PTZ_CAMERA => EnumCameraType.PTZ,
            _ => EnumCameraType.NONE
        };

        dest.Mode = EnumCameraMode.ONVIF;
        dest.Status = src.CameraStatus switch
        {
            OnvifSolution.Base.Enums.EnumCameraStatus.AVAILABLE => EnumDeviceStatus.ACTIVATED,
            OnvifSolution.Base.Enums.EnumCameraStatus.NOT_AVAILABLE => EnumDeviceStatus.DEACTIVATED,
            _ => EnumDeviceStatus.DEACTIVATED
        };

        /*────────────── 3) Identification ─────────────*/
        dest.Identification ??= new CameraInfoModel();           // 객체 없으면 만들어 둠
        dest.Identification.Name = src.Name;
        dest.Identification.Location = src.Location;
        dest.Identification.Manufacturer = src.Manufacturer;
        dest.Identification.Model = src.DeviceModel;
        dest.Identification.Hardware = src.HardwareId;
        dest.Identification.Firmware = src.FirmwareVersion;
        dest.Identification.DeviceId = src.SerialNumber;
        dest.Identification.MacAddress = src.MacAddress;
        dest.Identification.OnvifVersion = src.OnvifVersion;
        dest.Identification.Uri = src.ServiceUri;

        /*────────────── 4) PTZ 프리셋 ────────────────*/
        dest.Presets ??= new List<ICameraPresetModel>();
        dest.Presets.Clear();

        if (src.CameraMedia?.PTZPresets?.Any() == true)
        {
            dest.Presets.AddRange(
                src.CameraMedia.PTZPresets.Select(p => new CameraPresetModel
                {
                    
                    Preset = int.TryParse(p.Token, out var n) ? n : 0,
                    Name = p.Name,
                    Description = $"Preset {p.Name}",
                    Pitch = p.Position?.PanTilt?.X ?? 0,
                    Tilt = p.Position?.PanTilt?.Y ?? 0,
                    Zoom = p.Position?.Zoom?.X ?? 0,
                    Delay = 0
                }));
        }

        /*────────────── 5) PTZ Capability ─────────────*/
        if (src.CameraMedia?.Profiles?.FirstOrDefault()?.PTZConfig is { } cfg)
        {
            dest.PtzCapability ??= new CameraPtzCapabilityModel();
            dest.PtzCapability.MinPan = cfg.PanTiltLimits?.Range.XRange.Min ?? -180;
            dest.PtzCapability.MaxPan = cfg.PanTiltLimits?.Range.XRange.Max ?? 180;
            dest.PtzCapability.MinTilt = cfg.PanTiltLimits?.Range.YRange.Min ?? -90;
            dest.PtzCapability.MaxTilt = cfg.PanTiltLimits?.Range.YRange.Max ?? 90;
            dest.PtzCapability.MinZoom = cfg.ZoomLimits?.Range.XRange.Min ?? 0;
            dest.PtzCapability.MaxZoom = cfg.ZoomLimits?.Range.XRange.Max ?? 100;
            dest.PtzCapability.MaxVisibleDistance = 0;
            dest.PtzCapability.ZoomLevel = 0;
        }
        else
        {
            dest.PtzCapability = null;   // 관련 정보가 없으면 제거
        }

        return dest;                     // 갱신된 원본 인스턴스
    }
}