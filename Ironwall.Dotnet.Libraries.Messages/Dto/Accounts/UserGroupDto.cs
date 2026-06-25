using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// 사용자 그룹 DTO (§9.4, GET /api/user-groups). 실서버 확인 필드.
/// user_count 는 목록에서 null(상세 호출 시 채워짐, B-5). — GOP-00 PRD FR-19
/// </summary>
public class UserGroupDto
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("permissions")] public PermissionsDto? Permissions { get; set; }
    [JsonProperty("is_active")] public bool IsActive { get; set; }
    [JsonProperty("user_count")] public int? UserCount { get; set; }
    [JsonProperty("created_at")] public string? CreatedAt { get; set; }
    [JsonProperty("updated_at")] public string? UpdatedAt { get; set; }
}
