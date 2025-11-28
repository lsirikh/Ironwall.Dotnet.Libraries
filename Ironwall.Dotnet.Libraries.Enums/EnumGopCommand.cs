using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Enums;
public enum EnumGopCommand
{
    // 미정의
    NONE,
    // 센서 연결보고 
    CONNECTION,
    // 센서 탐지
    DETECTION,
    // 센서 장애보고
    MALFUNCTION,
    // 이벤트 조치보고 
    ACTION,
    // 풍량모드
    WINDYMODE_SETUP,
    // Ai분석서버 저조도
    AI_LOWLIGHT,
    // Ai분석서버 탐지
    AI_DETECTION,
    // Ai분석서버 트래킹
    AI_TRACKING,
    // SPG Api Ptz 변동
    PTZ_STATUS,
    // RtspPopup 요청/닫기
    RTSP_EVENTCALL,
}
