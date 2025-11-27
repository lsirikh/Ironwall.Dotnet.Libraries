using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Enums;
public enum EnumGopCommand
{
    // 미정의
    None,
    // 침입 (90: 0x5A)
    Intrusion,
    // 접점 켜기 (86: 0x56)        
    ContactOn,
    // 접점 끄기 (102: 0x66)
    ContactOff,
    // 연결보고 (104: 0x68)
    Connection,
    // 조치보고 (192: 0xC0)       
    Action,
    // 장애보고 (115: 0x73)
    Fault,
    // 풍량모드 (118: 0x76)
    WindyMode,
    // Ai분석서버 저조도
    Lowlight,
    // Ai분석서버 탐지
    DetectionMode,
    // Ai분석서버 트래킹
    TrackingMode,
    // SPG Api Ptz 변동
    CurrentPtz,
    // RtspPopup 요청/닫기
    EventCall,
}
