using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;

/// <summary>버튼/기능 UI 게이팅 최종 상태(T6 중앙 권한 필터의 산출).</summary>
public enum EnumUiGate
{
    /// <summary>권한 있음 — 활성.</summary>
    Enabled,
    /// <summary>권한 없음 — 비활성(회색). 기본 정책(사용자 결정 2026-07-02).</summary>
    Disabled,
    /// <summary>권한 없음 — 숨김. hideWhenDenied 채택 화면 전용.</summary>
    Hidden
}

/// <summary>
/// T6 — **중앙 권한 필터**. 프로그램 전반의 모든 버튼/기능이 거치는 단일 게이트.
/// <para><c>(IPermissionService, module, verb)</c> → <see cref="EnumUiGate"/>. 표시모델 정책을 한 곳에서 결정한다
/// (사용자 결정 2026-07-02: 권한 없으면 <b>기본 Disabled(회색)</b>, 특정 화면만 Hidden).</para>
/// <para>미등록 <see cref="IPermissionService"/>(오프라인/테스트/DB모드) → <c>Enabled</c> 폴백(전체허용). 실제 차단은 서버 응답이 최종(ADR v5.2).</para>
/// <para>모듈 키는 <see cref="PermissionCatalog.ServerKey(EnumPermissionModule)"/> 또는 문자열("devices","events"…). ADMIN 은 IPermissionService 내부 bypass.</para>
/// <para><b>모듈 목록을 여기 두지 않는다</b>(2026-09-18 확인) — 게이트는 <b>키 문자열 통과</b>이고 판정은
/// <see cref="IPermissionService"/> 가 서버 스냅샷(<c>/api/auth/me/permissions</c>)을 flatten 한 토큰 집합으로 한다.
/// 그래서 서버가 어휘를 늘려도(8.0.1 = 16종: <c>action_report_templates</c>·<c>integrations</c>·<c>files</c>·<c>units</c> 포함)
/// <b>이 파일은 손댈 필요가 없다</b>. 새 모듈의 게이팅은 호출부에서 그 키를 넘기면 즉시 동작한다.
/// 동작 적용성(어떤 verb 가 의미 있는지)은 <see cref="PermissionCatalog.IsVerbAllowed"/> 가 유일한 사전이다.</para>
/// </summary>
public static class PermissionUiPolicy
{
    /// <summary>
    /// verb별 권한 보유 여부. <paramref name="perm"/> null → <b>true (fail-open 폴백 — 의도된 설계, T6 2회차 결정)</b>.
    /// <para>근거(ADR v5.2): ① 비GOP(DB인증)·오프라인 배포엔 RBAC 개념이 없으므로 막으면 앱이 무력화 → 허용.
    /// ② GOP+token 모드에선 IPermissionService가 반드시 등록되어 실제 권한이 반환되고(폴백 미발동), 클라 우회 시에도 서버 403이 최종 차단.
    /// fail-closed(`?? false`)로 바꾸면 DB모드 전 기능이 비활성되므로 채택 안 함. GMaps `MapViewModel` L969/972/975 `?? true`도 동일 근거.</para>
    /// </summary>
    public static bool Allowed(IPermissionService? perm, string module, EnumPermissionVerb verb)
    {
        if (perm is null) return true;   // fail-open 폴백(의도) — 위 XML 근거 참조
        return verb switch
        {
            EnumPermissionVerb.View    => perm.CanView(module),
            EnumPermissionVerb.Edit    => perm.CanEdit(module),
            EnumPermissionVerb.Delete  => perm.CanDelete(module),
            EnumPermissionVerb.Control => perm.CanControl(module),
            _ => false
        };
    }

    /// <summary>UI 게이트 상태. <paramref name="hideWhenDenied"/>=true면 권한없음→Hidden, 기본은 Disabled.</summary>
    public static EnumUiGate Gate(IPermissionService? perm, string module, EnumPermissionVerb verb, bool hideWhenDenied = false)
        => Allowed(perm, module, verb) ? EnumUiGate.Enabled
         : hideWhenDenied ? EnumUiGate.Hidden : EnumUiGate.Disabled;

    /// <summary>enum 오버로드(<see cref="PermissionCatalog.ServerKey(EnumPermissionModule)"/> 경유).</summary>
    public static EnumUiGate Gate(IPermissionService? perm, EnumPermissionModule module, EnumPermissionVerb verb, bool hideWhenDenied = false)
        => Gate(perm, PermissionCatalog.ServerKey(module), verb, hideWhenDenied);

    /// <summary>IsEnabled 바인딩용 — 권한 있으면 true.</summary>
    public static bool IsEnabled(IPermissionService? perm, string module, EnumPermissionVerb verb)
        => Allowed(perm, module, verb);

    /// <summary>Visibility 바인딩용(hide 정책 화면) — 권한 있으면 true(Visible).</summary>
    public static bool IsVisible(IPermissionService? perm, string module, EnumPermissionVerb verb)
        => Allowed(perm, module, verb);
}
