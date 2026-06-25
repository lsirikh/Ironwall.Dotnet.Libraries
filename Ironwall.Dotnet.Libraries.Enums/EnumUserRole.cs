namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// GOP 서버 권위 RBAC 역할 (권한 강도 오름차순).
    /// 오름차순 정렬이므로 HasRole(required) 판정을 (role &gt;= required) 단순 비교로 처리할 수 있다.
    /// 레거시 <see cref="EnumLevelType"/>(ADMIN/USER 2단계)와는 <see cref="RoleMappingHelper"/>로만 상호 변환한다
    /// (정수값이 서로 달라 직접 캐스팅 금지). — GOP-00 PRD FR-2.
    /// </summary>
    public enum EnumUserRole
    {
        UNDEFINED  = 0,
        GUEST      = 1,
        VIEWER     = 2,
        OPERATOR   = 3,
        MAINTAINER = 4,
        ADMIN      = 5,
    }
}
