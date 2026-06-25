using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Services;

/// <summary>
/// 권한 판정 단일 진입점 (FR-8). 로그인 시 <see cref="Apply"/> 로 role+permissions 보관, 앱측 메뉴/버튼 가드가 소비.
/// <para>모든 판정은 null-safe(미적용/누락=권한없음 안전기본). permissions 는 flat 토큰(`module:verb`, §2.3.2)이라
/// Can* 는 contains 판정이다. 권한 변경 시 <see cref="PermissionsChanged"/> 발화 → 구독 VM 이 CanXxx 재평가.</para>
/// </summary>
public interface IPermissionService
{
    EnumUserRole Role { get; }
    bool IsAdmin { get; }
    /// <summary>보유 역할이 요구 역할 이상인가(강도 비교).</summary>
    bool HasRole(EnumUserRole required);

    bool CanView(string module);
    bool CanEdit(string module);
    bool CanControl(string module);
    bool CanDelete(string module);

    bool HasDeviceGroup(int id);
    IReadOnlyList<int> GetAccessibleDeviceGroups();

    /// <summary>감사 로그 접근(ADMIN 또는 MAINTAINER).</summary>
    bool CanAccessAuditLogs();

    /// <summary>로그인 사용자(권한 포함)로 갱신.</summary>
    void Apply(AuthUserDto user);
    /// <summary>로그아웃/만료 시 초기화.</summary>
    void Clear();

    event Action? PermissionsChanged;
}
