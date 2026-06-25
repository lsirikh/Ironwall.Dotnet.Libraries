using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// 계정/프로필 사용자 응답 DTO (§9.2~9.3).
/// <para>POST /api/auth/login 의 data.user, GET /api/users/me(envelope)·GET /api/auth/me(평면) 공통 페이로드.</para>
/// <para>날짜는 ApiMessageHelper.JsonSettings 의 DateParseHandling.None 때문에 반드시 <see cref="string"/>으로 수신한다.</para>
/// <para>permissions 는 서버가 flat-string(`{"events":"rw"}`)으로 반환(B-3 3원 drift, §2.3.2). 타입 고정 시 역직렬화 실패하므로
/// raw <see cref="JObject"/>로 관용 수용하고, 평탄화(IReadOnlyList&lt;string&gt;)는 ApiAccountGateway/PermissionService가 수행한다.
/// <b>login 응답에만 존재</b>(me/refresh엔 없음, A-4) → 갱신은 GET /api/user-groups/{group_id}.</para>
/// — GOP-00 PRD FR-1
/// </summary>
public class AuthUserDto
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("login_id")] public string LoginId { get; set; } = string.Empty;
    [JsonProperty("name")] public string? Name { get; set; }
    [JsonProperty("email")] public string? Email { get; set; }
    [JsonProperty("department")] public string? Department { get; set; }
    [JsonProperty("position")] public string? Position { get; set; }
    [JsonProperty("employee_number")] public string? EmployeeNumber { get; set; }
    [JsonProperty("photo_url")] public string? PhotoUrl { get; set; }   // ⚠ PUT /users/me 미반영 버그(C-5, v4.7 핫픽스 전)
    [JsonProperty("phone")] public string? Phone { get; set; }

    /// <summary>서버 역할 문자열("ADMIN"/"OPERATOR"/...). RoleMappingHelper.ParseRole 로 EnumUserRole 변환.</summary>
    [JsonProperty("role")] public string? Role { get; set; }
    [JsonProperty("group_id")] public int? GroupId { get; set; }

    /// <summary>그룹 권한 raw(flat-string 또는 nested). login 응답에만 존재. 평탄화는 게이트웨이/PermissionService 담당.</summary>
    [JsonProperty("permissions")] public JObject? Permissions { get; set; }

    [JsonProperty("is_active")] public bool IsActive { get; set; }
    [JsonProperty("is_locked")] public bool IsLocked { get; set; }
    [JsonProperty("lock_reason")] public string? LockReason { get; set; }
    [JsonProperty("locked_at")] public string? LockedAt { get; set; }
    [JsonProperty("last_login_at")] public string? LastLoginAt { get; set; }
    [JsonProperty("last_login_ip")] public string? LastLoginIp { get; set; }
    [JsonProperty("created_at")] public string? CreatedAt { get; set; }
    [JsonProperty("updated_at")] public string? UpdatedAt { get; set; }
}
