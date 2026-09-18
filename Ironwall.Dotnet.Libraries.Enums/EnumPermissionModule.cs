namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// GOP 권한 매트릭스 모듈 <b>사전</b>의 키. 서버 <c>app/utils/enums.py</c>(<c>EnumPermissionModule</c>) 순서와 1:1
    /// (배포 8.0.1 = <b>16종</b> / 운영 6.3.2 = 앞 12종 / 7.0 = 앞 15종).
    /// <para>⚠ <b>권위 목록이 아니다</b> — 화면 행 집합은 <b>서버가 돌려준 키 ∪ 이 사전 중 그 서버 세대가 아는 것</b>이다.
    /// 사전에 없는 키가 오면 버리지 않고 키 문자열 그대로 노출한다(<see cref="PermissionCatalog"/> 클래스 주석).</para>
    /// <para>신설 모듈은 <b>항상 맨 끝에</b> 붙인다 — 서버 enum 순서 = 스웨거 순서 = 권한 화면 행 순서이고,
    /// 중간 삽입은 기존 값의 정수를 흔든다. 세대 판정은 <see cref="PermissionCatalog.MinGeneration"/>.</para>
    /// <para>서버 JSON 키(snake_case)는 <see cref="PermissionCatalog.ServerKey(EnumPermissionModule)"/>,
    /// 역매핑은 <see cref="PermissionCatalog.TryFromServerKey"/>.</para>
    /// </summary>
    public enum EnumPermissionModule
    {
        Devices,     // "devices"     장비 7종의 행 CRUD·그룹·스펙 — 매니저 보고는 control
        Events,      // "events"      이벤트(탐지·장애·연결·조치·억제·통계)
        Reports,     // "reports"     보고서
        Cameras,     // "cameras"     카메라 부속(프리셋·ROI) + 클라 PTZ 게이트 — control 사용
        Users,       // "users"       사용자관리 — 계정 잠금·세션 강제 종료는 control
        UserGroups,  // "user_groups" 그룹·권한
        AuditLogs,   // "audit_logs"  감사로그 (View만)
        Servers,     // "servers"     서버모니터
        Map,         // "map"         상황도(심볼/오버레이/ROI 편집) — View/Edit
        Broadcast,   // "broadcast"   방송(스피커/TTS) — View/Edit/Control
        SetupSystem,  // "setup_system"  시스템 설정 — View/Edit
        SetupFeature, // "setup_feature" 기능 설정 — View/Edit (클라 메뉴 게이팅용)
        // ── 서버 v6.3 후속 ~ v8.0 증설(운영 6.3.2 에는 없음 — 세대 게이트 필수) ──
        ActionReportTemplates, // "action_report_templates" 조치보고 문구 기본값 — View/Edit/Delete
        Integrations,          // "integrations"            이벤트 매핑(카메라·스피커·경광등 연동) — View/Edit/Delete
        Files,                 // "files"                   방송 음원 파일그룹·카메라 썸네일 — View/Edit/Delete
        Units,                 // "units"                   부대 편제·인접·관계도 — View/Edit/Delete
    }
}
