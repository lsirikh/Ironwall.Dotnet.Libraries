using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// 권한그룹 한시부여(grant) 응답 — 서버 <c>GrantResponse</c> 대응(Grant Scheduling v5.2, PRD Grant_Scheduling_Client).
/// <para><c>status</c>=파생상태(ACTIVE/PENDING/EXPIRED/REVOKED). 인가 권위는 서버(클라 표시용).</para>
/// </summary>
public class GrantDto
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("user_id")] public int UserId { get; set; }
    [JsonProperty("user_login_id")] public string? UserLogin { get; set; }   // v5.4 GET /grants 보강 — 비정규화 계정 로그인 ID
    [JsonProperty("user_name")] public string? UserName { get; set; }         // v5.4 GET /grants 보강 — 비정규화 계정 이름
    [JsonProperty("group_id")] public int GroupId { get; set; }
    [JsonProperty("group_name")] public string? GroupName { get; set; }
    [JsonProperty("valid_from")] public DateTime ValidFrom { get; set; }
    [JsonProperty("valid_until")] public DateTime? ValidUntil { get; set; }   // null = 상시(무기한)
    [JsonProperty("is_active")] public bool IsActive { get; set; }
    [JsonProperty("status")] public string Status { get; set; } = string.Empty;
    [JsonProperty("granted_by")] public int? GrantedBy { get; set; }
    [JsonProperty("revoked_at")] public DateTime? RevokedAt { get; set; }
    [JsonProperty("created_at")] public DateTime CreatedAt { get; set; }

    /// <summary>표시용 계정 라벨(UserId→LoginId). 전체 부여 집계 시 클라가 태깅 — 직렬화 제외.</summary>
    [JsonIgnore] public string? UserLabel { get; set; }
}

/// <summary>권한그룹 부여 요청 — 서버 <c>GrantCreate</c> 대응. <c>valid_until</c> 생략/null = 상시.</summary>
public class GrantCreateDto
{
    [JsonProperty("group_id")] public int GroupId { get; set; }
    [JsonProperty("valid_from")] public DateTime ValidFrom { get; set; }
    [JsonProperty("valid_until")] public DateTime? ValidUntil { get; set; }
}
