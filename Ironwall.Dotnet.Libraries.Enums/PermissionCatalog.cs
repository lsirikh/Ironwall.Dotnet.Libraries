using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// 권한 매트릭스 <b>사전</b> — 모듈/verb 축의 서버 JSON 키·표시명·동작 적용성.
    /// <para>⚠ <b>이것은 권위 목록이 아니다.</b> 화면 행 집합의 권위는 <b>서버</b>다
    /// (<c>GET /api/user-groups/{id}</c> 의 <c>permissions.modules</c> 키 ∪ 서버 세대가 아는 어휘).
    /// 이 카탈로그는 "우리가 <b>표시명과 동작 적용성을 아는</b> 모듈들의 사전"일 뿐이고,
    /// 사전에 없는 모듈 키가 서버에서 오면 <b>버리지 않고</b> 키 문자열 그대로 행으로 띄운다
    /// (<c>PermissionMatrixPanelViewModel.OnClickGroupDetail</c>).</para>
    /// <para><b>왜 이렇게 바뀌었나</b> — 하드코딩 12종은 서버 16종(개발 8.0.1)과 불일치해
    /// <c>action_report_templates</c>·<c>files</c>·<c>integrations</c>·<c>units</c> 를 <b>운영자가 켤 수단이 없었다</b>
    /// (비-ADMIN 영구 403). 12→16 으로 숫자만 늘리면 다음 판(17종)에 같은 사고가 반복된다.</para>
    /// <para><b>출처</b>(2026-09-18 실측) — 서버 <c>app/utils/enums.py</c> <c>EnumPermissionModule</c> 16종 ·
    /// 운영 6.3.2 스웨거 <c>EnumPermissionModule</c> 12종 · 조치보고 문구 NOTIFY v2.1 §5.</para>
    /// </summary>
    public static class PermissionCatalog
    {
        #region - 서버 세대(generation) -
        /// <summary>서버 계약 6.x — 권한 모듈 <b>12종</b>(현 운영 6.3.2).</summary>
        public const int GEN_V6_3 = 0;
        /// <summary>서버 계약 7.0 — <c>action_report_templates</c>·<c>integrations</c>·<c>files</c> 추가로 <b>15종</b>.</summary>
        public const int GEN_V7_0 = 1;
        /// <summary>서버 계약 8.0 — <c>units</c> 추가로 <b>16종</b>.</summary>
        public const int GEN_V8_0 = 2;

        /// <summary>
        /// 이 모듈이 <b>처음 등장한 서버 세대</b>. 값은 <c>EnumServerContract</c>(V6_3=0·V7_0=1·V8_0=2)와 같은 축이다
        /// (Enums 어셈블리는 Api 를 참조하지 않으므로 정수로 표현한다 — 비교는 <c>&gt;=</c> 로만 한다).
        /// <para>⚠ 서버는 <c>action_report_templates</c>(v6.3 후속)·<c>integrations</c>·<c>files</c>(v6.3.17)를
        /// <b>major 6 안에서</b> 늘렸지만, 버전 프로브는 <c>6.x</c> 전체를 <see cref="GEN_V6_3"/> 로 본다.
        /// 그래서 6.3.17 서버에서는 이 3종이 세대 판정만으로는 안 보이는데, 서버가 기존 그룹에 백필(v102)해 두어
        /// <b>원본 키로 들어와 행이 뜬다</b>(행 집합 = 원본 ∪ 세대 어휘 — 설계가 스스로 메운다).</para>
        /// </summary>
        public static int MinGeneration(EnumPermissionModule m) => m switch
        {
            EnumPermissionModule.ActionReportTemplates => GEN_V7_0,
            EnumPermissionModule.Integrations          => GEN_V7_0,
            EnumPermissionModule.Files                 => GEN_V7_0,
            EnumPermissionModule.Units                 => GEN_V8_0,
            _ => GEN_V6_3,
        };

        /// <summary>해당 세대 서버가 <b>아는</b> 모듈(선언 순서 = 서버 enum 순서 = 화면 행 순서).</summary>
        public static IEnumerable<EnumPermissionModule> ModulesForGeneration(int generation)
            => Modules.Where(m => MinGeneration(m) <= generation);
        #endregion

        /// <summary>사전에 실린 모듈(서버 <c>enums.py</c> 열거 순서). <b>행 집합의 권위가 아니다</b> — 클래스 주석 참조.</summary>
        public static readonly IReadOnlyList<EnumPermissionModule> Modules = new[]
        {
            EnumPermissionModule.Devices, EnumPermissionModule.Events, EnumPermissionModule.Reports,
            EnumPermissionModule.Cameras, EnumPermissionModule.Users, EnumPermissionModule.UserGroups,
            EnumPermissionModule.AuditLogs, EnumPermissionModule.Servers,
            EnumPermissionModule.Map, EnumPermissionModule.Broadcast,
            EnumPermissionModule.SetupSystem, EnumPermissionModule.SetupFeature,
            EnumPermissionModule.ActionReportTemplates, EnumPermissionModule.Integrations,
            EnumPermissionModule.Files, EnumPermissionModule.Units,
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
            EnumPermissionModule.ActionReportTemplates => "action_report_templates",
            EnumPermissionModule.Integrations          => "integrations",
            EnumPermissionModule.Files                 => "files",
            EnumPermissionModule.Units                 => "units",
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

        /// <summary>
        /// 서버 키 → 사전 모듈. 사전에 없으면 <c>false</c> — 호출부는 <b>버리지 말고</b> 키 그대로 노출한다.
        /// </summary>
        public static bool TryFromServerKey(string? serverKey, out EnumPermissionModule module)
        {
            module = default;
            if (string.IsNullOrWhiteSpace(serverKey)) return false;
            return ByServerKey.TryGetValue(serverKey.Trim(), out module);
        }

        private static readonly IReadOnlyDictionary<string, EnumPermissionModule> ByServerKey =
            Modules.ToDictionary(ServerKey, m => m, StringComparer.OrdinalIgnoreCase);

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
            EnumPermissionModule.ActionReportTemplates => "조치보고 문구",
            EnumPermissionModule.Integrations          => "이벤트 매핑(연동)",
            EnumPermissionModule.Files                 => "파일(음원·썸네일)",
            EnumPermissionModule.Units                 => "부대 편제",
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
        /// 모듈×verb 활성 여부. 비활성(false) 셀은 매트릭스에서 ▦(편집 불가)로 표시 —
        /// <b>표시 힌트일 뿐</b>이고 서버 스키마(<c>ModulePermission</c>)는 어느 모듈에든 4동작을 다 받는다.
        /// <para><b>출처</b>: 서버 <c>app/utils/enums.py</c> <c>EnumPermissionModule</c>/<c>EnumPermissionVerb</c> 주석(배포 8.0.1).
        /// <list type="bullet">
        /// <item><c>audit_logs</c> = view only</item>
        /// <item><c>map</c>·<c>setup_system</c>·<c>setup_feature</c> = view/edit</item>
        /// <item><c>broadcast</c> = view/edit/control</item>
        /// <item><c>devices</c>·<c>users</c>·<c>cameras</c> = view/edit/delete/<b>control</b>
        ///   (devices=매니저 보고(통문·함체 상태·부품·계측) · users=계정 잠금·세션 강제 종료 · cameras=PTZ 게이트)</item>
        /// <item>그 외(<c>events</c>·<c>reports</c>·<c>user_groups</c>·<c>servers</c>·<c>action_report_templates</c>·
        ///   <c>integrations</c>·<c>files</c>·<c>units</c>) = view/edit/delete (control 미사용)</item>
        /// </list></para>
        /// </summary>
        public static bool IsVerbAllowed(EnumPermissionModule m, EnumPermissionVerb v) => m switch
        {
            // 감사로그는 조회만(서버 주석 "view only").
            EnumPermissionModule.AuditLogs => v is EnumPermissionVerb.View,
            // 상황도·설정 2종은 조회/편집.
            EnumPermissionModule.Map or EnumPermissionModule.SetupSystem or EnumPermissionModule.SetupFeature
                => v is EnumPermissionVerb.View or EnumPermissionVerb.Edit,
            // 방송은 조회/편집/제어(삭제 없음).
            EnumPermissionModule.Broadcast
                => v is EnumPermissionVerb.View or EnumPermissionVerb.Edit or EnumPermissionVerb.Control,
            // control 을 실제로 쓰는 3종 — 4동작 전부.
            EnumPermissionModule.Devices or EnumPermissionModule.Users or EnumPermissionModule.Cameras => true,
            // 나머지(신규 4종 포함) — 조회/편집/삭제.
            _ => v is EnumPermissionVerb.View or EnumPermissionVerb.Edit or EnumPermissionVerb.Delete,
        };
    }
}
