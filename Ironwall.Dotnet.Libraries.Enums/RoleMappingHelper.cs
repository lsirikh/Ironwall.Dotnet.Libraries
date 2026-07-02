namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// <see cref="EnumUserRole"/> ↔ <see cref="EnumLevelType"/>(레거시 2단계) 상호 변환 + 문자열 파싱.
    /// v5.4 서버는 role=ADMIN/USER만 발행(Role Simplification) — ParseRole이 "USER"도 매핑(레거시 5단계는 하위호환 보존).
    /// 두 enum 정수값이 다르므로(예: <c>EnumLevelType.ADMIN=1</c> vs <c>EnumUserRole.ADMIN=6</c>) 직접 캐스팅 금지·본 헬퍼 경유. — GOP-00 FR-2 / v5.4
    /// </summary>
    public static class RoleMappingHelper
    {
        /// <summary>
        /// 서버가 보낸 role 문자열("ADMIN"/"VIEWER"/...)을 파싱한다. 실패/미정의 시 안전 기본값(기본=GUEST) 반환.
        /// 로깅은 호출자(게이트웨이) 책임 — 본 헬퍼는 의존성 없이 순수 변환만 한다.
        /// </summary>
        public static EnumUserRole ParseRole(string? value, EnumUserRole fallback = EnumUserRole.GUEST)
            => Enum.TryParse<EnumUserRole>(value, ignoreCase: true, out var role)
               && Enum.IsDefined(typeof(EnumUserRole), role)
                ? role
                : fallback;

        /// <summary>5단계 역할 → 레거시 2단계. ADMIN만 ADMIN, 그 외는 USER(기존 AdminLevelAllowConverter 바인딩 호환).</summary>
        public static EnumLevelType ToLevel(EnumUserRole role)
            => role == EnumUserRole.ADMIN ? EnumLevelType.ADMIN : EnumLevelType.USER;

        /// <summary>레거시 2단계 → EnumUserRole. ADMIN→ADMIN, USER→USER(v5.4 표준), 그 외 GUEST.</summary>
        public static EnumUserRole ToRole(EnumLevelType level) => level switch
        {
            EnumLevelType.ADMIN => EnumUserRole.ADMIN,
            EnumLevelType.USER  => EnumUserRole.USER,
            _                   => EnumUserRole.GUEST,
        };

        /// <summary>권한 강도 비교: 보유 역할이 요구 역할 이상인가.</summary>
        public static bool HasRole(EnumUserRole role, EnumUserRole required) => role >= required;
    }
}
