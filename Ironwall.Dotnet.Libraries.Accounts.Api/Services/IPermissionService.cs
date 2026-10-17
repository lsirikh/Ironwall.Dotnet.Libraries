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
    /// <summary>로그인 계정 login_id (JWT sub 와 동일). 미로그인 시 null.</summary>
    string? LoginId { get; }
    /// <summary>로그인 계정 표시이름(user.name). 로그인 응답에만 존재(JWT 미포함). 미로그인 시 null. — 회전요청 등 requested_by 소스.</summary>
    string? Name { get; }
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

    // ── Grant Scheduling v5.2 (FR-GS-01) ──
    /// <summary>가장 임박한 grant 만료 시각. null=상시(만료없음). 클라 재조회 타이머(FR-GS-03) 기준.</summary>
    DateTimeOffset? ValidUntil { get; }
    /// <summary>마지막 스냅샷의 서버 시각(재조회 응답). null=미수신(로그인만).</summary>
    DateTimeOffset? ServerTime { get; }
    /// <summary>서버−클라 시계 편차(server_time − localNow). 만료 판정에 <c>localNow + ClockSkew</c> 사용(폐쇄망 NTP 편차 보정).</summary>
    TimeSpan ClockSkew { get; }

    /// <summary>로그인 사용자(권한 포함)로 갱신.</summary>
    void Apply(AuthUserDto user);
    /// <summary>재조회 스냅샷(GET /me/permissions)으로 권한·valid_until·clockSkew 갱신. role/loginId/name 은 유지(스냅샷에 없음). (FR-GS-02)</summary>
    void Refresh(PermissionsSnapshotDto snapshot);
    /// <summary>로그아웃/만료 시 초기화.</summary>
    void Clear();

    event Action? PermissionsChanged;
}
