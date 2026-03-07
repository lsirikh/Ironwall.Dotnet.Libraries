using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Enums;
public enum EnumGopCommand
{
    // 미정의
    NONE = 0,
    // 센서 연결보고
    CONNECTION = 1,
    // 센서 탐지 (레거시 REQ 메시지 호환 — 기존 정수값 2 유지)
    DETECTION = 2,
    // 센서 장애보고
    MALFUNCTION = 3,
    // 이벤트 조치보고
    ACTION = 4,
    // 풍량모드
    WINDY = 5,
    // Ai분석서버 저조도
    AI_LOWLIGHT = 6,
    // Ai분석서버 탐지 (레거시)
    AI_DETECTION = 7,
    // Ai분석서버 트래킹
    AI_TRACKING = 8,
    // SPG Api Ptz 변동
    PTZ_STATUS = 9,
    // RtspPopup 요청/닫기
    RTSP_EVENTCALL = 10,
    // 이벤트 조치보고 (설계 문서 기준 cmd 값 — PUB 메시지용)
    ACTION_REPORT = 11,
    // 장치 동기화 알림 (DBApi가 장치 CRUD 시 발행)
    SYNC_DEVICE = 12,
    // 장치 그룹 동기화 알림 (DBApi가 DeviceGroup CRUD 시 발행)
    SYNC_DEVICE_GROUP = 13,
    // 센서/AI 탐지 (설계 문서 기준 cmd 값 — PUB 메시지용, 정수 라우팅 없음)
    DETECT = 100,
}
