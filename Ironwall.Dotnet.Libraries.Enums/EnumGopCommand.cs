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
    // 탐지 이벤트 동기화 알림 (DBApi가 탐지 UPDATE/DELETE 시 all.sync.detection 로 발행 — PTZ 회전 후 썸네일 갱신).
    // 처리=라이브러리 DetectionSyncNatsService(cmd 문자열 이름매칭). 정수는 유일성만(SYNC_* 라우팅은 이름 기반). 메인 라우터 Unknown 경고 회피용.
    SYNC_DETECTION = 29,
    // 억제(정비 창) 스케줄 동기화 알림 (API 6.3.3 — 생성/수정/취소 + 회차 경계마다 발행).
    // body.suppressing(bool, 선택)만 토글되고 status 는 active 로 불변이라 status 만 보면 전이를 놓친다.
    // 처리=라이브러리 EventSuppressionSyncNatsService(cmd 문자열 이름매칭, 폴링 가속 전용).
    // 정수는 유일성만. 메인 라우터 Unknown 경고 회피용.
    SYNC_EVENT_SUPPRESSION = 30,
    // ── 전역(global) 자원 동기화 — 서버 GLOBAL_CMDS ────────────────────────────
    //  ⚠ 아래 3종은 `sensorway.global.…` 으로 발행된다(부대 토큰이 아님).
    //     서버 `db_monitor/main.py:64` GLOBAL_CMDS = {SYNC_CATALOG, SYNC_CATEGORY,
    //     SYNC_ACTION_REPORT_TEMPLATE, SYNC_FILE_GROUP, SYNC_UNIT} + v8.0.4 SYNC_UNIT_LAYOUT.
    //     정수는 유일성만 갖는다(SYNC_* 라우팅은 이름 기반). 메인 라우터 Unknown 경고 회피용.
    // 카탈로그(장비유형·카테고리 등) 동기화.
    SYNC_CATALOG = 31,
    // 조치보고 문구 템플릿 동기화 (API 6.3 후속 — /reorder 는 {UPDATED, 0} 한 건).
    //   처리=호스트 라우터 → ActionReportTemplatesChangedMessage. 전환기에는 부대 subject 로도 같은 봉투 id 가 와서 id 로 한 번만 옮긴다.
    SYNC_ACTION_REPORT_TEMPLATE = 32,
    // 부대 편제 동기화 (API v8.0). 처리=호스트 라우터 → UnitTopologyChangedMessage + 부대 캐시 무효화.
    //   ⚠ 인접 추가/제거의 resource_id 는 인접 행 id 다(부대 id 아님 — 서버 명세 불일치, 보고됨).
    SYNC_UNIT = 33,
    // 부대 관계도 배치 문서 동기화 (서버 v8.0.4 · 브로커 v2.0.7 §9.18). subject `sensorway.global.all.sync.unit-layout`(하이픈),
    //   body {action:"UPDATED", resource_id:<새 문서 판>} — resource_id 는 부대 id 가 아니라 문서 판(version)이다. 요청 1건 = 알림 1건.
    //   처리=호스트 라우터 → UnitLayoutChangedMessage(판). 가진 판 이하면 관계도가 버린다(자기 저장 메아리 포함).
    SYNC_UNIT_LAYOUT = 34,
    // ── GIS 가 호스트 라우터에서 쓰지 않는 수신 cmd (WP-1 ⑩, 2026-09-30) ──────────────────────────
    //  미정의면 메시지마다 "Unknown type_command" + "Unknown command: NONE" 경고 2줄이 남았다. 인식만 하고 건너뛴다.
    //  정수는 유일성만(라우팅은 이름 기반).
    // 운영 이벤트(통문 · 함체 개폐 · 임계치) — 처리=라이브러리 OperationEventNatsSyncService(cmd 자가 필터).
    OPERATION_EVENT = 35,
    // 시스템 이벤트(서버 발행, 브로커 v1.5 신규) — GIS 소비처 없음.
    SYSTEM_EVENT = 36,
    // 운영 이벤트 동기화 알림 — GIS 캐시 없음.
    SYNC_OPERATION_EVENT = 37,
    // 시스템 이벤트 동기화 알림 — GIS 캐시 없음.
    SYNC_SYSTEM_EVENT = 38,
    // 함체 계측(온도 · 전압 …) 주기 발행 — GIS 소비처 없음.
    ENCLOSURE_METRICS = 39,
    // 센서/AI 탐지 (설계 문서 기준 cmd 값 — PUB 메시지용, 정수 라우팅 없음)
    DETECT = 100,
}
