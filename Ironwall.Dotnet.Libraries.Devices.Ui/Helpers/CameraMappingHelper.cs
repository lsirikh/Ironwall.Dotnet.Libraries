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
        dest.IpPort = src.Port;
        dest.UserName = src.Username;
        dest.UserPassword = src.Password;
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

        /*────────────── 3) HardwareSpec ─────────────*/
        dest.HardwareSpec ??= new CameraInfoModel();           // 객체 없으면 만들어 둠
        dest.HardwareSpec.Name = src.Name;
        dest.HardwareSpec.Location = src.Location;
        dest.HardwareSpec.Manufacturer = src.Manufacturer;
        dest.HardwareSpec.Model = src.DeviceModel;
        dest.HardwareSpec.Hardware = src.HardwareId;
        dest.HardwareSpec.Firmware = src.FirmwareVersion;
        dest.HardwareSpec.DeviceId = src.SerialNumber;
        dest.HardwareSpec.MacAddress = src.MacAddress;
        dest.HardwareSpec.OnvifVersion = src.OnvifVersion;

        return dest;                     // 갱신된 원본 인스턴스
    }
}