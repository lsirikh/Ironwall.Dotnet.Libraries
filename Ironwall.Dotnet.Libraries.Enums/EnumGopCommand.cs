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
    // 카메라 특정위치 확인(GPS 조준) — 지도 클릭 좌표로 nvr_manager.ptz PTZ 회전 요청 (GIS→NVRManager, REQ — v1.5.2에서 PUB 예외 폐지, RSP 확인 필수). PTZ_* Absolute 패밀리.
    PTZ_AIM_LOCATION = 14,
    // ── SYNC 동기화 알림 (DBApi가 CRUD 시 all.sync.* 로 발행). cmd 문자열 이름 매칭(Enum.TryParse). ──
    // 이벤트매핑 동기화 (all.sync.event_mapping)
    SYNC_EVENT_MAPPING = 15,
    // 프리셋 동기화 (all.sync.preset) — is_restricted_zone(감시금지구역, v4.6) 포함
    SYNC_PRESET = 16,
    // 이하 5종은 GIS 무시 가능 — cmd 인식(파싱)용으로만 정의(미정의 시 "Unknown" 경고 유발)
    SYNC_SERVER = 17,
    SYNC_CATEGORY = 18,
    SYNC_FILE_GROUP = 19,
    SYNC_CAMERA_SETTING = 20,
    SYNC_PROXY_SETTING = 21,
    // ── GIS→매니저 제어 (v1.5.2 §6.4: 전부 REQ 발행 + RSP(success/req_id/message) 확인) ──
    // 음원 재생 (GIS→BroadcastingManager, broadcast_manager.play)
    BROADCAST_PLAY = 22,
    // 방송 정지 (GIS→BroadcastingManager, broadcast_manager.stop)
    BROADCAST_STOP = 23,
    // 경광등 이벤트 연동 해제 (GIS→PidsProxy, proxy.lamp-clear — lamp_ids 생략 시 전체)
    LAMP_CLEAR = 24,
    // 경광등 비활성화 (GIS→PidsProxy, proxy.lamp-off)
    LAMP_OFF = 25,
    // 경광등 색상 직접 설정 (GIS→PidsProxy, proxy.lamp-color)
    LAMP_COLOR_SET = 26,
    // 경광등 부저 직접 설정 (GIS→PidsProxy, proxy.lamp-buzzer)
    LAMP_BUZZER_SET = 27,
    // 추적 상태 보고 수신 (AiAnalysis→GIS, gis.tracking-status — 처리=라이브러리 TrackingStatusNatsSyncService, 여기 정의는 메인 라우터 Unknown 경고 회피용)
    TRACKING_STATUS = 28,
    // 센서/AI 탐지 (설계 문서 기준 cmd 값 — PUB 메시지용, 정수 라우팅 없음)
    DETECT = 100,
}
