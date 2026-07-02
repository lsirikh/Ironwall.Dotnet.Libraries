namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// GOP 서버 권위 RBAC 역할. <b>v5.4(Role Simplification)부터 서버는 ADMIN/USER 2종만 발행</b> — 실권한은 group_id 매트릭스로만 판정.
    /// 레거시 5단계(GUEST/VIEWER/OPERATOR/MAINTAINER)는 하위호환 표시용으로 보존(서버 미발행). 오름차순이라 HasRole=(role &gt;= required) 비교 가능하나,
    /// v5.4 권한 판정 권위는 매트릭스(CanView 등)이고 role은 ADMIN bypass 라벨. 정수 직접 캐스팅 금지(<see cref="RoleMappingHelper"/> 경유). — GOP-00 FR-2 / v5.4
    /// </summary>
    public enum EnumUserRole
    {
        UNDEFINED  = 0,
        GUEST      = 1,   // [레거시] v5.4 서버 미발행
        VIEWER     = 2,   // [레거시]
        OPERATOR   = 3,   // [레거시]
        MAINTAINER = 4,   // [레거시]
        USER       = 5,   // v5.4 표준 일반 사용자 — 권한은 group_id 매트릭스로만 (ADMIN 미만 유지: HasRole 안전)
        ADMIN      = 6,   // 특권(require_admin bypass, 매트릭스 무관)
    }
}
