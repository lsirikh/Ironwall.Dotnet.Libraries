using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// 권한 매트릭스 단일소스 — 모듈/verb 축, 서버 JSON 키 매핑, 표시명, 모듈별 활성 verb 규칙.
    /// 권한설정 매트릭스 UI(행=모듈, 열=verb)가 이 카탈로그로 행/열/비활성셀을 구성한다.
    /// 서버 <c>enums.py</c> 권위와 동기화 필요(서버 v5.0 모듈 추가 시 갱신). — PRD-GOP-01 Phase 5 IMPL-05.
    /// </summary>
    public static class PermissionCatalog
    {
        /// <summary>매트릭스 행 순서(서버 enums.py 순서).</summary>
        public static readonly IReadOnlyList<EnumPermissionModule> Modules = new[]
        {
            EnumPermissionModule.Devices, EnumPermissionModule.Events, EnumPermissionModule.Reports,
            EnumPermissionModule.Cameras, EnumPermissionModule.Users, EnumPermissionModule.UserGroups,
            EnumPermissionModule.AuditLogs, EnumPermissionModule.Servers,
            EnumPermissionModule.Map, EnumPermissionModule.Broadcast,
            EnumPermissionModule.SetupSystem, EnumPermissionModule.SetupFeature,
        };

        /// <summary>매트릭스 열 순서.</summary>
        public static readonly IReadOnlyList<EnumPermissionVerb> Verbs = new[]
        {
            EnumPermissionVerb.View, EnumPermissionVerb.Edit, EnumPermissionVerb.Delete, EnumPermissionVerb.Control,
        };

        /// <summary>모듈 서버 JSON 키(<c>UserGroupDto.Permissions.Modules</c> Dictionary 키).</summary>
        public static string ServerKey(EnumPermissionModule m) => m switch
        {
            EnumPermissionModule.Devices    => "devices",
            EnumPermissionModule.Events     => "events",
            EnumPermissionModule.Reports    => "reports",
            EnumPermissionModule.Cameras    => "cameras",
            EnumPermissionModule.Users      => "users",
            EnumPermissionModule.UserGroups => "user_groups",
            EnumPermissionModule.AuditLogs  => "audit_logs",
            EnumPermissionModule.Servers    => "servers",
            EnumPermissionModule.Map        => "map",
            EnumPermissionModule.Broadcast  => "broadcast",
            EnumPermissionModule.SetupSystem  => "setup_system",
            EnumPermissionModule.SetupFeature => "setup_feature",
            _ => m.ToString().ToLowerInvariant(),
        };

        /// <summary>verb 서버 JSON 키(<c>ModulePermissionDto</c> 프로퍼티 lower).</summary>
        public static string ServerKey(EnumPermissionVerb v) => v switch
        {
            EnumPermissionVerb.View    => "view",
            EnumPermissionVerb.Edit    => "edit",
            EnumPermissionVerb.Delete  => "delete",
            EnumPermissionVerb.Control => "control",
            _ => v.ToString().ToLowerInvariant(),
        };

        /// <summary>매트릭스 표시명(한글).</summary>
        public static string DisplayName(EnumPermissionModule m) => m switch
        {
            EnumPermissionModule.Devices    => "장비",
            EnumPermissionModule.Events     => "이벤트",
            EnumPermissionModule.Reports    => "보고서",
            EnumPermissionModule.Cameras    => "카메라",
            EnumPermissionModule.Users      => "사용자",
            EnumPermissionModule.UserGroups => "그룹·권한",
            EnumPermissionModule.AuditLogs  => "감사로그",
            EnumPermissionModule.Servers    => "서버",
            EnumPermissionModule.Map        => "상황도",
            EnumPermissionModule.Broadcast  => "방송",
            EnumPermissionModule.SetupSystem  => "시스템 설정",
            EnumPermissionModule.SetupFeature => "기능 설정",
            _ => m.ToString(),
        };

        /// <summary>verb 표시명(한글).</summary>
        public static string DisplayName(EnumPermissionVerb v) => v switch
        {
            EnumPermissionVerb.View    => "조회",
            EnumPermissionVerb.Edit    => "편집",
            EnumPermissionVerb.Delete  => "삭제",
            EnumPermissionVerb.Control => "제어",
            _ => v.ToString(),
        };

        /// <summary>
        /// 모듈×verb 활성 여부. 비활성(false) 셀은 매트릭스에서 ▦(편집 불가)로 표시.
        /// 규칙: Control은 Cameras만 / AuditLogs는 View만 / 그 외는 View·Edit·Delete.
        /// </summary>
        public static bool IsVerbAllowed(EnumPermissionModule m, EnumPermissionVerb v)
        {
            if (m == EnumPermissionModule.AuditLogs) return v == EnumPermissionVerb.View;
            if (m == EnumPermissionModule.Map)       return v is EnumPermissionVerb.View or EnumPermissionVerb.Edit;     // 상황도 조회/편집
            if (m == EnumPermissionModule.Broadcast) return v is EnumPermissionVerb.View or EnumPermissionVerb.Control;  // 방송 조회/제어
            if (m is EnumPermissionModule.SetupSystem or EnumPermissionModule.SetupFeature) return v is EnumPermissionVerb.View or EnumPermissionVerb.Edit;  // 설정 조회/편집 (v5.2)
            if (v == EnumPermissionVerb.Control) return m == EnumPermissionModule.Cameras;
            return true; // View/Edit/Delete 는 나머지 전부 활성
        }
    }
}
