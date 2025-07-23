using Ironwall.Dotnet.Libraries.Enums;
using System;

namespace Ironwall.Dotnet.Libraries.Sounds.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/10/2025 7:58:05 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class AudioDeviceInfo : IAudioDeviceInfo
{
    public int Id { get; set; }
    public int DeviceNumber { get; set; } = -1;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public EnumAudioType DeviceType { get; set; }
    public bool IsDefault { get; set; }
    public bool IsEnabled { get; set; }
    public object? NativeDevice { get; set; } // WaveOut용 int, WASAPI용 MMDevice 등
}