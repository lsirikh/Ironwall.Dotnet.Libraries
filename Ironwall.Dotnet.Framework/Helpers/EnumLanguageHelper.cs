using Ironwall.Dotnet.Framework.Constants;
using Ironwall.Dotnet.Framework.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Helpers;

public static class EnumLanguageHelper
{
    public static string GetEventType(string langCode, EnumFaultType enumId) =>
langCode switch
{
    LanguageConst.ENGLISH => enumId switch
    {
        EnumFaultType.FaultController => TypeConst_en.FAULT_CONTROLLER,
        EnumFaultType.FaultFence => TypeConst_en.FAULT_FENCE,
        EnumFaultType.FaultCompound => TypeConst_en.FAULT_MULTI,
        EnumFaultType.FaultCable => TypeConst_en.FAULT_CABLE,
        EnumFaultType.FaultUnderground => TypeConst_en.FAULT_UNDERGROUND,
        EnumFaultType.FaultPIR => TypeConst_en.FAULT_PIR,
        EnumFaultType.FaultIOController => TypeConst_en.FAULT_IOCONTROLLER,
        EnumFaultType.FaultContact => TypeConst_en.FAULT_CONTACT,
        EnumFaultType.FaultIpCamera => TypeConst_en.FAULT_CAMERA,
        EnumFaultType.FaultIpSpeaker => TypeConst_en.FAULT_SPEAKER,
        EnumFaultType.FaultRadar => TypeConst_en.FAULT_RADAR,
        EnumFaultType.FaultOpticalCable => TypeConst_en.FAULT_OPTICAL_CABLE,
        _ => "Unknown"
    },
    LanguageConst.KOREAN => enumId switch
    {
        EnumFaultType.FaultController => TypeConst_kr.FAULT_CONTROLLER,
        EnumFaultType.FaultFence => TypeConst_kr.FAULT_FENCE,
        EnumFaultType.FaultCompound => TypeConst_kr.FAULT_MULTI,
        EnumFaultType.FaultCable => TypeConst_kr.FAULT_CABLE,
        EnumFaultType.FaultUnderground => TypeConst_kr.FAULT_UNDERGROUND,
        EnumFaultType.FaultPIR => TypeConst_kr.FAULT_PIR,
        EnumFaultType.FaultIOController => TypeConst_kr.FAULT_IOCONTROLLER,
        EnumFaultType.FaultContact => TypeConst_kr.FAULT_CONTACT,
        EnumFaultType.FaultIpCamera => TypeConst_kr.FAULT_CAMERA,
        EnumFaultType.FaultIpSpeaker => TypeConst_kr.FAULT_SPEAKER,
        EnumFaultType.FaultRadar => TypeConst_kr.FAULT_RADAR,
        EnumFaultType.FaultOpticalCable => TypeConst_kr.FAULT_OPTICAL_CABLE,
        _ => "알 수 없음"
    },
    _ => enumId switch
    {
        EnumFaultType.FaultController => TypeConst_en.FAULT_CONTROLLER,
        EnumFaultType.FaultFence => TypeConst_en.FAULT_FENCE,
        EnumFaultType.FaultCompound => TypeConst_en.FAULT_MULTI,
        EnumFaultType.FaultCable => TypeConst_en.FAULT_CABLE,
        EnumFaultType.FaultUnderground => TypeConst_en.FAULT_UNDERGROUND,
        EnumFaultType.FaultPIR => TypeConst_en.FAULT_PIR,
        EnumFaultType.FaultIOController => TypeConst_en.FAULT_IOCONTROLLER,
        EnumFaultType.FaultContact => TypeConst_en.FAULT_CONTACT,
        EnumFaultType.FaultIpCamera => TypeConst_en.FAULT_CAMERA,
        EnumFaultType.FaultIpSpeaker => TypeConst_en.FAULT_SPEAKER,
        EnumFaultType.FaultRadar => TypeConst_en.FAULT_RADAR,
        EnumFaultType.FaultOpticalCable => TypeConst_en.FAULT_OPTICAL_CABLE,
        _ => "Unknown"
    }
};


    public static string GetDeviceType(string langCode, EnumDeviceType enumId) =>
 langCode switch
 {
     LanguageConst.ENGLISH => enumId switch
     {
         EnumDeviceType.Controller => UnitConst_en.CONTROLLER,
         EnumDeviceType.Multi => UnitConst_en.MULTI,
         EnumDeviceType.Fence => UnitConst_en.FENCE,
         EnumDeviceType.Underground => UnitConst_en.UNDERGROUND,
         EnumDeviceType.Contact => UnitConst_en.CONTACT,
         EnumDeviceType.PIR => UnitConst_en.PIR,
         EnumDeviceType.IoController => UnitConst_en.IOCONTROLLER,
         EnumDeviceType.Laser => UnitConst_en.LASER,
         EnumDeviceType.Cable => UnitConst_en.CABLE,
         EnumDeviceType.IpCamera => UnitConst_en.CAMERA,
         EnumDeviceType.IpSpeaker => UnitConst_en.SPEAKER,
         EnumDeviceType.Radar => UnitConst_en.RADAR,
         EnumDeviceType.OpticalCable => UnitConst_en.OPTICAL_CABLE,
         _ => "알 수 없음"
     },
     LanguageConst.KOREAN => enumId switch
     {
         EnumDeviceType.Controller => UnitConst_kr.CONTROLLER,
         EnumDeviceType.Multi => UnitConst_kr.MULTI,
         EnumDeviceType.Fence => UnitConst_kr.FENCE,
         EnumDeviceType.Underground => UnitConst_kr.UNDERGROUND,
         EnumDeviceType.Contact => UnitConst_kr.CONTACT,
         EnumDeviceType.PIR => UnitConst_kr.PIR,
         EnumDeviceType.IoController => UnitConst_kr.IOCONTROLLER,
         EnumDeviceType.Laser => UnitConst_kr.LASER,
         EnumDeviceType.Cable => UnitConst_kr.CABLE,
         EnumDeviceType.IpCamera => UnitConst_kr.CAMERA,
         EnumDeviceType.IpSpeaker => UnitConst_kr.SPEAKER,
         EnumDeviceType.Radar => UnitConst_kr.RADAR,
         EnumDeviceType.OpticalCable => UnitConst_kr.OPTICAL_CABLE,
         _ => "Unknown"
     },
     _ => enumId switch
     {
         EnumDeviceType.Controller => UnitConst.CONTROLLER,
         EnumDeviceType.Multi => UnitConst.MULTI,
         EnumDeviceType.Fence => UnitConst.FENCE,
         EnumDeviceType.Underground => UnitConst.UNDERGROUND,
         EnumDeviceType.Contact => UnitConst.CONTACT,
         EnumDeviceType.PIR => UnitConst.PIR,
         EnumDeviceType.IoController => UnitConst.IOCONTROLLER,
         EnumDeviceType.Laser => UnitConst.LASER,
         EnumDeviceType.Cable => UnitConst.CABLE,
         EnumDeviceType.IpCamera => UnitConst.CAMERA,
         EnumDeviceType.IpSpeaker => UnitConst.SPEAKER,
         EnumDeviceType.Radar => UnitConst.RADAR,
         EnumDeviceType.OpticalCable => UnitConst.OPTICAL_CABLE,
         _ => "Unknown"
     }
 };


    public static string GetAutoActionType(string langCode)
    {
        if (LanguageConst.ENGLISH == langCode)
            return "Automatic Action Reporting";
        else if (LanguageConst.KOREAN == langCode)
            return "자동 조치 보고";
        else
            return "Automatic Action Reporting";

    }

    public static string GetAutoRecoveryType(string langCode)
    {
        if (LanguageConst.ENGLISH == langCode)
            return "Auto-recovery report";
        else if (LanguageConst.KOREAN == langCode)
            return "자동 복구 보고 ";
        else
            return "Auto-recovery report";

    }
}
