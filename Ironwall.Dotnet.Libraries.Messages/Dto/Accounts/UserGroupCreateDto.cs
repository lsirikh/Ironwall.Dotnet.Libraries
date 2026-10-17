using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// 권한 그룹 생성 요청 DTO (POST /api/user-groups, ADMIN).
/// name 필수(중복명 409). permissions 생략 시 서버 기본(빈 매트릭스=권한 0). — GOP_Permission_Group_Management FR-01
/// </summary>
public class UserGroupCreateDto
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)] public string? Description { get; set; }
    [JsonProperty("permissions", NullValueHandling = NullValueHandling.Ignore)] public PermissionsDto? Permissions { get; set; }
    [JsonProperty("is_active", NullValueHandling = NullValueHandling.Ignore)] public bool? IsActive { get; set; }
}
