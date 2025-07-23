using System;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Enums;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/16/2025 4:09:08 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/

public enum RotateModeDto { OFF, ON, AUTO }
public enum SceneOrientationModeDto { Off, Manual, Auto }
public enum EnumIpType { IPv4, IPv6 }
public enum EnumAudioEncoding
{
    PCM,
    G711,
    G726,
    AAC,
    Opus,
    Unknown
}

public enum EnumExposureMode
{

    /// <remarks/>
    AUTO,

    /// <remarks/>
    MANUAL,
}

/// <remarks/>
public enum EnumExposurePriority
{

    /// <remarks/>
    LowNoise,

    /// <remarks/>
    FrameRate,
}

public enum EnumAutoFocusMode
{

    /// <remarks/>
    AUTO,

    /// <remarks/>
    MANUAL,
}

public enum EnumIrCutFilterMode
{

    /// <remarks/>
    ON,

    /// <remarks/>
    OFF,

    /// <remarks/>
    AUTO,
}

public enum EnumBacklightCompensationMode
{

    /// <remarks/>
    OFF,

    /// <remarks/>
    ON,
}

public enum EnumWhiteBalanceMode
{

    /// <remarks/>
    AUTO,

    /// <remarks/>
    MANUAL,
}

public enum EnumWideDynamicMode
{

    /// <remarks/>
    OFF,

    /// <remarks/>
    ON,
}

public enum EnumVideoEncoding
{

    /// <remarks/>
    JPEG,

    /// <remarks/>
    MPEG4,

    /// <remarks/>
    H264,
}
public enum EnumH264Profile
{

    /// <remarks/>
    Baseline,

    /// <remarks/>
    Main,

    /// <remarks/>
    Extended,

    /// <remarks/>
    High,
}
public enum EnumMpeg4Profile
{

    /// <remarks/>
    SP,

    /// <remarks/>
    ASP,
}

/*---------------------------  PT-Control-Direction DTOs  ---------------------------*/
public enum EnumEFlipMode { Off, On, Extended }
public enum EnumReverseMode { Off, On, Auto, Extended }

/*--------------------------- Preset-Tour Enum ---------------------------*/
public enum EnumPtzPresetTourOperation
{
    Start,
    Stop,
    Pause,
    Extended
}

public enum EnumImageStabilizationMode { OFF, ON, AUTO, Extended }

public enum EnumFocusMoveStatus
{
    Idle,
    Moving,
    Unknown
}
