using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// 사용자 세션 DTO (§9.5, GET /api/user-sessions). 실서버 확인 필드. 날짜=string(KST +09:00). — GOP-00 PRD FR-19
/// </summary>
public class UserSessionDto
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("user_id")] public int UserId { get; set; }
    [JsonProperty("login_id")] public string? LoginId { get; set; }
    [JsonProperty("role")] public string? Role { get; set; }
    [JsonProperty("ip_address")] public string? IpAddress { get; set; }
    [JsonProperty("user_agent")] public string? UserAgent { get; set; }
    [JsonProperty("expires_at")] public string? ExpiresAt { get; set; }
    [JsonProperty("is_active")] public bool IsActive { get; set; }
    [JsonProperty("logout_reason")] public string? LogoutReason { get; set; }
    [JsonProperty("logged_out_at")] public string? LoggedOutAt { get; set; }
    [JsonProperty("created_at")] public string? CreatedAt { get; set; }
    [JsonProperty("updated_at")] public string? UpdatedAt { get; set; }

    /// <summary>커넥션 주체 식별(서버 v6.3 user_sessions.client_id). 구버전 서버는 null → UI "-".</summary>
    [JsonProperty("client_id")] public string? ClientId { get; set; }

    /// <summary>현재(내) 세션 표시 — 서버 sid 클레임(session_id) ↔ 행 id 정확 대조(서버 회신 2026-08-03). 클라 계산(비직렬화).
    /// sid 미제공 구서버에서만 login_id+active 근사로 폴백(동일 계정 다중세션 구분 불가).</summary>
    [JsonIgnore] public bool IsCurrentSession { get; set; }
}
