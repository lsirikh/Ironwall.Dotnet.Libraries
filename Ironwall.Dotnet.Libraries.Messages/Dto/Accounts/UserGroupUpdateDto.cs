using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// 권한 그룹 메타 수정 요청 DTO (PUT /api/user-groups/{id}, ADMIN) — name/description/is_active 부분수정.
/// ⚠ permissions 는 권한상승 방지로 제외 — 전용 경로(POST /user-groups/{id}/permissions) 사용. — FR-02
/// </summary>
public class UserGroupUpdateDto
{
    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)] public string? Name { get; set; }
    [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)] public string? Description { get; set; }
    [JsonProperty("is_active", NullValueHandling = NullValueHandling.Ignore)] public bool? IsActive { get; set; }
}
