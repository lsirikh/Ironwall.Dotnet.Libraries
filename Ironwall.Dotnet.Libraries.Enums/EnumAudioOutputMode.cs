using System;

namespace Ironwall.Dotnet.Libraries.Enums;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/10/2025 8:00:14 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public enum EnumAudioOutputMode
{
    WaveOutEvent,  // 기본값
    WaveOut,
    DirectSoundOut,
    WasapiOut
}