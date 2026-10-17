using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// 감사 로그 DTO (§9.6, GET /api/audit-logs). 실서버 형태 확인.
/// action_type/resource_type 는 str(tolerant) — 서버가 append-only 라 과거 비-enum 값이 잔존 가능(서버 v51 hardening). — GOP-00 FR-19
/// </summary>
public class AuditLogDto
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("action_type")] public string? ActionType { get; set; }
    [JsonProperty("action_status")] public string? ActionStatus { get; set; }
    [JsonProperty("resource_type")] public string? ResourceType { get; set; }
    [JsonProperty("resource_id")] public int? ResourceId { get; set; }
    [JsonProperty("resource_name")] public string? ResourceName { get; set; }
    [JsonProperty("actor_id")] public int? ActorId { get; set; }
    [JsonProperty("actor_login_id")] public string? ActorLoginId { get; set; }
    [JsonProperty("actor_name")] public string? ActorName { get; set; }
    [JsonProperty("actor_role")] public string? ActorRole { get; set; }
    [JsonProperty("changes")] public JObject? Changes { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("ip_address")] public string? IpAddress { get; set; }
    [JsonProperty("user_agent")] public string? UserAgent { get; set; }
    [JsonProperty("error_message")] public string? ErrorMessage { get; set; }
    [JsonProperty("created_at")] public string? CreatedAt { get; set; }
}
