namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// GOP 권한 매트릭스의 모듈(행) 단일소스. 서버 <c>enums.py</c>(PermissionModule) 권위와 1:1.
    /// 서버 JSON 키(snake_case)는 <see cref="PermissionCatalog.ServerKey"/>로 매핑한다
    /// (<c>UserGroupDto.Permissions.Modules</c> Dictionary 문자열키 = 서버키).
    /// — PRD-GOP-01 Phase 5 IMPL-05.
    /// </summary>
    public enum EnumPermissionModule
    {
        Devices,     // "devices"     장비
        Events,      // "events"      이벤트
        Reports,     // "reports"     보고서
        Cameras,     // "cameras"     카메라 (유일하게 Control 활성)
        Users,       // "users"       사용자관리
        UserGroups,  // "user_groups" 그룹·권한
        AuditLogs,   // "audit_logs"  감사로그 (View만 활성)
        Servers,     // "servers"     서버모니터
        Map,         // "map"         상황도(심볼/오버레이/ROI 편집) — View/Edit (FR-EN-05)
        Broadcast,   // "broadcast"   방송(스피커/TTS) — View/Control (FR-EN-05)
    }
}
